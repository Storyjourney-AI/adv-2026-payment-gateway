# Task Completion Summary — task-001: Midtrans Snap Integration

## Overall Impact

The Payment Gateway can now act as a secure intermediary for Midtrans Snap payments. Child apps never need to hold a Midtrans Server Key — they simply call this gateway with their registered API key. The gateway handles all communication with Midtrans and forwards payment notifications back to each child app's registered webhook URL.

---

### Task 1 — Midtrans Config in AppSettings

Change: Added a `Midtrans` block to `appsettings.development.json` with Sandbox and Production sections, each holding a `ServerKey` and an `IsEnabled` flag. Created a strongly-typed `MidtransOptions` class wired into the .NET options system.

Impact: Server keys are managed centrally and never exposed to child apps. The `IsEnabled` flags let operators disable production access independently of sandbox access — useful for limiting who can trigger real charges.

---

### Task 2 — Snap Token DTOs

Change: Created request and response DTOs (`Dto_SnapTokenRequest`, `Dto_SnapTokenResponse`) that define what child apps send when requesting a payment token and what they receive back.

Impact: Clear, validated API contract for child apps integrating with the gateway. `OrderId` is capped at 42 characters to accommodate the internal routing prefix the gateway adds.

---

### Task 3 — Snap Transaction Log (DB)

Change: Added a `Db_SnapTransaction` table that records every Snap token creation request. Each record stores the combined Midtrans `order_id`, the originating environment, gross amount, and transaction status updates received from Midtrans.

Impact: Enables the gateway to route incoming Midtrans webhook notifications back to the correct child app, and provides an audit trail of all payment activity.

> **Developer action required:** Run the EF migration documented in `Migrations/migrations.md` under "Pending Migrations" to create the `payment.SnapTransactions` table before deploying.

---

### Task 4 — Snap Token Endpoints

Change: Added two endpoints to a new `SnapController`:
- `POST api/snap/sandbox/token` — creates a Midtrans Sandbox Snap token
- `POST api/snap/production/token` — creates a Midtrans Production Snap token

Child apps authenticate via an `X-Api-Key` header matching their registered environment key. The gateway internally contacts Midtrans and returns the `{ token, redirect_url }` for the child app to use.

Impact: Child apps can trigger Snap payment flows without ever needing direct Midtrans credentials.

---

### Task 5 — Signature Verification Utility

Change: Added `MidtransSignatureHelper` — a static utility that computes and verifies the SHA-512 signature that Midtrans attaches to every webhook notification.

Impact: Ensures webhook notifications cannot be spoofed. Any forged notification without the correct signature is rejected before processing.

---

### Task 6 — Webhook Receiver Endpoints

Change: Added two endpoints to a new `WebhookController`:
- `POST api/webhook/midtrans/sandbox` — receives Midtrans Sandbox payment notifications
- `POST api/webhook/midtrans/production` — receives Midtrans Production payment notifications

When a notification arrives, the gateway verifies the signature, updates the transaction status in the database, then forwards the raw payload to the child app's registered `WebhookUrl`. If the child app has no webhook URL set, the notification is logged and acknowledged silently. Midtrans always receives a `200 OK` to prevent it from retrying.

Impact: Each child app automatically receives payment status updates at its own registered endpoint without any direct connection to Midtrans.

---

## Notes

- The `order_id` sent to Midtrans is prefixed with 8 characters of the environment's ID (e.g. `a1b2c3d4_ORDER-123`). This is how the gateway knows which child app to notify — no changes required from child apps aside from expecting this prefix in webhook payloads.
- Failed webhook forwarding (child app is down) is logged but does not block the Midtrans acknowledgement. Retry logic is out of scope for this task.
- Production payments are disabled by default (`IsEnabled: false`). A developer must explicitly set the production server key and flip the flag when ready.
