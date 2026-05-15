# [Feature] Payment Status Check & Cancel Endpoints

**Labels:** `feature` `backend` `midtrans` `api`
**Milestone:** task-004
**Priority:** High
**Related:** task-001 (Midtrans Snap Integration), task-002 (Webhook & Environment Setup), task-006 (Order ID + API Key Correlation)

---

## Summary

Expose two new API endpoints authenticated by `X-Api-Key` that allow child applications to (1) query the live status of a payment by their own `orderId` on demand, and (2) request cancellation of a pending payment — all without ever embedding Midtrans server keys in the child application.

---

## Background / Context

### Current State

- Child apps create Snap payment tokens via `POST /api/snap/token` using their `X-Api-Key`.
- Midtrans notifies the gateway via webhook when a payment status changes; the gateway records it in `Db_SnapTransaction.TransactionStatus` and forwards the raw payload to the environment's `WebhookUrl`.
- **There is no way for a child app to query payment status on demand.** If a webhook delivery fails or the app misses an event, the child app has no fallback without its own Midtrans credentials.
- **There is no way for a child app to programmatically cancel a transaction through the gateway.** Child apps would need direct Midtrans access to do so.
- `Db_SnapTransaction` records store both `CallerOrderId` (the ID the child app knows) and `MidtransOrderId` (`{envId[0..8]}_{callerOrderId}` — the Midtrans-facing ID). Child apps only know their own `CallerOrderId`.

### What Needs to Change

1. **New GET endpoint** `GET /api/snap/status/{orderId}` — authenticated by `X-Api-Key`. Resolves the environment from the API key, looks up the transaction by `(EnvironmentId, CallerOrderId)`, calls the Midtrans *Get Status* API, and returns both the gateway's stored status and the live Midtrans status.
2. **New POST endpoint** `POST /api/snap/cancel/{orderId}` — authenticated by `X-Api-Key`. Same resolution flow, then calls the Midtrans *Cancel* API and updates `TransactionStatus` in the DB.
3. Both endpoints must verify that the resolved transaction belongs to the environment identified by the supplied API key — mismatches return `403 Forbidden`.

---

## User Stories

**US-1 — Poll payment status:**
> As a child application developer, I want to query the current payment status of my order at any point in time so that I can reconcile my system even when a webhook delivery is delayed or missed.

**US-2 — Cancel a pending payment:**
> As a child application developer, I want to cancel a pending Midtrans order via the gateway so that I can implement order cancellation flows without embedding Midtrans server keys in my application.

**US-3 — Security boundary:**
> As a gateway operator, I want all status and cancel requests to be scoped to the caller's API key so that one tenant cannot query or cancel another tenant's transactions.

---

## Acceptance Criteria

### GET /api/snap/status/{orderId}

- [ ] `{orderId}` is the child app's `CallerOrderId` (not the Midtrans-prefixed ID).
- [ ] Requires `X-Api-Key` header. Returns `401` with standard error envelope if missing or invalid.
- [ ] Resolves environment from `X-Api-Key`. If no matching environment exists, returns `401`.
- [ ] Looks up `Db_SnapTransaction` by `CallerOrderId` WHERE `EnvironmentId == resolvedEnvironment.Id`. Returns `404` if not found.
- [ ] Calls Midtrans `GET /v2/{MidtransOrderId}/status` using the correct server key and base URL:
  - Sandbox env → `https://api.sandbox.midtrans.com/v2/{id}/status`
  - Production env → `https://api.midtrans.com/v2/{id}/status`
  - Auth header: `Basic {Base64(serverKey:)}`
- [ ] Returns `200` with a `DataWrapper<T>` envelope containing:
  - `callerOrderId` (string)
  - `midtransOrderId` (string)
  - `gatewayStatus` — `TransactionStatus` from the gateway DB record
  - `midtransStatus` — `transaction_status` from the live Midtrans response
  - `fraudStatus` (string, nullable) — from Midtrans response
  - `grossAmount` (string) — from Midtrans response
  - `midtransTransactionId` (string, nullable) — `transaction_id` from Midtrans
  - `paymentType` (string, nullable) — `payment_type` from Midtrans
  - `createdAt` (ISO 8601) — gateway record creation time
  - `updatedAt` (ISO 8601) — gateway record last update time
- [ ] If the Midtrans API call fails (network error or non-2xx response), returns `502 Bad Gateway` with the Midtrans error message included in the response where available.

### POST /api/snap/cancel/{orderId}

- [ ] `{orderId}` is the child app's `CallerOrderId`.
- [ ] Requires `X-Api-Key` header. Returns `401` if missing or invalid.
- [ ] Resolves environment from `X-Api-Key`. Returns `401` if invalid.
- [ ] Looks up `Db_SnapTransaction` by `(EnvironmentId, CallerOrderId)`. Returns `404` if not found.
- [ ] Calls Midtrans `POST /v2/{MidtransOrderId}/cancel` using the correct server key and base URL.
- [ ] On Midtrans success (`2xx`):
  - Updates `SnapTransaction.TransactionStatus` to the `transaction_status` value from the Midtrans response (typically `"cancel"`).
  - Updates `SnapTransaction.UpdatedAt = DateTime.UtcNow`.
  - Saves changes to the database.
  - Returns `200` with the same response shape as the status endpoint (reflecting the post-cancel state).
- [ ] On Midtrans error indicating a terminal state is not cancellable (e.g. already settled, already cancelled, expired):
  - Returns `422 Unprocessable Entity` with the Midtrans error message.
- [ ] On network or unexpected failure, returns `502 Bad Gateway`.

### Security

- [ ] If a child app supplies an `orderId` that exists in the DB but belongs to a **different** environment (different API key), returns `403 Forbidden` — not `404`. This prevents enumeration of other tenants' order IDs.
  - Implementation: look up by `(EnvironmentId, CallerOrderId)` where `EnvironmentId` comes from the API key — a miss is always a `404` because the scope is already correct. No separate ownership check needed when the query is properly scoped.

---

## Technical Notes

### API Key Resolution Pattern

Reuse the same environment resolution pattern already present in `SnapController.CreateToken`:

```csharp
var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
if (string.IsNullOrEmpty(apiKey)) return Unauthorized(...);

var environment = await m_dbContext.Environments
    .FirstOrDefaultAsync(e => e.ApiKey == apiKey && !e.IsDeleted);
if (environment == null) return Unauthorized(...);
```

### Midtrans Transaction Status API

```
GET https://api.sandbox.midtrans.com/v2/{order_id}/status    (sandbox)
GET https://api.midtrans.com/v2/{order_id}/status            (production)
Authorization: Basic {Base64(serverKey + ":")}
```

Relevant response fields: `transaction_status`, `fraud_status`, `gross_amount`, `transaction_id`, `payment_type`, `status_code`, `status_message`.

### Midtrans Cancel API

```
POST https://api.sandbox.midtrans.com/v2/{order_id}/cancel   (sandbox)
POST https://api.midtrans.com/v2/{order_id}/cancel           (production)
Authorization: Basic {Base64(serverKey + ":")}
```

Cancellation is only valid when `transaction_status` is `pending` or `authorize`. A request to cancel a terminal status (e.g. `settlement`, `cancel`, `expire`) will return a Midtrans error — surface this as `422`.

### No New Database Migration Required

`TransactionStatus` is already a nullable `text` column in `payment.SnapTransactions`. No schema change is needed for these endpoints. Consider task-006 for adding the composite unique index on `(EnvironmentId, CallerOrderId)`.

### New Constants

Add Midtrans API base URLs alongside the existing Snap URL constants in `SnapController`:
```csharp
private const string MidtransSandboxApiUrl = "https://api.sandbox.midtrans.com/v2";
private const string MidtransProductionApiUrl = "https://api.midtrans.com/v2";
```

---

## Out of Scope

- Refund / partial refund endpoints (separate Midtrans API — future task)
- Expire endpoint (`POST /v2/{id}/expire`)
- Webhook retry / re-delivery
- Admin-level status check that bypasses the `X-Api-Key` requirement
- Bulk status queries

---

## Definition of Done

- [ ] `GET /api/snap/status/{orderId}` is implemented and returns live Midtrans status merged with the gateway's DB record
- [ ] `POST /api/snap/cancel/{orderId}` is implemented and updates `TransactionStatus` in the DB on success
- [ ] Both endpoints return `401` for missing or invalid API key
- [ ] Both endpoints return `404` when `CallerOrderId` is not found for the calling environment
- [ ] Both endpoints return `422` when Midtrans rejects the operation due to terminal transaction state
- [ ] Both endpoints return `502` on network failures calling Midtrans
- [ ] `dotnet build` passes with no new errors or warnings
- [ ] Manual sandbox test: status check returns live Midtrans status for a known transaction
- [ ] Manual sandbox test: cancel on a `pending` transaction updates DB status to `cancel`
- [ ] Manual sandbox test: cancel on a `settlement` transaction returns `422`
