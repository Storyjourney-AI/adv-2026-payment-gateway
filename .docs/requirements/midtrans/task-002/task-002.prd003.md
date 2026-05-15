# [Fix] Webhook Flow Hardening — Guarantee All Midtrans Notifications Route Through Gateway

**Labels:** `fix` `backend` `midtrans` `security` `architecture`
**Milestone:** task-002
**Priority:** Medium
**Related:** task-001 (Midtrans Snap Integration), task-002.prd002

---

## Summary

Confirm and harden the Midtrans webhook notification architecture. The intended flow — **Midtrans → Gateway → DB update → Forward to child app's `WebhookUrl`** — is already implemented but uses `X-Override-Notification`, which _adds_ the gateway as an extra recipient rather than _replacing_ all recipients. Change to `X-Custom-Notification` so the gateway is the **sole and exclusive** notification target, regardless of what may be configured in the Midtrans dashboard. Also document the flow explicitly in code and in this doc.

---

## Background / Context

### Current Implementation (task-001)

When a child app calls `POST /api/snap/{env}/token`, the gateway sends the Snap creation request to Midtrans with the following HTTP header:

```http
X-Override-Notification: https://{gateway-base-url}/api/webhook/midtrans/{sandbox|production}
```

Midtrans' `X-Override-Notification` behavior: it **appends** the provided URL to any notification URLs already configured on the Midtrans merchant dashboard. This means:

- If no webhook URL is set in the Midtrans dashboard → only the gateway receives notifications ✅
- If a webhook URL _is_ set in the Midtrans dashboard → **both** that URL and the gateway receive the notification ⚠️

The second case is a problem: the child app's server (or any URL in the Midtrans dashboard) would receive raw Midtrans notifications **directly**, bypassing the gateway entirely. This defeats the purpose of the gateway as the single notification hub.

### What `X-Custom-Notification` Does Differently

Midtrans also supports `X-Custom-Notification`: it **replaces** all notification URLs (including the dashboard-configured one) for that specific transaction. Only the URL(s) in this header receive the notification.

Switching to `X-Custom-Notification` ensures:
1. The gateway is always the only Midtrans notification recipient.
2. No raw Midtrans payloads ever reach the child app directly (the gateway receives, verifies, then forwards).
3. The signature verification performed by the gateway cannot be bypassed.

### Desired End-to-End Webhook Flow

```
Midtrans Payment Completed
         │
         ▼
POST /api/webhook/midtrans/{sandbox|production}  ← ONLY recipient
         │  (1) Read raw body
         │  (2) Verify SHA-512 signature_key against server key
         │  (3) Look up Db_SnapTransaction by order_id
         │  (4) Update transaction status + MidtransTransactionId in DB
         │  (5) Look up Db_Environment.WebhookUrl
         ▼
Forward raw body → child app's registered WebhookUrl  ← forwarded by gateway
         │
         ▼
Return 200 OK to Midtrans  ← always, to prevent retries
```

Steps 1–5 + forwarding are already implemented in `WebhookController.cs`. The only gap is step 0: ensuring Midtrans calls only the gateway in the first place.

---

## Problem Statement

1. `X-Override-Notification` does not guarantee exclusivity — a Midtrans dashboard webhook URL bypasses the gateway.
2. If someone sets a webhook URL directly in the Midtrans merchant dashboard (perhaps as a fallback or misconfiguration), child apps would receive un-verified, un-logged Midtrans payloads directly.
3. The gateway's signature verification and DB logging would be skipped in that path — creating an audit gap and potential SSRF/integrity risk.
4. The intended architecture ("always webhook to this server first") is not enforced at the protocol level.

---

## User Stories

**US-1 — Guaranteed gateway routing:**
> As a gateway operator, I want every Midtrans payment notification to always arrive at this gateway first, regardless of what is configured in the Midtrans merchant dashboard — so no notification ever bypasses our signature check and DB update.

**US-2 — Child app receives clean forwarded notification:**
> As a child app developer, I want to receive payment notifications at my registered `WebhookUrl` as forwarded by the gateway, already verified for authenticity — so I can trust the notification without re-verifying the Midtrans signature.

---

## Acceptance Criteria

### Backend — SnapController

- [ ] In `SnapController.CreateTokenAsync`, change the Midtrans request header from:
  ```http
  X-Override-Notification: {webhookCallbackUrl}
  ```
  to:
  ```http
  X-Custom-Notification: {webhookCallbackUrl}
  ```
  This applies to both the existing `sandbox/token`, `production/token` endpoints and the new unified `/api/snap/token` endpoint from PRD 002.

- [ ] The `webhookCallbackUrl` construction remains unchanged:
  - `IsSandbox = true` → `{MidtransOptions.BaseUrl}/api/webhook/midtrans/sandbox`
  - `IsSandbox = false` → `{MidtransOptions.BaseUrl}/api/webhook/midtrans/production`

- [ ] The value passed must be a single HTTPS URL pointing to this gateway (no comma-separated multi-URL). Midtrans supports one URL per `X-Custom-Notification` header value.

### Backend — WebhookController

- [ ] No functional changes required to `WebhookController.cs` — the forwarding logic is already correct.
- [ ] Verify (code review / manual test) the existing SSRF guard `IsWebhookUrlSafe()` is called before forwarding to child app's `WebhookUrl`:
  - Must be `https://`
  - Must not be a loopback address
  - This check already exists but should be confirmed as working

### Webhook Forward Error Handling (no change, confirm only)

- [ ] If the child app's `WebhookUrl` is unreachable or returns an error, the gateway:
  - Logs the failure with the environment ID, order ID, and HTTP status
  - **Does not** retry (retry logic is out of scope)
  - **Always** returns `200 OK` to Midtrans regardless — to prevent Midtrans from re-sending the notification to the gateway
- [ ] If `WebhookUrl` is `null` or empty, the gateway silently acknowledges and logs — already confirmed implemented

---

## Technical Notes

### X-Override-Notification vs X-Custom-Notification — Reference

| Header | Behaviour |
|---|---|
| `X-Override-Notification` | Adds to the notification list; does **not** remove dashboard-configured URLs |
| `X-Custom-Notification` | **Replaces** all notification URLs for this transaction only |

Source: Midtrans Snap API documentation.

### Change Is a One-Line Fix

In `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`, line:
```csharp
httpRequest.Headers.Add("X-Override-Notification", webhookCallbackUrl);
```
becomes:
```csharp
httpRequest.Headers.Add("X-Custom-Notification", webhookCallbackUrl);
```

This change affects all token creation calls (sandbox, production, and the unified endpoint from PRD 002) since they all go through `CreateTokenAsync`.

### BaseUrl Configuration Reminder

`MidtransOptions.BaseUrl` must be set to the publicly routable HTTPS URL of the gateway (e.g. `https://payment-gateway.example.com`). Midtrans cannot call `localhost`. Confirm this is set in `appsettings.development.json` for any integration testing in a tunnelled environment.

### Webhook Endpoint Stays Split

Even after PRD 002 introduces the unified token endpoint, the **webhook receivers remain as two separate endpoints** (`/sandbox` and `/production`). This split is intentional — it allows the gateway to know which `MidtransOptions` server key to use for signature verification without a DB lookup (before the transaction is even found). The webhook URL encoded in `X-Custom-Notification` encodes this routing decision.

### Sequence Diagram (Full Flow After Fix)

```
Child App                  Gateway (SnapController)           Midtrans             Gateway (WebhookController)        Child App WebhookUrl
─────────                  ────────────────────────           ────────             ─────────────────────────────       ─────────────────────
POST /api/snap/token  ───► lookup env (X-Api-Key)
                           build midtrans body
                           set X-Custom-Notification
                                  ──────────────────────────► POST Snap API
                                                              create payment
                                  ◄──────────────────────── {token, redirect_url}
                      ◄─── return token + redirect_url
                      
open redirect_url ──────────────────────────────────────────► payment hosted page
                                                              customer pays
                                                              POST webhook ──────► POST /api/webhook/midtrans/{env}
                                                                                    verify signature
                                                                                    update DB
                                                                                    forward payload ──────────────► POST {WebhookUrl}
                                                                                    return 200 OK ◄── Midtrans
```

---

## Architecture Clarification: Order ID-Based Routing (Already Implemented)

The question of _how the gateway knows which child app to forward a webhook to_ is answered by the `Db_SnapTransaction` table and a deliberate `order_id` prefix scheme — already in place since task-001.

### How It Works

**At token creation** (`SnapController.CreateTokenAsync`):
```csharp
var midtransOrderId = environment.Id.ToString("N")[..8] + "_" + request.OrderId;
// e.g.  "abcdef12_your-order-123"
```
- The gateway namespaces the child app's `OrderId` with the first 8 hex chars of the `EnvironmentId`.
- This `midtransOrderId` is what gets sent to Midtrans as `order_id`.
- A `Db_SnapTransaction` row is persisted immediately, linking `MidtransOrderId → EnvironmentId`.
- A duplicate-check rejects any second token creation for the same `midtransOrderId`.

**At webhook receipt** (`WebhookController.HandleWebhookAsync`):
```csharp
var snapTransaction = await m_dbContext.SnapTransactions
    .Include(t => t.Environment)
    .FirstOrDefaultAsync(t => t.MidtransOrderId == orderId);
// orderId came from Midtrans webhook payload's "order_id" field
...
var webhookUrl = snapTransaction.Environment?.WebhookUrl;  // forward here
```
Midtrans returns `order_id` verbatim in the webhook payload. The gateway looks it up in `Db_SnapTransaction`, resolves the `Db_Environment`, and reads `WebhookUrl` — which is the child app's registered endpoint.

### Why This Is Reliable

| Property | Guarantee |
|---|---|
| `order_id` uniqueness | `{envId[0..8]}_` prefix namespaces per environment; duplicate guard at creation |
| Midtrans contract | `order_id` is returned unchanged in every webhook notification |
| DB correlation | `Db_SnapTransaction.MidtransOrderId` is the sole lookup key |
| Multi-app isolation | Two child apps can use the same raw `CallerOrderId`; the prefix makes them distinct |

**This mechanism requires no changes.** The only missing piece is the `X-Custom-Notification` fix to ensure Midtrans actually routes to the gateway exclusively.

---

## Out of Scope

- Retry logic for failed webhook forward to child app
- Webhook delivery status tracking in the dashboard
- Verifying the child app's `WebhookUrl` is reachable at environment configuration time
- HTTP (non-HTTPS) webhook forwarding (blocked by SSRF guard — intentional)

---

## Definition of Done

- [x] `X-Custom-Notification` header is sent instead of `X-Override-Notification` in `SnapController`
- [x] SSRF guard `IsWebhookUrlSafe()` confirmed in place in `WebhookController`
- [x] Order ID-based routing chain confirmed: `MidtransOrderId → Db_SnapTransaction → Environment.WebhookUrl`
- [ ] Manual end-to-end test: trigger a sandbox Snap token → complete payment in Midtrans test → verify DB status updated → verify child app's webhook URL receives forwarded payload
- [x] No behaviour changes to the forwarding logic or response codes
