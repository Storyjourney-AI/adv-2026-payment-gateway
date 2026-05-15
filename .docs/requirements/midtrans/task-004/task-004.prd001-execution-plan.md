## Execution Plan — task-004: Payment Status Check & Cancel Endpoints

### Checklist

- [x] Task 1 — Create `Dto_SnapStatusResponse.cs`
- [x] Task 2 — Add Midtrans API URL constants to `SnapController.cs`
- [x] Task 3 — Add `GetPaymentStatus` endpoint (`GET /api/snap/status/{orderId}`)
- [x] Task 4 — Add `CancelPayment` endpoint (`POST /api/snap/cancel/{orderId}`)
- [x] Task 5 — Add private helper `BuildStatusResponseFromMidtrans`
- [x] Task 6 — Build validation (`dotnet build`)

---

## Task 1 — Create Response DTO

* Target File: **NEW** `PaymentGateway.Server/Midtrans/Models/Dtos/Dto_SnapStatusResponse.cs`
  - Define `Dto_SnapStatusResponse` with fields: `CallerOrderId`, `MidtransOrderId`, `GatewayStatus`, `MidtransStatus`, `FraudStatus`, `GrossAmount`, `MidtransTransactionId`, `PaymentType`, `CreatedAt`, `UpdatedAt`.
  - Namespace: `PaymentGateway.Server.Midtrans.Models.Dtos`
  - **Feasibility**: ✅ High — DTOs already exist at this path; pattern is direct copy of `Dto_SnapTokenResponse.cs` style.

---

## Task 2 — Add API URL Constants

* Target File: **EXISTING** `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - Add two `private const string` entries immediately below the existing `MidtransSandboxUrl` / `MidtransProductionUrl` constants (lines 23–24):
    ```csharp
    private const string MidtransSandboxApiUrl = "https://api.sandbox.midtrans.com/v2";
    private const string MidtransProductionApiUrl = "https://api.midtrans.com/v2";
    ```
  - **Feasibility**: ✅ High — Constants section is clearly defined and unambiguous.

---

## Task 3 — Implement `GetPaymentStatus` Endpoint

* Target File: **EXISTING** `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - Add after `TestPurchase` action (before `Callback`).
  - Route: `[HttpGet("status/{orderId}")]`, `[AllowAnonymous]`
  - Logic:
    1. Validate `X-Api-Key` → `401` if missing/invalid
    2. Look up `Db_SnapTransaction` WHERE `(EnvironmentId, CallerOrderId)` → `404` if not found
    3. Resolve base URL and server key from `environment.IsSandbox`
    4. Auth: `Basic {Base64(serverKey + ":")}`
    5. `GET {baseUrl}/{MidtransOrderId}/status`
    6. On 2xx → return `200 DataWrapper<Dto_SnapStatusResponse>` via `BuildStatusResponseFromMidtrans`
    7. On non-2xx → return `502` with Midtrans error message
    8. On exception → return `502`
  - **Feasibility**: ✅ High — Follows identical HTTP client pattern as `CreateTokenAsync`.

---

## Task 4 — Implement `CancelPayment` Endpoint

* Target File: **EXISTING** `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - Add after `GetPaymentStatus` action.
  - Route: `[HttpPost("cancel/{orderId}")]`, `[AllowAnonymous]`
  - Logic:
    1. Same auth/lookup as `GetPaymentStatus`
    2. `POST {baseUrl}/{MidtransOrderId}/cancel` (no request body)
    3. On 2xx → update `TransactionStatus` from response + `UpdatedAt = UtcNow` → save → return `200`
    4. On Midtrans 4xx → return `422` with Midtrans error message
    5. On non-4xx non-2xx or exception → return `502`
  - **Feasibility**: ✅ High — Same client factory, same auth pattern.

---

## Task 5 — Add `BuildStatusResponseFromMidtrans` Helper

* Target File: **EXISTING** `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - Add private method in the same class, after `HandleCallbackRedirectAsync`.
  - Signature: `private static Dto_SnapStatusResponse BuildStatusResponseFromMidtrans(JsonDocument midtransDoc, Db_SnapTransaction tx)`
  - Reads: `transaction_status`, `fraud_status`, `gross_amount`, `transaction_id`, `payment_type` from `midtransDoc.RootElement` using `TryGetProperty`.
  - Returns: populated `Dto_SnapStatusResponse`.
  - **Feasibility**: ✅ High — `JsonDocument`/`JsonElement` API already used in `CreateTokenAsync` (line 416).

---

## Task 6 — Build Validation

* Command: `cd PaymentGateway.Server && dotnet build`
  - Verify 0 errors, 0 new warnings introduced.
  - **Feasibility**: ✅ High — No new dependencies; only additive changes to existing files.

---

## Infrastructure Rules Cross-Check

| Rule | Status |
|------|--------|
| No new service layer (logic not reused across multiple controllers) | ✅ Logic stays in `SnapController` |
| No DB migration executed | ✅ `TransactionStatus` column already exists |
| `DataWrapper<T>` used for all responses | ✅ All endpoints use `DataWrapper` |
| `AllowAnonymous` on public API key endpoints | ✅ Matches existing token endpoint pattern |
| `IHttpClientFactory` for outbound HTTP | ✅ Reuses `m_httpClientFactory` |
