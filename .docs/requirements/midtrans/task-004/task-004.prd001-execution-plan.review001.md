# Review Report — task-004: Payment Status Check & Cancel Endpoints
**Review ID:** review001  
**Source Documents:** `task-004.prd001.md`, `task-004.prd001-userflow.md`, `task-004.prd001-execution-plan.md`  
**Reviewed Files:**
- `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
- `PaymentGateway.Server/Midtrans/Models/Dtos/Dto_SnapStatusResponse.cs`

---

## Overview

Task-004 adds two new API endpoints to the `SnapController`: `GET /api/snap/status/{orderId}` for on-demand payment status polling, and `POST /api/snap/cancel/{orderId}` for cancelling pending payments. Both authenticate callers via `X-Api-Key` and proxy to the Midtrans API without exposing Midtrans server keys to child applications. A new `Dto_SnapStatusResponse` DTO and a private `BuildStatusResponseFromMidtrans` helper were also introduced.

The implementation is structurally sound and correctly covers the happy path and most error paths. Two issues require remediation before the feature is production-ready.

---

## PRD Validation

### GET /api/snap/status/{orderId}

| # | Acceptance Criterion | Status | Notes |
|---|---------------------|--------|-------|
| 1 | `{orderId}` is `CallerOrderId` (not Midtrans-prefixed) | ✅ Implemented | Query: `t.CallerOrderId == orderId` |
| 2 | Requires `X-Api-Key`; returns `401` if missing | ✅ Implemented | Line 242–247 |
| 3 | Returns `401` if no environment matches the API key | ✅ Implemented | Line 249–255 |
| 4 | Looks up `Db_SnapTransaction` by `(EnvironmentId, CallerOrderId)`; returns `404` if not found | ⚠️ Partially Implemented | Transaction scope is correct but the environment lookup does not filter `!e.IsDeleted`, meaning soft-deleted environments accept their old API keys *(see Security Finding S-1)* |
| 5 | Calls Midtrans `GET /v2/{MidtransOrderId}/status` on correct sandbox/production base URL | ✅ Implemented | Lines 267–269 |
| 6 | Auth header: `Basic {Base64(serverKey + ":")}` | ✅ Implemented | Line 270 |
| 7 | Returns `200` with `DataWrapper<Dto_SnapStatusResponse>` containing all 10 required fields | ✅ Implemented | All fields present: `callerOrderId`, `midtransOrderId`, `gatewayStatus`, `midtransStatus`, `fraudStatus`, `grossAmount`, `midtransTransactionId`, `paymentType`, `createdAt`, `updatedAt` |
| 8 | Returns `502` on non-2xx from Midtrans | ✅ Implemented | Lines 283–299 |
| 9 | Returns `502` on network/exception | ✅ Implemented | Lines 308–314 |

### POST /api/snap/cancel/{orderId}

| # | Acceptance Criterion | Status | Notes |
|---|---------------------|--------|-------|
| 1 | `{orderId}` is `CallerOrderId` | ✅ Implemented | Query: `t.CallerOrderId == orderId` |
| 2 | Requires `X-Api-Key`; returns `401` if missing/invalid | ✅ Implemented | Lines 326–338 |
| 3 | Looks up by `(EnvironmentId, CallerOrderId)`; returns `404` if not found | ⚠️ Partially Implemented | Same `!e.IsDeleted` gap as above |
| 4 | Calls Midtrans `POST /v2/{MidtransOrderId}/cancel` on correct base URL | ✅ Implemented | Lines 351–353 |
| 5 | On `2xx`: updates `TransactionStatus` from response; sets `UpdatedAt = UtcNow`; saves to DB | ✅ Implemented | Lines 396–403; `UpdatedAt` is set unconditionally (correct per PRD) |
| 6 | On success: returns `200` with same status-response shape reflecting post-cancel state | ✅ Implemented | `BuildStatusResponseFromMidtrans` called after DB save, so `GatewayStatus` reflects the updated value |
| 7 | Returns `422` for terminal-state Midtrans errors (e.g. already settled) | ⚠️ Partially Implemented | Maps **all** Midtrans `4xx` → `422`. PRD specifies `422` only for terminal-state business errors. A Midtrans `401` (wrong server key) would incorrectly surface as `422` instead of `502` *(see Finding S-2)* |
| 8 | Returns `502` on non-4xx non-2xx from Midtrans | ✅ Implemented | Lines 388–390 |
| 9 | Returns `502` on exception | ✅ Implemented | Lines 411–415 |
| 10 | DB record **not** modified on Midtrans error | ✅ Implemented | DB save only occurs inside the `IsSuccessStatusCode` branch |

### Security (PRD AC)

| # | Criterion | Status | Notes |
|---|-----------|--------|-------|
| 1 | Tenant isolation: lookup scoped by `(EnvironmentId, CallerOrderId)`; cross-tenant miss = `404` | ✅ Implemented | No cross-tenant information leak possible |
| 2 | No separate `403` ownership check needed when lookup is properly scoped | ✅ Implemented | Matches PRD guidance exactly |

### New Constants

| # | Criterion | Status | Notes |
|---|-----------|--------|-------|
| 1 | `MidtransSandboxApiUrl` and `MidtransProductionApiUrl` added | ✅ Implemented | Lines 25–26 |

### Definition of Done

| # | Item | Status |
|---|------|--------|
| 1 | `GET /api/snap/status/{orderId}` implemented with live Midtrans status | ✅ |
| 2 | `POST /api/snap/cancel/{orderId}` updates DB on success | ✅ |
| 3 | Both endpoints return `401` for missing/invalid key | ✅ |
| 4 | Both endpoints return `404` for unknown `CallerOrderId` | ✅ |
| 5 | Both endpoints return `422` for terminal-state Midtrans rejection | ⚠️ Over-broad (all 4xx → 422) |
| 6 | Both endpoints return `502` on network failures | ✅ |
| 7 | `dotnet build` passes with 0 errors/warnings | ✅ (per completion summary) |

---

## Userflow Validation

### Flow #1 — Poll Payment Status (Happy Path)

| Step | Description | Status |
|------|-------------|--------|
| 1 | Child app holds `CallerOrderId` | ✅ Covered |
| 2 | `GET /api/snap/status/{orderId}` with `X-Api-Key` | ✅ Covered |
| 3 | Gateway validates key → resolves environment (sandbox/production) | ✅ Covered |
| 4 | Gateway looks up `Db_SnapTransaction` by `(EnvironmentId, CallerOrderId)` | ✅ Covered |
| 5 | Gateway selects correct Midtrans base URL and server key | ✅ Covered |
| 6 | Gateway calls `GET {baseUrl}/{MidtransOrderId}/status` | ✅ Covered |
| 7 | Midtrans returns `200` with live state | ✅ Covered |
| 8 | Gateway merges and returns `DataWrapper` with all 10 fields | ✅ Covered |

### Flow #2 — Poll Payment Status (Error Paths)

| Scenario | Expected | Status |
|----------|----------|--------|
| Missing `X-Api-Key` | `401` | ✅ Covered |
| Invalid `X-Api-Key` | `401` | ✅ Covered |
| `orderId` not found | `404` | ✅ Covered |
| Midtrans non-2xx | `502` with Midtrans message | ✅ Covered |
| Network failure | `502` | ✅ Covered |

### Flow #3 — Cancel a Pending Payment (Happy Path)

| Step | Description | Status |
|------|-------------|--------|
| 1–4 | Auth, environment resolution, transaction lookup | ✅ Covered |
| 5 | `POST {baseUrl}/{MidtransOrderId}/cancel` | ✅ Covered |
| 6 | Midtrans returns `200` with `transaction_status: "cancel"` | ✅ Covered |
| 7 | DB updated: `TransactionStatus = "cancel"`, `UpdatedAt = UtcNow`, saved | ✅ Covered |
| 8 | Response reflects post-cancel state | ✅ Covered — helper called after DB save |

### Flow #4 — Cancel a Non-Cancellable Payment

| Step | Description | Status |
|------|-------------|--------|
| 1–4 | Auth + transaction lookup | ✅ Covered |
| 5 | Midtrans returns `4xx` (e.g. 412) | ✅ Covered |
| 6 | Gateway returns `422` with Midtrans error message | ✅ Covered |
| 7 | DB record NOT modified | ✅ Covered |

### Flow #5 — Cancel with Network Failure

| Step | Description | Status |
|------|-------------|--------|
| 1 | Midtrans call throws exception | ✅ Covered |
| 2 | Gateway returns `502` | ✅ Covered |
| 3 | DB record NOT modified | ✅ Covered |

---

## Security Findings

### S-1 — Soft-Deleted Environments Remain Authenticated
**Priority:** 🟠 High  
**Location:** `SnapController.cs` — `GetPaymentStatus` (line 249), `CancelPayment` (line 333), and pre-existing `CreateToken` (line 70), `CreateSandboxToken` (line 107), `CreateProductionToken` (line 144)  
**Issue:** The environment lookup in all API-key-authenticated endpoints omits the `!e.IsDeleted` soft-delete filter:
```csharp
// Current (all API-key endpoints):
var environment = await m_dbContext.Environments
    .FirstOrDefaultAsync(e => e.ApiKey == apiKey);

// TestPurchase correctly uses:
.FirstOrDefaultAsync(e => e.Id == environmentId && !e.IsDeleted && ...)
```
A deleted environment's API key remains valid, allowing a revoked tenant to continue querying statuses or cancelling payments. This violates the tenant lifecycle assumption — deletion of an environment should immediately invalidate its API key.  
**Suggestion:** Add `&& !e.IsDeleted` to all environment lookups in the controller. Since `Db_Environment` implements `ISoftDelete`, a global query filter on `AppDbContext` for `ISoftDelete` entities would be a cleaner long-term solution to prevent this class of bug across the codebase.

---

### S-2 — Over-Broad 4xx → 422 Mapping in `CancelPayment`
**Priority:** 🟡 Medium  
**Location:** `SnapController.cs` — `CancelPayment`, lines 382–386  
**Issue:** Any Midtrans `4xx` response is mapped to `422 Unprocessable Entity`:
```csharp
if ((int)httpResponse.StatusCode >= 400 && (int)httpResponse.StatusCode < 500)
    return UnprocessableEntity(...);
```
The PRD defines `422` specifically for **terminal-state business errors** (Midtrans rejects cancel because the transaction is already settled/expired/cancelled — typically a `412 Precondition Failed`). Other `4xx` codes signal different problems:
- **`401`** from Midtrans = gateway's server key is misconfigured → this should be **`502`** (infrastructure failure on our side, not the child app's fault)
- **`404`** from Midtrans = `MidtransOrderId` not found at Midtrans (data inconsistency) → arguably **`502`**
- **`412`** from Midtrans = terminal state, cannot cancel → correctly **`422`**

Returning `422` for a `401` from Midtrans causes a child app to believe the transaction is in a terminal state and to skip retrying or escalating, when the real problem is a misconfigured server key.  
**Suggestion:** Narrow the 422 branch to the specific Midtrans status codes that indicate a business-logic rejection, and route other 4xx codes to 502:
```csharp
// Midtrans 412 = terminal state, not cancellable → 422
// Other 4xx (401, 404, 400) = gateway-side problem → 502
if ((int)httpResponse.StatusCode == 412)
{
    return UnprocessableEntity(DataWrapper<object>.Unprocessable(
        message: midtransMessage ?? "Transaction cannot be cancelled in its current state."));
}
return StatusCode(502, DataWrapper<object>.Fail(
    System.Net.HttpStatusCode.BadGateway,
    message: midtransMessage ?? "Midtrans cancel API returned an error."));
```
Alternatively, if matching on the Midtrans `status_code` field in the response body (e.g. `"412"`), that is also a valid approach since Midtrans includes it in error payloads.

---

### S-3 — Midtrans Response Body Logged on Errors
**Priority:** 🟢 Low  
**Location:** `SnapController.cs` — `GetPaymentStatus` line 285, `CancelPayment` line 370  
**Issue:** Full Midtrans response bodies are logged at `Warning` level on non-2xx responses. In some error cases (e.g. auth errors or partial successes), Midtrans may echo back request-scoped data including card masking info, amounts, or order metadata.  
**Suggestion:** Log only the `status_code` and `status_message` fields rather than the full raw `responseBody`. The structured log is already constructed; reuse `midtransMessage` after parsing.

---

### S-4 — No `IsEnabled` Guard on New Endpoints
**Priority:** 🟢 Low  
**Location:** `SnapController.cs` — `GetPaymentStatus`, `CancelPayment`  
**Issue:** The new endpoints do not check `envOptions.IsEnabled`. This is likely intentional — operators should still be able to query or cancel transactions on a disabled environment. However, this differs from `CreateTokenAsync` which enforces the flag.  
**Suggestion:** Document the intentional absence of the `IsEnabled` check in code comments on both methods to prevent future maintainers from inadvertently adding it or questioning the omission.

---

## UI/UX Findings

> These endpoints are backend-only (no frontend component). The "user" persona is the **Child Application Developer** consuming the API. Findings are from that perspective.

### UX-1 — `GrossAmount` Returns Empty String When Missing
**Persona:** Child Application Developer  
**Priority:** 🟡 Minor  
**Location:** `Dto_SnapStatusResponse.cs` — `GrossAmount` field; `BuildStatusResponseFromMidtrans`, line 650  
**Issue:** If `gross_amount` is absent from the Midtrans response, `GrossAmount` silently defaults to `""`. The DTO defines `GrossAmount` as non-nullable `string` (initialized to `string.Empty`), so a consumer cannot distinguish "Midtrans returned no gross_amount" from "gross_amount is legitimately empty". Midtrans does not guarantee field presence in all error or edge-case responses.  
**Suggestion:** Change `GrossAmount` to `string?` (nullable) in `Dto_SnapStatusResponse.cs` so that `null` signals a missing value and `""` can be reserved for an explicit empty string from Midtrans:
```csharp
public string? GrossAmount { get; set; }
```
Update `BuildStatusResponseFromMidtrans` accordingly (drop the `?? string.Empty` fallback).

---

### UX-2 — 404 Error Message Leaks the orderId
**Persona:** Child Application Developer  
**Priority:** 🟢 Enhancement  
**Location:** `SnapController.cs` — `GetPaymentStatus` line 262, `CancelPayment` line 346  
**Issue:** The `404` message includes the submitted `orderId` (`$"Transaction with orderId '{orderId}' not found."`). While helpful in dev/debug, in production this confirms to a potential enumerator that a given `orderId` does not exist under the presented API key (as opposed to the API key itself being the problem).  
**Suggestion:** For production builds, consider a generic message such as `"Transaction not found."` without echoing the caller-supplied value, or keep the current message but ensure it is only logged server-side.

---

### UX-3 — No `Content-Type` Enforcement on Cancel POST
**Persona:** Child Application Developer  
**Priority:** 🟢 Enhancement  
**Location:** `SnapController.cs` — `CancelPayment`  
**Issue:** `POST /api/snap/cancel/{orderId}` is documented as requiring no request body. However, there is no explicit documentation (XML doc comments or Swagger annotation) saying so. A developer might assume a body is required.  
**Suggestion:** Add an XML summary comment stating "No request body is required" and consider adding a `[Consumes]` attribute noting this, or at minimum ensure the Swagger/OpenAPI output reflects the intent.

---

## Overall Verdict

### ⚠️ Pass with Conditions

The core implementation is correct, well-structured, and follows existing controller patterns. All happy paths and most error paths work as specified. The following two conditions **must** be resolved before production deployment:

| # | Condition | Priority | File | Line |
|---|-----------|----------|------|------|
| C-1 | Add `&& !e.IsDeleted` to environment lookup in `GetPaymentStatus` and `CancelPayment` (and fix consistently across all existing API-key endpoints in this controller) | 🟠 High | `SnapController.cs` | 249, 333 (also 70, 107, 144) |
| C-2 | Narrow the Midtrans `4xx → 422` mapping in `CancelPayment` to only terminal-state codes (e.g. `412`); route Midtrans `401` and other unexpected `4xx` to `502` | 🟡 Medium | `SnapController.cs` | 382–386 |

The remaining findings (S-3, S-4, UX-1–3) are low-priority improvements that can be addressed in a follow-up task.
