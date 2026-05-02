# Background Jobs Architecture

Famick Home Management runs recurring background work — daily notifications,
calendar reminder polling, external calendar sync, address verification — as
**one-shot CLI invocations of the same web container image**, fired by an
external scheduler. The web container itself does **not** host any
`BackgroundService`; it is only an HTTP server.

This page describes the abstraction (`IJob` + `JobRunner`), the container's
two execution modes (`web` vs `run-job`), how scheduling works in each
deployment model, and the idempotency story.

---

## Overview

```
┌──────────────────────────────┐         ┌─────────────────────────────┐
│ Self-hosted (docker-compose) │         │ Cloud (AWS)                 │
│                              │         │                             │
│  ┌──────────┐  ┌───────────┐ │         │  EventBridge Scheduler      │
│  │   web    │  │scheduler  │ │         │            │                │
│  │ (Kestrel)│  │supercronic│ │         │            ▼                │
│  └──────────┘  └────┬──────┘ │         │      ECS RunTask            │
│                     │        │         │            │                │
│                     ▼        │         │            ▼                │
│         dotnet ... run-job   │         │ dotnet ... run-job <name>   │
│         <name> (each tick)   │         │ (Fargate, exits at end)     │
└──────────────────────────────┘         └─────────────────────────────┘
                │                                    │
                └────────────────┬───────────────────┘
                                 ▼
                      ┌──────────────────────┐
                      │  IJob.RunJob(...)    │
                      │  + Distributed Lock  │
                      │  + Per-tenant scope  │
                      └──────────────────────┘
```

Same image, same `IJob` implementations, same lock primitive. Only the
scheduler is environment-specific.

---

## Why one-shot CLI invocations?

The previous shape was three `BackgroundService`s registered with
`AddHostedService<>` in the web app. Each held its own `Task.Delay`-loop
inside the long-lived web process. That worked but had three drawbacks:

1. **Coupling.** Job execution shared a process and a dependency graph
   with the HTTP request path. A misbehaving job could starve Kestrel
   threads or hold open DB connections.
2. **Scaling.** The web container scales to handle HTTP load, not to run
   jobs. An idle off-hours instance still spends memory on hosted-service
   timers; a busy peak instance still runs the same number of jobs.
3. **Visibility.** Jobs logged into the web container's log stream,
   intermixed with request handling.

Splitting jobs into one-shot tasks gives them their own container
runtime, their own logs, and a scheduler whose only job is firing them.

---

## The IJob contract

[IJob](../src/Famick.HomeManagement.Core/Interfaces/IJob.cs):

```csharp
public interface IJob
{
    Task RunJob(ILogger logger, CancellationToken ct);
}
```

Two notable choices:

- **Logger as a parameter** rather than DI. The dispatcher creates a
  logger named `Job.<job-key>` so each job's output is filterable
  without each implementation having to know its own name.
- **No `string Name { get; }`.** The job key is a DI registration
  detail, not part of the implementation contract. Keys are assigned
  via .NET 8+ keyed services in `StartupExtensions`.

---

## Job dispatch

### Registration

Each library exposes a `StartupExtensions.Add<X>(IServiceCollection, IConfiguration)`
that registers its jobs as **keyed scoped** services:

```csharp
// src/Famick.HomeManagement.Jobs/StartupExtensions.cs
services.AddKeyedScoped<IJob, NotificationsDailyJob>("notifications-daily");
services.AddKeyedScoped<IJob, CalendarRemindersJob>("calendar-reminders");
services.AddKeyedScoped<IJob, ExternalCalendarSyncJob>("external-calendar-sync");
```

```csharp
// homemanagement-cloud/src/Famick.HomeManagement.Cloud.Jobs/StartupExtensions.cs
services.AddKeyedScoped<IJob, VerifyAddressesJob>("verify-addresses");
```

Both `Program.cs` files call `services.AddJobs(configuration)` (shared);
cloud also calls `services.AddCloudJobs(configuration)`. All registrations
land in the same keyed `IJob` collection — `JobRunner` doesn't care which
project owns a given key.

### Dispatch

[JobRunner.RunAsync](../src/Famick.HomeManagement.Jobs/JobRunner.cs):

```csharp
using var scope = services.CreateScope();
var job = scope.ServiceProvider.GetKeyedService<IJob>(jobKey);
if (job is null) return 64;       // EX_USAGE — unknown job key
try { await job.RunJob(logger, ct); return 0; }
catch (OperationCanceledException) when (ct.IsCancellationRequested) { return 130; }
catch { return 1; }
```

Exit codes:

| Code | Meaning                                                |
| ---- | ------------------------------------------------------ |
| 0    | Job ran to completion (including the lock-skipped path)|
| 1    | Job threw an unhandled exception                       |
| 64   | Job key not registered (CLI typo / misconfigured cron) |
| 130  | Job canceled (SIGINT / scheduler aborted the task)     |

### CLI mode

Both `Program.cs` files short-circuit on `args[0] == "run-job"` **after**
service registration but **before** middleware/Kestrel:

```csharp
if (isJobMode)
{
    return await JobRunner.RunAsync(app.Services, jobKey!, CancellationToken.None);
}
// ... middleware pipeline + app.Run() for web mode ...
```

The DI container is built identically in both modes, so jobs see the same
DbContext, ITenantProvider, IDistributedLockService, etc. Kestrel never
starts in run-job mode; the process exits with the JobRunner's code.

Invocation:

```sh
dotnet Famick.HomeManagement.Web.dll run-job <job-key> [extra args]
```

(Self-hosted assembly is the same name. The cloud project is also
`Famick.HomeManagement.Web.dll`.)

---

## Per-tenant scoping

Each job iterates tenants from the database and creates a fresh DI scope
per tenant, calling `tenantProvider.SetTenantId(tenantId)` inside the
scope. This is how multi-tenant query filters get the right context in a
non-HTTP code path. Pattern:

```csharp
foreach (var tenantId in tenantIds)
{
    using var scope = _scopeFactory.CreateScope();
    var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
    tenantProvider.SetTenantId(tenantId);
    // resolve scoped services and run per-tenant work
}
```

The outer tenant query also runs in its own throwaway scope. Errors in
one tenant are caught and logged; the loop continues with the next.

---

## Idempotency: the distributed lock

Each `IJob.RunJob` opens a [`IDistributedLockService`](../src/Famick.HomeManagement.Core/Interfaces/IDistributedLockService.cs)
guard before doing any work:

```csharp
await using var lockHandle = await _lockService.TryAcquireLockAsync(LockKey, LockExpiry, ct);
if (lockHandle is null)
{
    logger.LogInformation("Another instance is already running ... Skipping.");
    return;
}
```

Two implementations:

- **Self-hosted**: [`NoOpDistributedLockService`](../src/Famick.HomeManagement.Infrastructure/Services/NoOpDistributedLockService.cs) — always succeeds. Safe because
  the docker-compose `scheduler` service runs as a single replica, so the
  lock is theoretically unnecessary; it stays in place so the IJob impls
  remain identical across deployments.
- **Cloud**: [`RedisDistributedLockService`](../homemanagement-cloud/src/Famick.HomeManagement.Cloud.Infrastructure/Services/RedisDistributedLockService.cs) — `SET NX EX` against Redis with a
  Lua-script compare-and-delete on release. If two ECS tasks for the
  same schedule fire concurrently (e.g. EventBridge retry), only one
  does work; the other returns exit 0 after logging the skip.

Lock TTLs are tuned per-job (1h for daily notifications, 10min for the
5-minute reminder check, 30min for external calendar sync) so a crashed
holder can't park the lock indefinitely.

See `docs/architecture.md` for the broader Redis/multi-instance story.

---

## Self-hosted scheduling

A second docker-compose service named `scheduler` runs the **same image**
as `web`, with its command overridden to launch `supercronic`:

```yaml
# docker-compose.yml
scheduler:
  image: famick/homemanagement:latest
  command: ["supercronic", "/app/scheduler-crontab"]
  environment:
    - ConnectionStrings__DefaultConnection=...
    # (same DB / JWT / config the web service needs)
  volumes:
    - ./config:/app/config:ro
    - ./logs:/app/logs
  depends_on: [postgres]
  restart: unless-stopped
```

The crontab is baked into the image at `/app/scheduler-crontab`:

```cron
0 7 * * *     dotnet /app/Famick.HomeManagement.Web.dll run-job notifications-daily
*/5 * * * *   dotnet /app/Famick.HomeManagement.Web.dll run-job calendar-reminders
*/15 * * * *  dotnet /app/Famick.HomeManagement.Web.dll run-job external-calendar-sync
```

[supercronic](https://github.com/aptible/supercronic) is a single-binary
cron purpose-built for containers: it doesn't fork, logs every job
invocation to stdout (which Docker captures), and respects PID 1
signals. It's installed in the runtime stage of the [Dockerfile](../Dockerfile).

Adding a new job:

1. Implement `IJob` and register it as keyed-scoped under a job key.
2. Add a line to `docker/scheduler-crontab`.
3. Rebuild the image.

### Manual one-off invocation

Run any job ad-hoc against an existing deployment:

```sh
docker exec -it homemanagement-web \
    dotnet /app/Famick.HomeManagement.Web.dll run-job calendar-reminders
```

The distributed lock prevents this from clashing with a scheduled run.

---

## Cloud scheduling

Cloud uses **Amazon EventBridge Scheduler → ECS RunTask** against the
existing ECS cluster, with a dedicated `aws_ecs_task_definition` per
job. Every task definition pulls the same image as cloud-app from ECR
and bakes the command in:

```hcl
container_definitions = [{
  name    = "job"
  image   = "${ecr_repository_url}:${ecr_image_tag}"
  command = ["dotnet", "Famick.HomeManagement.Web.dll", "run-job", each.key]
  # environment + secrets mirror cloud-app
}]
```

Per-job EventBridge schedules then point at the per-job task definition
on a cron expression. Implementation lives in the [`scheduled-jobs`
terraform module](../homemanagement-cloud/infrastructure/terraform/modules/scheduled-jobs/),
documented in detail in
[homemanagement-cloud/docs/scheduled-jobs.md](../homemanagement-cloud/docs/scheduled-jobs.md).

Logs land in the shared `/<project>/<env>/jobs` CloudWatch log group, one
log stream prefix per job-key.

---

## Inventory

| Job key                  | Project       | Schedule (default)         | Lock TTL |
| ------------------------ | ------------- | -------------------------- | -------- |
| `notifications-daily`    | Jobs          | `0 7 * * *` (07:00 UTC)    | 1h       |
| `calendar-reminders`     | Jobs          | `*/5 * * * *`              | 10m      |
| `external-calendar-sync` | Jobs          | `*/15 * * * *`             | 30m      |
| `verify-addresses`       | Cloud.Jobs    | `0 9 * * *` (09:00 UTC) †  | TBD      |

† Cloud-only; not scheduled on self-hosted (the crontab omits it).

---

## Failure semantics

| Outcome                      | Exit | Self-hosted (supercronic) | AWS (EventBridge → ECS) |
| ---------------------------- | ---- | ------------------------- | ----------------------- |
| Success                      | 0    | Logged, container exits   | Task succeeds           |
| Lock held by another run     | 0    | Same as success           | Same as success         |
| Job threw                    | 1    | Logged as error           | Task marked failed; CloudWatch records exit code |
| Unknown job key              | 64   | Misconfigured cron        | Misconfigured terraform |
| Cancelled mid-run            | 130  | Container received SIGINT | ECS stopped the task    |

EventBridge is configured with `maximum_retry_attempts = 0` so transient
job failures are not retried automatically — Redis lock idempotency is
designed for *concurrent* fires (e.g. operator manually invokes during
a scheduled run), not unreliable schedulers. If you need retry-on-failure,
either bump retry attempts on the schedule or build retry into the job
itself.

---

## Testing

Unit tests live alongside the rest of the suite, in
[tests/Famick.HomeManagement.Tests.Unit/Jobs/](../tests/Famick.HomeManagement.Tests.Unit/Jobs/):

- **`JobRunnerTests`** — exit codes for success/failure/unknown-key/cancellation.
- **`JobLockSkipTests`** — strict-mock `IServiceScopeFactory` proves each
  job creates **zero** scopes when the lock is already held (the critical
  idempotency invariant).
- **`JobHappyPathTests`** — InMemory EF Core proves the lock is acquired
  and disposed cleanly with zero or N tenants, plus an error-isolation
  test that one throwing tenant doesn't kill the run.

Run them with:

```sh
dotnet test tests/Famick.HomeManagement.Tests.Unit \
    --filter "FullyQualifiedName~Jobs"
```

---

## Adding a new job

1. **Implement `IJob`** in either `src/Famick.HomeManagement.Jobs/` (shared
   between self-hosted + cloud) or `homemanagement-cloud/src/Famick.HomeManagement.Cloud.Jobs/`
   (cloud-only). Inside `RunJob`, acquire `IDistributedLockService.TryAcquireLockAsync`
   and use `IServiceScopeFactory` for any per-tenant work.
2. **Register** it as `services.AddKeyedScoped<IJob, MyNewJob>("my-new-job")`
   in the appropriate `StartupExtensions.cs`.
3. **Schedule** it:
   - Self-hosted: add a line to `docker/scheduler-crontab`.
   - Cloud: add an entry to the `var.jobs` map in
     [scheduled-jobs/variables.tf](../homemanagement-cloud/infrastructure/terraform/modules/scheduled-jobs/variables.tf)
     defaults (or override per-env in `environments/{staging,prod}/main.tf`).
4. **Test** — at minimum, a lock-skip test mirroring `JobLockSkipTests`
   to prove the idempotency guard is wired up.

---

## Related

- [`IDistributedLockService`](../src/Famick.HomeManagement.Core/Interfaces/IDistributedLockService.cs)
  — the idempotency primitive.
- [`docs/architecture.md`](architecture.md) — overall system layering.
- [`homemanagement-cloud/docs/scheduled-jobs.md`](../homemanagement-cloud/docs/scheduled-jobs.md)
  — AWS scheduling internals (terraform module, IAM, EventBridge details).
- [`Dockerfile`](../Dockerfile) — supercronic install + crontab COPY.
- [`docker-compose.yml`](../docker-compose.yml) — self-hosted scheduler service.
