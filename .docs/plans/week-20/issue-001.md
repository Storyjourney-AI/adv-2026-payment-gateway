# Issue 001: Verified Midtrans Callback Flow

## Owner Role
- gh-tech-planner

## Problem

The current Midtrans integration mixes two different concerns:

- Midtrans notification and browser redirect signals are treated as if they can determine payment truth by themselves.
- Browser callback handlers currently trust the incoming route and query string enough to choose a frontend redirect without first reconciling against Midtrans.

That creates two concrete gaps:

- A forged or stale browser redirect can influence the user-facing result before the gateway verifies status with Midtrans.
- The current implementation only models success and failure frontend redirects, while Midtrans also supports an unfinish path that should map to a pending state.

## Current State

- Webhook endpoints already exist for production and sandbox and verify Midtrans signatures before updating local state.
- Browser callback endpoints exist for finish and error flows, but they currently redirect directly based on local environment URLs.
- No dedicated service exists for Midtrans Get Status verification.
- Environment configuration currently exposes:
  - `WebhookUrl`
  - `SuccessResponseUrl`
  - `FailureResponseUrl`
- New environments still default to `https://example.com/success` and `https://example.com/failure`.

## Desired Flow

1. The gateway creates a Snap transaction and stores the Midtrans `order_id`.
2. Midtrans may notify the gateway through four channels:
   - webhook notification
   - finish redirect URL
   - unfinish redirect URL
   - error redirect URL
3. Any of those channels acts only as a trigger.
4. The gateway calls Midtrans Get Status using the stored `order_id` and environment.
5. The Get Status response becomes the source of truth.
6. The gateway updates the local transaction idempotently.
7. For browser callbacks, the gateway redirects the customer to the configured frontend URL that matches the verified status.

## Scope

### In Scope
- Add a Midtrans Get Status verification service.
- Refactor browser callback endpoints to verify before redirecting.
- Add explicit support for the unfinish callback path.
- Reuse one normalized payment update path for webhook and browser callbacks.
- Replace `example.com` default redirect URLs.
- Add focused automated tests for callback verification and redirect selection.

### Out of Scope
- Reworking the child-app webhook forwarding design.
- Full payment domain redesign outside Snap callbacks.
- Frontend redesign of payment result pages.

## Design Decisions

### 1. Source Of Truth

Midtrans Get Status is the source of truth for payment state. Webhooks and browser redirects are only triggers.

### 2. Callback Surface

The gateway should own all four public Midtrans callback entry points:

- Production webhook
- Sandbox webhook
- Production browser finish/unfinish/error
- Sandbox browser finish/unfinish/error

The Midtrans dashboard should point to gateway endpoints, not directly to project frontend URLs.

### 3. Pending Redirect Handling

The data model currently only supports success and failure URLs. For this fix slice, unfinish should map to the failure redirect URL with verified `transaction_status` preserved in query string.

Rationale:

- It avoids a migration and API shape change in this slice.
- It keeps the implementation narrow enough to complete safely.
- The frontend can still distinguish pending vs failed using verified query parameters.

Follow-up option:

- Add a dedicated `PendingResponseUrl` later if product needs a separate pending destination.

### 4. Update Semantics

Webhook and browser callback handling must converge into one idempotent transaction reconciliation path.

That path should:

- load the local `Db_SnapTransaction`
- call Midtrans Get Status
- map the remote response into the local status model
- update stored fields such as `TransactionStatus` and `MidtransTransactionId`

## Implementation Plan

### A. Add Midtrans Status Verification Service

Create a service that:

- accepts `orderId` and `isSandbox`
- selects the correct Midtrans base API URL and server key
- performs authenticated `GET /v2/{orderId}/status`
- returns a normalized status result object

The service should not depend on controller types.

### B. Add Transaction Reconciliation Service

Create a service that:

- loads the local Snap transaction by Midtrans order id
- calls the status verification service
- updates local database state
- returns a reconciliation result containing:
  - verified transaction status
  - local environment
  - final redirect classification

### C. Refactor Browser Callback Endpoints

Replace direct redirect logic with verified reconciliation.

Required endpoints:

- `/api/midtrans/snap/callback`
- `/api/midtrans/sandbox/snap/callback`
- `/api/midtrans/snap/callback/unfinish`
- `/api/midtrans/sandbox/snap/callback/unfinish`
- `/api/midtrans/snap/callback/error`
- `/api/midtrans/sandbox/snap/callback/error`

Controller behavior:

- validate `order_id`
- reconcile against Midtrans
- choose configured frontend target URL
- append verified status query values
- redirect

### D. Reuse Reconciliation From Webhook Flow

Keep webhook signature validation and replay protection intact.

After the webhook is authenticated, hand off to the same reconciliation logic used by browser callbacks instead of performing ad hoc local updates.

### E. Replace Default Redirect URLs

Change default application environment URLs from `example.com` placeholders to gateway-owned payment result pages:

- `https://payment.advine.id/payment/success`
- `https://payment.advine.id/payment/failed`

These defaults should be applied to both staging and production environment creation.

### F. Add Tests

Add focused tests for:

- verified status reconciliation success path
- pending/unfinish redirect selection
- failed/error redirect selection
- unknown order id handling
- Midtrans status API failure handling
- webhook path using shared reconciliation logic

## Risks

- Browser callback and webhook may race; reconciliation must stay idempotent.
- Midtrans Get Status can temporarily fail; callback handlers need a safe fallback response.
- Mapping pending to failure URL is functionally acceptable for this slice, but product may later want a dedicated pending destination.

## Acceptance Criteria

- Browser callback handlers do not trust redirect route or query string as payment truth.
- Every callback path verifies against Midtrans before updating local state or redirecting users.
- Webhook and browser callback processing share the same reconciliation logic.
- Unfinish callbacks are supported.
- New environments no longer default to `example.com` URLs.
- Automated tests cover the verified callback flow.

## Execution Notes

- Development role: gh-developer
- Review role: gh-reviewer
- Expected output: merged implementation plus test evidence and review findings resolved