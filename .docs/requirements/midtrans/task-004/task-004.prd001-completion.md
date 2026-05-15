# Task Completion Summary — task-004

## Overall Impact

Two new API endpoints are now live in the payment gateway. Child applications can query the live status of any payment and cancel pending payments — all without ever handling Midtrans server keys directly. The gateway securely proxies both operations using the existing `X-Api-Key` authentication model.

---

### Task A — New Response DTO (`Dto_SnapStatusResponse`)

**Change:** Created a new data transfer object that carries the combined payment state — fields from the gateway's own database (like `callerOrderId`, `gatewayStatus`, timestamps) merged with live fields from Midtrans (`midtransStatus`, `fraudStatus`, `grossAmount`, `paymentType`, `midtransTransactionId`).  
**Impact:** Clean, typed response shape for both new endpoints; no ambiguity about which field comes from where.

---

### Task B — `GET /api/snap/status/{orderId}` Endpoint

**Change:** Added a new endpoint that accepts a child app's own `orderId` plus their `X-Api-Key`, resolves the correct Midtrans environment (sandbox or production), and calls the Midtrans status API in real time. Returns the live status merged with the gateway's stored record.  
**Impact:** Child apps can now reconcile missed webhooks or poll payment state on demand — no Midtrans credentials required on their side. Returns `401` for bad keys, `404` for unknown orders, `502` for Midtrans connectivity issues.

---

### Task C — `POST /api/snap/cancel/{orderId}` Endpoint

**Change:** Added a new endpoint that cancels a pending Midtrans payment by the child app's `orderId`. On a successful cancel, the gateway updates `TransactionStatus` in the database to `"cancel"` and returns the final state. Terminal-state rejections from Midtrans (e.g. already settled) are surfaced as `422 Unprocessable Entity`; network failures return `502`.  
**Impact:** Child apps can implement order cancellation flows without any Midtrans SDK or server key. The gateway's database stays in sync with the cancel outcome automatically.

---

### Task D — Midtrans API URL Constants

**Change:** Added `MidtransSandboxApiUrl` and `MidtransProductionApiUrl` constants to `SnapController` alongside the existing Snap token URL constants.  
**Impact:** Centralised, easy-to-update base URL references — no magic strings scattered through the new endpoint methods.

---

## No Database Changes

No migration is needed. The `TransactionStatus` column already exists as a nullable text field and is updated in-place by the cancel endpoint.

## Build Status

`dotnet build` — ✅ **0 errors, 0 warnings**
