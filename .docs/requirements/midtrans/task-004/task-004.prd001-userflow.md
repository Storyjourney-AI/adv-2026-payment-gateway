# User Flow — task-004: Payment Status Check & Cancel Endpoints

## Use Case

Child applications need to query live payment status and cancel pending payments through the gateway **without holding Midtrans credentials**. The gateway acts as a secure proxy — it authenticates the child app via `X-Api-Key`, resolves the Midtrans credentials internally, calls the Midtrans API, and returns the result in the gateway's standard response envelope.

---

## User Levels (Action × Role)

| Action / Capability                            | Child App (API Key) | Gateway Admin (JWT) | Unauthenticated |
|------------------------------------------------|:-------------------:|:-------------------:|:---------------:|
| Query live payment status by `orderId`         | ✅                  | ❌                  | ❌              |
| Cancel a pending payment by `orderId`          | ✅                  | ❌                  | ❌              |
| See another tenant's transaction               | ❌                  | ❌                  | ❌              |
| Cancel a settled/expired/already-cancelled tx  | ❌ (422)            | ❌ (422)            | ❌              |

> **Child App** = any system holding a valid `X-Api-Key` issued by the gateway.  
> Scope is always enforced by the API key → only transactions belonging to the resolved environment are accessible.

---

## User Flows

### Flow #1 — Poll Payment Status (Happy Path)
**As a Child Application**

1. Child app holds an `orderId` (their own `CallerOrderId`, e.g. `"ORD-20240601-001"`).
2. Child app calls:
   ```
   GET /api/snap/status/{orderId}
   X-Api-Key: <their-api-key>
   ```
3. Gateway validates `X-Api-Key` → resolves the environment (sandbox or production).
4. Gateway looks up `Db_SnapTransaction` by `(EnvironmentId, CallerOrderId)`.
5. Gateway determines the correct Midtrans base URL and server key from the environment.
6. Gateway calls: `GET https://api[.sandbox].midtrans.com/v2/{MidtransOrderId}/status`
7. Midtrans returns `200` with live payment state.
8. Gateway merges live Midtrans fields with stored DB fields and returns:
   ```json
   {
     "success": true,
     "code": 200,
     "message": "Payment status retrieved successfully.",
     "data": {
       "callerOrderId": "ORD-20240601-001",
       "midtransOrderId": "abc12345_ORD-20240601-001",
       "gatewayStatus": "pending",
       "midtransStatus": "pending",
       "fraudStatus": "accept",
       "grossAmount": "30000.00",
       "midtransTransactionId": "txn-xyz-123",
       "paymentType": "bank_transfer",
       "createdAt": "2024-06-01T10:00:00Z",
       "updatedAt": "2024-06-01T10:05:00Z"
     }
   }
   ```

---

### Flow #2 — Poll Payment Status (Error Paths)

| Scenario | What Happens | Response |
|----------|-------------|---------|
| Missing `X-Api-Key` header | Gateway rejects immediately | `401 Unauthorized` |
| Invalid `X-Api-Key` (no matching env) | Gateway rejects | `401 Unauthorized` |
| `orderId` not found for this environment | Gateway returns | `404 Not Found` |
| Midtrans API returns non-2xx | Gateway returns Midtrans error | `502 Bad Gateway` |
| Network failure reaching Midtrans | Gateway catches exception | `502 Bad Gateway` |

---

### Flow #3 — Cancel a Pending Payment (Happy Path)
**As a Child Application**

1. Child app has a `pending` payment they wish to cancel (e.g. user abandoned checkout).
2. Child app calls:
   ```
   POST /api/snap/cancel/{orderId}
   X-Api-Key: <their-api-key>
   ```
   *(No request body required)*
3. Gateway validates `X-Api-Key` → resolves the environment.
4. Gateway looks up `Db_SnapTransaction` by `(EnvironmentId, CallerOrderId)`.
5. Gateway calls: `POST https://api[.sandbox].midtrans.com/v2/{MidtransOrderId}/cancel`
6. Midtrans returns `200` with `transaction_status: "cancel"`.
7. Gateway updates `Db_SnapTransaction.TransactionStatus = "cancel"` and `UpdatedAt = DateTime.UtcNow`, saves to DB.
8. Gateway returns `200` with the same status-response shape reflecting the post-cancel state.

---

### Flow #4 — Cancel a Non-Cancellable Payment
**As a Child Application (trying to cancel a settled transaction)**

1. Child app calls `POST /api/snap/cancel/{orderId}` for an order that is already `settlement`.
2. Gateway validates auth and resolves the transaction (steps 3–4 above).
3. Gateway calls Midtrans cancel API.
4. Midtrans returns `4xx` (e.g. `412`) with error message "Transaction status is not allow to be cancel".
5. Gateway returns `422 Unprocessable Entity` with the Midtrans error message.
6. **DB record is NOT modified** — the status remains `settlement`.

---

### Flow #5 — Cancel with Network Failure

1. Gateway calls Midtrans cancel API but the request times out / throws an exception.
2. Gateway catches the exception and returns `502 Bad Gateway`.
3. **DB record is NOT modified.**

---

## Key Rules / Constraints

1. **Scope isolation**: All lookups are `WHERE EnvironmentId = resolvedEnv.Id AND CallerOrderId = {orderId}`. A miss is always `404` — there is no cross-tenant information leak.
2. **No direct Midtrans credentials in child apps**: Child apps never see server keys; the gateway proxies all Midtrans API calls.
3. **DB update only on confirmed success**: `TransactionStatus` in `Db_SnapTransaction` is updated only after Midtrans returns a successful `2xx` cancel response.
4. **Correct base URL selection**: Sandbox environments use `https://api.sandbox.midtrans.com/v2`; production environments use `https://api.midtrans.com/v2`.
5. **`422` vs `502` distinction**: Midtrans-level business logic errors (terminal state) → `422`. Network/infrastructure errors → `502`.
6. **No schema changes required**: `TransactionStatus` column already exists as nullable `text`.

---

## Page / Endpoint Mapping

| Flow | Existing / New | Route | File |
|------|---------------|-------|------|
| Status check | **NEW** | `GET /api/snap/status/{orderId}` | `SnapController.cs` (EXISTING) |
| Cancel payment | **NEW** | `POST /api/snap/cancel/{orderId}` | `SnapController.cs` (EXISTING) |
| Response DTO | **NEW** | — | `Dto_SnapStatusResponse.cs` (NEW) |
| Auth pattern | EXISTING | — | `SnapController.cs` (reuse) |
