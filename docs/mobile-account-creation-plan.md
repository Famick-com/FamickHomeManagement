# Plan: re-enable household + account creation in the mobile app

**Ticket:** [FHM-24](https://famick.atlassian.net/browse/FHM-24) — blocked by [FHM-15](https://famick.atlassian.net/browse/FHM-15)
**Branch:** `feat/mobile-account-creation` (off `feat/fhm-10-shopping-fixes`)
**Status:** Phase 1 done; Phase 3 unblocked; the rest waits on FHM-15

---

## Summary

The switch itself is one line. What sits behind it is not ready, and turning it on
without the rest would take users from a hidden feature to a broken one.

---

## The switch

`OnboardingService.IsBetaMode`:

```csharp
/// Beta mode flag — when true, new household creation is disabled
/// and users must sign in to an existing account.
public static bool IsBetaMode => true;
```

It has exactly one consumer — `WelcomePage.OnAppearing` — which hides the registration
fields and the Next button, and changes the subtitle to "Sign in to get started". The
whole onboarding flow is otherwise intact and reachable: `WelcomePage` →
`EmailVerificationPage` → `CreatePasswordPage`, with `QrScannerPage` alongside.

So the UI comes back by flipping one flag. The work is everything that flow then hits.

---

## What the flow actually calls

`WelcomePage.OnNextClicked` → `StartRegistrationAsync(householdName, email)` →
`POST /api/auth/start-registration`, then verification, then complete-registration.

That is **the same shared registration flow** documented in
`HomeManagement-Cloud/docs/cloud-self-service-registration-plan.md`. Which server the app
is pointed at decides what happens, and neither answer is currently "it works".

### Against Famick Cloud

Blocked twice over, and both are FHM-15's subject:

1. `RegistrationDisabledMiddleware` returns **403** for `start-registration`,
   `complete-registration` and `resend-verification`, because `Registration:Enabled` is
   `false`. Verified against live `app.famick.com`.
2. Even with that flag flipped, `CompleteRegistrationAsync` creates a tenant with no tier
   limits, no trial dates, no KMS key and no seed data — so the household lands on Free
   with no trial.

**This work is therefore gated on [FHM-15](https://famick.atlassian.net/browse/FHM-15).**
Re-enabling the mobile UI before cloud registration works means a signup form that 403s.

### Against a self-hosted server

Worse, because it fails silently.

Self-hosted runs single-tenant: `IsMultiTenantEnabled = false` with a `FixedTenantProvider`
bound to `FixedTenantId` (default `00000000-0000-0000-0000-000000000001`). But
`CompleteRegistrationAsync` does:

```csharp
var tenant = new Tenant { Id = Guid.NewGuid(), Name = verificationToken.HouseholdName };
_context.Tenants.Add(tenant);
```

A brand-new random tenant id, with the user assigned to it — while every query in the app
resolves the *fixed* tenant. The account would be created successfully and then appear
empty forever.

This is precisely the failure mode `Program.cs` already warns about for a related case:

> When the two diverge, new rows accumulate under one tenant while queries read from
> another — no error surfaces until the operator notices empty UI views weeks later.

There is a startup guard for the `FixedTenantId` / `SelfHosted:TenantId` mismatch. There is
no equivalent guard here, because nothing currently reaches this path on self-hosted —
first-run uses the setup flow (`/api/setup`) instead.

**Decided: registration is a cloud-only feature.** Self-hosted is single-tenant — one
household per server — so creating a second one has no coherent meaning there. First-run
stays with the setup flow (`/api/setup`); after that, self-hosted users sign in.

That decision does more than hide a button. Today `/api/auth/start-registration` and
`complete-registration` are **ungated on self-hosted** — `RegistrationDisabledMiddleware`
lives in the cloud repo, so the public server has no equivalent. Anything reaching those
endpoints on a self-hosted install still walks the orphan-tenant path above, whether or not
the mobile app offers a button. A client-side gate hides the entrance; it does not close
it.

So "cloud-only" is worth enforcing on the self-hosted server as well — see Phase 2b.

---

## Two defects found while investigating

Both pre-date this work and both are cheap to fix.

### 1. Mobile never sees the real setup status

The server sends `setupRequired`; the mobile model declares `RequiresSetup`:

```csharp
// Mobile: Models/ApiModels.cs
public class SetupStatusResponse
{
    public bool RequiresSetup { get; set; }      // wire name: requiresSetup
    public bool RequireLegalConsent { get; set; }
}
```

Live wire format from a self-hosted server:

```json
{"platform":"SelfHosted","setupRequired":false,"requireLegalConsent":false}
```

Case-insensitive matching does not save this — the words are in a different order — so
`RequiresSetup` silently binds to nothing and is always `false`. It happens to be harmless
today because nothing reads it (`CreatePasswordPage` only uses `RequireLegalConsent`), but
any platform-aware branching added here would be built on a field that never populates.

### 2. Mobile discards the platform the server reports

The same response carries `"platform":"SelfHosted"`, added by the FHM-8 first-class
`ServerPlatform` detection so clients can adapt without re-deriving config. The mobile
model has no `Platform` property and no reference to `ServerPlatform` anywhere.

It is already on the wire — adding the property is the whole change, and it is what makes
a cloud-only registration UI possible without guessing from the server URL.

---

## Proposed work

### Phase 1 — fix the contract (small, independent, do first)

Correct `SetupStatusResponse` on mobile: rename to `SetupRequired` and add `Platform`.
Worth doing regardless of the rest, since it is a silent-wrong-answer bug today.

### Phase 2 — restore the choice on the welcome screen

**Correction to an earlier draft of this plan.** It proposed detecting the platform from
the server and branching on it. That solves the wrong problem: on the welcome screen there
is no server yet. `ApiSettings.Mode` defaults to `Cloud`, and the QR code is what switches
it to `SelfHosted` — so the app is already pointed at the cloud when this page renders, and
there is nothing to detect. Asking the server which platform it is would only ever answer
"Cloud", because that is the only thing configured at that moment.

**The choice on this page is the platform selection.** It does not need to be inferred:

- **Create an account** — cloud signup, restoring what `IsBetaMode` currently hides.
- **Sign In** — existing account, either deployment.
- **I have a QR Code** — the self-hosted path; scanning switches `ApiSettings.Mode` to
  `SelfHosted` and points the app at that server.
- **What's this?** — a link explaining the difference, pointing at the self-hosted page on
  famick.com, for anyone who does not know which of the above they are.

Phase 1's `Platform` field is still worth having — it tells the app what it connected *to*
after the fact, which is what Phase 2b and any later cloud-only UI need. It just is not the
mechanism for this screen.

Note `ServerMode` has three values, not two: `Proxied` covers self-hosted households
reached through auth.famick.com. Any branching on "cloud versus self-hosted" needs to say
which side `Proxied` falls on.

### Phase 2b — enforce cloud-only on the server

Since registration is cloud-only by decision, the self-hosted server should say so rather
than relying on the client not to ask. Reject `start-registration` /
`complete-registration` on self-hosted — the natural place is alongside the existing
single-tenant guards in `Program.cs`, which already refuse to start on a related tenant
misconfiguration.

Without this, the orphan-tenant path stays reachable by any client, old app builds
included — and it fails silently, which is the worst kind.

### Phase 3 — verify the flow end to end

Against cloud once FHM-15 lands: registration completes, the household gets tier limits, a
trial and seed data, and the user can sign in immediately. Against self-hosted: first-run
still works through setup, registration is not offered in the app, and calling the endpoint
directly is refused rather than silently creating an unreachable household.

### Phase 4 — ship

Removing `IsBetaMode` is a user-visible change to the store builds, so it wants a release
note and a deliberate TestFlight/Play Store rollout rather than riding along silently.

---

## Recommendation

Do **Phase 1 now** — it is a real bug, isolated, and unblocks everything else.

Hold Phases 2–4 until FHM-15 has at least reached the point where cloud registration
returns something other than 403. Flipping `IsBetaMode` before then converts a deliberately
hidden feature into a visibly broken one, which is a worse position than today.

---

## Note on the branch

`feat/mobile-account-creation` is branched from `feat/fhm-10-shopping-fixes`, which is open
as a pull request and not yet merged. Until FHM-10 lands, this branch carries its commits
and any PR from it will show them. If that is awkward, rebase onto `main` once FHM-10
merges.
