# Home Assistant Add-on

**Status: in progress.** Files in this folder are the Famick side of an HA
Supervisor add-on. The add-on manifest, Dockerfile, and s6 service layout
live in a separate wrapper repo (`Famick-com/famick-home-assistant-addon`)
which `FROM`s the public Famick image and adds Postgres + s6 on top.

## What lives here

- `bootstrap.sh` — Famick-app-level first-boot script the wrapper repo
  drops into `/etc/cont-init.d/`. Idempotent. Generates the RSA JWT key,
  per-install tenant UUID, starter `server-config.json`, and plugin
  config seed under `/data/`.

## Healthcheck contract

The wrapper repo's Dockerfile / s6 readiness probe / Supervisor
`healthcheck` field all point at the existing Famick web endpoint:

```http
GET http://localhost:8088/health
```

Behavior:

- **200** — Postgres connection succeeded (the only check registered is
  `AddNpgSql`); the add-on is fully up.
- **503** — Postgres unreachable. Returned during boot (before Postgres
  is ready) and any subsequent DB outage. Supervisor's `start_period`
  semantics treat the boot window as "starting" rather than "unhealthy".
- **No auth required** — `MapHealthChecks` has no `[Authorize]` metadata
  and the JwtBearer scheme skips endpoints without it.
- **Rate-limit whitelisted** — `get:/health` is in
  `IpRateLimiting.EndpointWhitelist` (appsettings.json), so probes at any
  cadence never trip the IP limiter.
- **No `X-Ingress-Path` needed** — Supervisor probes loopback inside
  the container, not through the ingress proxy; the PathBase middleware
  is a no-op without the header.

The body is JSON with `status`, `version`, and per-check details — useful
for `bashio::log.info "$(curl -s localhost:8088/health)"` in the wrapper's
service scripts, but Supervisor only cares about the status code.

The scheduler does *not* expose its own healthcheck (the docker-compose
strategy disables it explicitly because supercronic doesn't bind a port).
Inside the single add-on container the web's `/health` is the sole
readiness signal; "is the scheduler process alive" is left to s6.

## Decisions (see plan for full context)

- **Single container** with bundled Postgres (s6-overlay supervises web +
  Postgres + scheduler).
- **/data** (Supervisor's persistent mount) holds everything: `postgres/`
  data dir, `keys/jwt-rsa.pem`, `config/`, `plugins/`, `uploads/`,
  `dataprotection/`.
- **HA Ingress + SSO** — Supervisor authenticates the HA user at the
  edge and forwards `X-Remote-User-*` headers, which Famick's
  `HaIngressAuthenticationHandler` trusts and resolves to a local user.
- **No app-level TLS** — Supervisor terminates TLS at the ingress edge.

## Until the wrapper repo lands

HA OS users with SSH access can install Docker via the community add-on
store and run the [docker-compose](../docker-compose/README.md) strategy
directly.
