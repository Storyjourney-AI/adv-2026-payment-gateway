# Review Report — task-001: Midtrans Snap Integration

**Reviewer:** GitHub Copilot (c-reviewer)
**Review Date:** 2026-03-11
**Source Document:** `task-001-execution-plan.md` + `task-001.md`
**Persona Assumed:** Gateway Operator / Backend Integrator (no end-user frontend; child apps are the consumers)

---

## Overview

Task-001 adds Midtrans Snap integration to the Payment Gateway. The gateway acts as a secure intermediary — child apps authenticate via `X-Api-Key` and never hold Midtrans server keys. All six planned tasks were delivered and the EF migration (`add-snap-transactions`) has been applied to the database.

---

## PRD Validation

| # | Requirement | Status | Notes |
|---|---|---|---|
| 1 | Read Sandbox + Production Midtrans API keys from appsettings | ✅ Implemented | `Midtrans.Sandbox.ServerKey` and `Midtrans.Production.ServerKey` in `appsettings.development.json` |
| 2 | `enable_sandbox` and `enable_production` flags in appsettings | ✅ Implemented | `IsEnabled` boolean per environment in `MidtransOptions`; controls whether the endpoint is active |
| 3 | Staging (sandbox) endpoint for child app development/testing; Production endpoint for real transactions | ✅ Implemented | `POST api/snap/sandbox/token` and `POST api/snap/production/token` are separate and independently gated |
| 4 | Webhook forwarding to registered callback URL per child app after Snap payment | ✅ Implemented | `WebhookController` reads `Db_Environment.WebhookUrl` and forwards raw Midtrans payload |

---

## Userflow Validation

Defined flow: **Child app → Gateway → Midtrans → Webhook back to child app**

| Step | Flow | Status | Notes |
|---|---|---|---|
| 1 | Child app `POST api/snap/sandbox/token` with `X-Api-Key` header | ✅ Covered | `[AllowAnonymous]` + `X-Api-Key` lookup against `Db_Environment.ApiKey` |
| 2 | Gateway validates request and looks up environment | ✅ Covered | `IsEnabled` check → auth check → `ModelState.IsValid` check |
| 3 | Gateway calls Midtrans Snap API with `Authorization: Basic Base64(ServerKey + ":")` | ✅ Covered | `IHttpClientFactory` client with correct `Authorization` header |
| 4 | Gateway sends `X-Override-Notification` to route Midtrans callback | ✅ Covered | Header set to `{scheme}://{host}/api/webhook/midtrans/{env}` |
| 5 | Gateway stores transaction log (`Db_SnapTransaction`) with `"pending"` status | ✅ Covered | Inserted before calling Midtrans; `MidtransOrderId` is prefixed with 8-char env Guid |
| 6 | Returns `{ token, redirect_url }` to child app | ✅ Covered | `DataWrapper<Dto_SnapTokenResponse>.Succeed(...)` |
| 7 | Midtrans POSTs notification to `api/webhook/midtrans/sandbox` | ✅ Covered | `WebhookController` receives POST |
| 8 | Gateway verifies SHA-512 signature | ✅ Covered | `MidtransSignatureHelper.Verify(...)` called before any DB write |
| 9 | Gateway updates `Db_SnapTransaction.TransactionStatus` | ✅ Covered | Status and `MidtransTransactionId` updated, `UpdatedAt` refreshed |
| 10 | Gateway forwards raw payload to `Db_Environment.WebhookUrl` | ✅ Covered | Forwarded via `IHttpClientFactory`; no forwarding if `WebhookUrl` is null |
| 11 | Gateway always returns `200 OK` to Midtrans | ✅ Covered | All webhook paths return `Ok()` at the end |

---

## Security Findings

### 🟠 High — SSRF via `WebhookUrl` Forwarding

**Location:** `PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs`, method `HandleWebhookAsync` (step 7)

**Issue:** The `webhookUrl` value is read directly from `Db_Environment.WebhookUrl` (a user/admin-supplied value) and used as the target for an outbound HTTP POST without any URL validation. An operator who creates or modifies an environment record can set `WebhookUrl` to an internal service (e.g., `http://localhost:5000/admin`, `http://169.254.169.254/latest/meta-data/`), causing the gateway to act as an SSRF proxy — making requests to internal infrastructure on every Midtrans notification.

**Suggestion:** Validate `webhookUrl` before forwarding:
- Ensure the URL scheme is `https` (or `http` only in development).
- Ensure the host is not a loopback address (`localhost`, `127.0.0.1`, `::1`), link-local range (`169.254.x.x`), private ranges (`10.x.x.x`, `172.16-31.x.x`, `192.168.x.x`), or metadata endpoint.
- Alternatively, enforce that `WebhookUrl` is validated at environment creation/update time in `EnvironmentController`.

---

### 🟠 High — `X-Override-Notification` URL Derived from Request Host

**Location:** `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`, methods `CreateSandboxToken` / `CreateProductionToken`

**Issue:** The Midtrans callback URL is dynamically built from `Request.Scheme` and `Request.Host`. When deployed behind a reverse proxy (Nginx, load balancer), `Request.Host` may resolve to the internal hostname/IP that Midtrans cannot reach, causing all webhooks to be lost silently. This is not a security issue per se, but causes a silent functional failure in production that would be hard to diagnose.

**Suggestion:** Add a `BaseUrl` (or `PublicUrl`) setting to `appsettings` and use it to build the callback URL:
```json
"Midtrans": {
  "BaseUrl": "https://payment-gateway.example.com",
  ...
}
```
Add `BaseUrl` to `MidtransOptions` and use `$"{options.BaseUrl}/api/webhook/midtrans/{env}"`.

---

### 🟡 Medium — Unhandled Duplicate `MidtransOrderId` on Concurrent Retry

**Location:** `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`, `CreateTokenAsync` step 5

**Issue:** The `MidtransOrderId` (`{envId[0..8]}_{callerOrderId}`) has a unique database index. If a child app retries a failed request with the same `OrderId`, or two concurrent requests arrive simultaneously, the second `SaveChangesAsync()` will throw an unhandled `DbUpdateException` (unique constraint violation), returning a generic 500 to the client.

**Suggestion:** Before inserting, check if a record with the same `MidtransOrderId` already exists. If it does and the status is `"pending"` or `"error"`, either return a `409 Conflict` with a descriptive message or return the existing transaction for re-use.

```csharp
var existing = await m_dbContext.SnapTransactions
    .FirstOrDefaultAsync(t => t.MidtransOrderId == midtransOrderId);
if (existing != null)
    return Conflict(DataWrapper<object>.Conflict(message: "A transaction with this OrderId already exists."));
```

---

### 🟡 Medium — No HTTP Timeout on Outbound `HttpClient` Calls

**Location:** `SnapController.cs` and `WebhookController.cs` — all `m_httpClientFactory.CreateClient()` usages

**Issue:** Both controllers use the unnamed `IHttpClientFactory` client which has a 100-second default timeout. A slow or unresponsive Midtrans API or child app webhook endpoint will hold an ASP.NET thread for the full duration, which under load can exhaust the thread pool.

**Suggestion:** Register named or typed clients with an explicit short timeout in `Program.cs`:
```csharp
builder.Services.AddHttpClient("midtrans", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("webhook-forward", c => c.Timeout = TimeSpan.FromSeconds(10));
```
Then use `m_httpClientFactory.CreateClient("midtrans")` and `m_httpClientFactory.CreateClient("webhook-forward")`.

---

### 🟡 Medium — Duplicate `AddHttpClient()` Registration

**Location:** `PaymentGateway.Server/Program.cs`, lines 62 and 107

**Issue:** `builder.Services.AddHttpClient()` is called twice. While the second call is a no-op for ASP.NET Core's `IHttpClientFactory`, it indicates that a previous registration was not removed when task-001 added its own comment. This creates code confusion.

**Suggestion:** Remove the duplicate registration (line 107 added by task-001, or consolidate comments into one call).

---

### 🟢 Low — Plaintext API Key in Database

**Location:** `Db_Environment.ApiKey` column; `SnapController.cs` step 2

**Issue:** `ApiKey` is stored and compared as plaintext in the database. If the database is compromised, all API keys are immediately exposed.

**Suggestion:** For a future hardening task, consider storing a SHA-256 hash of the API key and comparing the hash. The cleartext key would only be shown once at creation time (analogous to GitHub PATs). Not breaking for the current threat model assuming the DB is access-controlled.

---

### 🟢 Low — No Rate Limiting on Snap Token Endpoints

**Location:** `POST api/snap/sandbox/token`, `POST api/snap/production/token`

**Issue:** There is no rate limiting on token creation endpoints. A stolen or leaked API key could be used to flood the gateway, incurring Midtrans API rate limit errors and bloating the `SnapTransactions` table with `"error"` records.

**Suggestion:** Apply ASP.NET Core rate limiting middleware (available in .NET 7+) to these endpoints, keyed on the `X-Api-Key` header value.

---

## UI/UX Findings

**Scope Note:** Task-001 is a **pure backend API task**. No frontend pages or components were modified or added, which is consistent with the execution plan that explicitly scopes frontend Snap.js integration to child apps. There is no dashboard UI for Snap transactions at this time.

| # | Persona | Location | Issue | Suggestion | Priority |
|---|---|---|---|---|---|
| 1 | Gateway Operator | Dashboard (none yet) | There is no UI to view or monitor `SnapTransactions` records. Operators must query the DB directly to diagnose payment issues. | Add a read-only Snap Transaction list page to the dashboard as a future task. | 🟡 Minor |
| 2 | Child App Developer | API Contract | The `MidtransOrderId` prefix (`{envId[0..8]}_`) is added by the gateway but there is no documentation in the API response indicating the final Midtrans `order_id` that was used. Child apps receive only `token` and `redirect_url`. | Consider adding `MidtransOrderId` to `Dto_SnapTokenResponse` so child apps can reconcile webhook payloads (which contain the full prefixed ID). | 🟠 Major |
| 3 | Child App Developer | API Contract | The `503 Service Unavailable` response for a disabled environment is consistent and correct. However, there is no machine-readable error code distinguishing "environment disabled" from other 503 conditions. | Add an `errorCode` field to `DataWrapper` responses (e.g., `"MIDTRANS_ENV_DISABLED"`) for programmatic handling by child apps. This is a general API design enhancement, not specific to this task. | 🟢 Enhancement |

---

## Overall Verdict

**✅ Pass with Conditions**

All six planned tasks are fully implemented and structurally correct. The core payment flow — Snap token creation, transaction logging, webhook verification, and callback forwarding — is working as designed. The implementation follows the project's conventions (`DataWrapper<T>`, `IHttpClientFactory`, `IOptions<T>` pattern, schema separation).

**Conditions to address before production deployment:**

| Priority | Finding |
|---|---|
| 🟠 High (must-fix) | SSRF risk via unvalidated `WebhookUrl` forwarding |
| 🟠 High (must-fix) | `X-Override-Notification` URL derived from `Request.Host` — will break behind a reverse proxy |
| 🟡 Medium (should-fix) | Unhandled `DbUpdateException` on duplicate `MidtransOrderId` retries |
| 🟡 Medium (should-fix) | No HTTP timeout configured on outbound `HttpClient` calls |
