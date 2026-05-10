# Step-up authentication

Famick's API protects a small set of high-impact actions with **step-up authentication**: even when a user is logged in, the server requires that their authentication is **recent** (within 5 minutes by default) before letting these actions through.

The purpose is defense-in-depth. A stolen or replayed access token is normally valid for an hour. Step-up narrows that window for the actions where a stolen-token compromise would do the most damage — credential changes, sign-in-method changes, share-token issuance, and similar.

---

## How it works

### Server side

A request to a step-up-protected endpoint is allowed through only when:

- The access token's `auth_time` claim is within the configured freshness window (default 300 seconds = 5 minutes), AND
- The `step_up_enabled` feature flag is on.

When the check fails, the server returns:

```
HTTP/1.1 403 Forbidden
Content-Type: application/json

{
  "error_message": "Step-up authentication required",
  "code": "STEP_UP_REQUIRED"
}
```

When the feature flag is off (the default during phased rollout), step-up is a no-op and these endpoints behave exactly like any other authenticated endpoint.

### Client side

When a Famick client (web app or mobile app) receives a `403 STEP_UP_REQUIRED`, it presents a re-authentication prompt to the user. The user re-authenticates with their password (or passkey, on web), and the client transparently retries the original request with a fresh access token.

If the user dismisses the prompt, the original `403` surfaces to the calling page and the action does not complete.

---

## Actions requiring step-up

There are 18 protected endpoints, in six categories:

### Account credentials

| Action | Endpoint |
|---|---|
| Change your account password | `POST /api/v1/profile/change-password` |

### Passkey management

| Action | Endpoint |
|---|---|
| Register a new passkey (get challenge) | `POST /api/auth/passkey/register/options` |
| Register a new passkey (verify) | `POST /api/auth/passkey/register/verify` |
| Delete a passkey | `DELETE /api/auth/passkey/credentials/{id}` |
| Rename a passkey | `PUT /api/auth/passkey/credentials/{id}/name` |

### External sign-in methods (Google, Apple, OpenID)

| Action | Endpoint |
|---|---|
| Link a native Apple sign-in to this account | `POST /api/auth/external/apple/native/link` |
| Link a native Google sign-in to this account | `POST /api/auth/external/google/native/link` |
| Start the OAuth flow to link a provider | `POST /api/auth/external/{provider}/link` |
| Finish the OAuth flow to link a provider | `POST /api/auth/external/{provider}/link/verify` |
| Unlink a sign-in provider | `DELETE /api/auth/external/{provider}` |

Note: the *sign-in* paths under `/api/auth/external/{provider}/challenge`, `/callback`, and `/native` are **not** step-up gated — they're the unauthenticated login flow. Step-up only applies to **link** and **unlink**, which mutate an existing account.

### Contact sharing

| Action | Endpoint |
|---|---|
| Share a contact with another user | `POST /api/v1/contacts/{id}/shares` |
| Update share permissions on a contact | `PUT /api/v1/contacts/shares/{shareId}` |
| Revoke a contact share | `DELETE /api/v1/contacts/shares/{shareId}` |

### Recipe sharing

| Action | Endpoint |
|---|---|
| Generate a public share token for a recipe | `POST /api/v1/recipes/{id}/share` |
| Revoke a recipe share token | `DELETE /api/v1/recipes/{id}/share` |

### Calendar feed tokens (ICS)

| Action | Endpoint |
|---|---|
| Create an iCalendar feed token | `POST /api/v1/calendar/feed/tokens` |
| Revoke a calendar feed token | `POST /api/v1/calendar/feed/tokens/{id}/revoke` |
| Delete a calendar feed token | `DELETE /api/v1/calendar/feed/tokens/{id}` |

---

## How to re-authenticate

When step-up is required, the client presents one of two flows:

### Password

Confirm your account password. The server validates it and issues a fresh access token. Your refresh token (the long-lived session) is **not** rotated — you stay signed in everywhere; only the access token gets a fresh `auth_time`.

Endpoint: `POST /api/auth/reauth` with `{ "password": "..." }`.

### Passkey *(web only in Phase 2.5a)*

If you have at least one registered passkey, the web app offers a "Use Passkey" button. Tapping it performs a WebAuthn assertion against your device's biometric / PIN, then submits the result to the server. On success, fresh tokens are issued. **Note**: the passkey path currently issues a new refresh-token family (treats it as a fresh login). A future enhancement will make it preserve the existing family like the password path.

Endpoints: `POST /api/auth/passkey/authenticate/options`, then `POST /api/auth/passkey/authenticate/verify`.

Mobile passkey re-auth is not yet implemented — see [Phase 2.5b](#future-work) below.

---

## Configuration

### Feature flag

`step_up_enabled` (in `FeatureManagement` config section). Default `false`. When off, step-up is a no-op everywhere — the endpoints behave like any other authenticated endpoint.

Override per environment in Terraform (`cloud_app_environment_variables`):

```hcl
"FeatureManagement__step_up_enabled" = "true"
```

### Freshness window

`JwtSettings:StepUpFreshnessSeconds`. Default `300` (5 minutes). Override in `appsettings.json` or via env var `JwtSettings__StepUpFreshnessSeconds=600` for a 10-minute window.

### Per-endpoint override

A future need may want a stricter window on certain endpoints. The `[StepUp]` attribute accepts a `FreshnessSeconds` override:

```csharp
[StepUp(FreshnessSeconds = 60)]   // 1-minute window on this endpoint only
public IActionResult ChangePassword(...)
```

Setting `0` (the default) uses the configured global value.

---

## When step-up is *not* required

The following endpoints are sensitive but **not** step-up gated for specific reasons:

- **Login endpoints** (`/api/auth/login`, `/api/auth/passkey/authenticate/*`, `/api/auth/external/{provider}/challenge|callback|native`) — these establish the session and have no `auth_time` to check against.
- **Refresh endpoint** (`/api/auth/refresh`) — preserves `auth_time` from the prior refresh token; rotating doesn't refresh it.
- **Reauth endpoint itself** (`/api/auth/reauth`) — would create a chicken-and-egg.
- **Password reset via email** (`/api/auth/reset-password`) — anonymous flow with email proof-of-control as the security boundary.
- **Subscription operations** (cloud-only) — currently no step-up gating; will be added in a later phase if needed.

---

## Future work

- **Phase 2.5b — MAUI passkey re-auth**: build iOS `ASAuthorizationController` + Android `CredentialManager` bridges so mobile users can re-authenticate with a passkey instead of typing their password.
- **Refresh-family-preserving passkey reauth**: a dedicated `POST /api/auth/reauth/passkey` endpoint that mirrors the password reauth shape (verifies an assertion against the currently-authenticated user, returns only a new access token, no refresh-token rotation).
- **Subscription-op annotations** (Phase 8): when cloud subscription endpoints (cancel, modify plan, change payment method) are implemented, they'll get `[StepUp]`.

---

## Implementation references

- **Server filter**: [src/Famick.HomeManagement.Web.Shared/Authorization/StepUpFilter.cs](../src/Famick.HomeManagement.Web.Shared/Authorization/StepUpFilter.cs)
- **Server attribute**: [src/Famick.HomeManagement.Web.Shared/Authorization/StepUpAttribute.cs](../src/Famick.HomeManagement.Web.Shared/Authorization/StepUpAttribute.cs)
- **Reauth endpoint**: [src/Famick.HomeManagement.Web.Shared/Controllers/AuthApiController.cs](../src/Famick.HomeManagement.Web.Shared/Controllers/AuthApiController.cs) (`Reauth` action)
- **Web client modal**: [src/Famick.HomeManagement.UI/Components/Authentication/ReauthDialog.razor](../src/Famick.HomeManagement.UI/Components/Authentication/ReauthDialog.razor)
- **Mobile client modal**: [src/Famick.HomeManagement.Mobile/Pages/StepUpReauthPage.xaml](../src/Famick.HomeManagement.Mobile/Pages/StepUpReauthPage.xaml)
