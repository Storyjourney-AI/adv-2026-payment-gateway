# [Feature] Test Purchase Button per Environment

**Labels:** `feature` `frontend` `backend` `midtrans` `ux`
**Milestone:** task-002
**Priority:** Medium
**Related:** task-001 (Midtrans Snap Integration)

---

## Summary

Add a **"Test Purchase"** button to the Application Detail page for each environment. Clicking it opens a confirmation warning dialog, then calls the gateway's Snap endpoint with pre-built dummy data, and opens the returned payment URL in a new browser tab. This allows the admin/developer to verify Midtrans integration is live without writing any code against the gateway.

---

## Background / Context

As of task-001, the gateway exposes:
- `POST /api/snap/sandbox/token` — accepts `X-Api-Key` + `Dto_SnapTokenRequest`, returns `{ token, redirect_url }`
- `POST /api/snap/production/token` — same, against Midtrans Production

The dashboard already has the Application Detail page (`Page_ApplicationDetail.tsx`) that lists environments. Each environment row has `Regenerate API Key`, `Edit`, and `Delete` actions. There is currently no way to trigger a test transaction from the admin UI.

The separation between sandbox and production routing will be superseded by the `IsSandbox` flag introduced in PRD 002. This PRD is compatible with both the current two-endpoint model and the unified model — the backend endpoint introduced here will internally resolve the correct target using `IsSandbox`.

**Admin users (JWT authenticated) must not need to copy-paste API keys to test a snap flow.** The test endpoint runs on their behalf using server-side resolution.

---

## Problem Statement

1. After configuring an application and its environments, there is no quick way to verify the Midtrans integration is working end-to-end.
2. Manually calling `POST /api/snap/sandbox/token` requires an API client, copy-pasting the API key, and constructing a dummy payload — friction that shouldn't exist for the admin user.
3. Production environments need an extra guard: the admin must consciously confirm they are about to trigger a real transaction.

---

## User Stories

**US-1 — Admin verifies sandbox integration:**
> As an admin, I want to click a "Test Purchase" button on a staging environment, confirm the test-only disclaimer, and see the Midtrans Sandbox payment page open in a new tab — so I can verify the API key and Midtrans config are wired correctly without leaving the dashboard.

**US-2 — Admin verifies production integration:**
> As an admin, I want to click a "Test Purchase" button on a production environment and see a prominent warning that real money will be charged before proceeding — so I do not accidentally trigger a live charge.

**US-3 — Clean test isolation:**
> As a developer, I want dummy test purchases to use clearly identifiable order IDs (prefixed with `test_`) so they are distinguishable from real transactions in the transaction log and Midtrans dashboard.

---

## Acceptance Criteria

### Backend

- [ ] New endpoint: `POST /api/snap/test/{environmentId}`
  - Requires JWT (`[Authorize(Policy = "RequireUser")]`), **not** `X-Api-Key`
  - Resolves the environment from the DB by `environmentId`; returns `404 Not Found` (wrapped `DataWrapper`) if not found or soft-deleted
  - Enforces ownership: only the environment's application owner (or Super Admin) may call it; returns `403 Forbidden` otherwise
  - Constructs a fixed dummy payload:
    - `GrossAmount = 30000` (3 items × 10 000)
    - `OrderId = "test_{Guid.NewGuid():N[..8]}"` — unique each call, keeps `CallerOrderId` under 42 chars
    - `ItemDetails`:
      - `{ Id: "test_item_1", Name: "test_something_1", Price: 10000, Quantity: 1 }`
      - `{ Id: "test_item_2", Name: "test_something_2", Price: 10000, Quantity: 1 }`
      - `{ Id: "test_item_3", Name: "test_something_3", Price: 10000, Quantity: 1 }`
  - Routes to Midtrans **Sandbox** if `env.IsSandbox == true`; routes to Midtrans **Production** if `env.IsSandbox == false`
    - _Fallback until PRD 002 is merged_: if `IsSandbox` field does not yet exist, derive from `env.Name.ToLower() == "production"` → production, else → sandbox
  - Returns `DataWrapper<Dto_SnapTokenResponse>` (`{ token, redirectUrl }`) on success
  - Returns `503 Service Unavailable` if the target Midtrans environment is disabled (`IsEnabled = false` in `MidtransOptions`)
  - Logs the test transaction as a normal `Db_SnapTransaction` record with `CallerOrderId = "test_*"` so it is auditable
- [ ] Does **not** expose a new `[AllowAnonymous]` surface — full JWT gate

### Frontend

- [ ] Each environment row on `Page_ApplicationDetail` gets a **"Test Purchase"** action button (e.g., `FlaskConical` or `TestTube2` icon, label "Test Purchase")
- [ ] Clicking opens a confirmation `AlertDialog` (not a dismissible Toast) with:
  - **Sandbox / `IsSandbox = true`:** title `"Test Purchase"`, body `"This will create a test transaction using Midtrans Sandbox. No real money will be charged."`, confirm button `"Proceed"`
  - **Production / `IsSandbox = false`:** title `"⚠ Real Money Warning"`, body `"This will create a LIVE Midtrans transaction. Real money will be charged to the payment method. Are you sure?"`, confirm button variant `destructive` labelled `"Yes, Proceed"`
- [ ] On confirm:
  - Button shows loading state (`Loader2` spinner) while waiting
  - Calls `POST /api/snap/test/{environmentId}` with the user's JWT cookie/header
  - On success: opens `redirectUrl` in a new tab (`window.open(url, "_blank", "noopener,noreferrer")`)
  - On error: shows a `toast.error(message)` with the error from the server
- [ ] The Test Purchase button is **disabled** (greyed out with tooltip `"Production payment environment is disabled"`) when the target Midtrans environment is disabled (response `503`)
- [ ] No new page or route is needed; everything is within the existing Application Detail page

---

## Technical Notes

### New Backend File

- **`PaymentGateway.Server/Midtrans/Controllers/SnapTestController.cs`** (or extend `SnapController.cs` with an additional `[HttpPost("test/{environmentId}")]` action)
  - Place under `[Route("api/snap")]` so endpoint is `POST /api/snap/test/{environmentId}`
  - Inject: `AppDbContext`, `IOptions<MidtransOptions>`, `IHttpClientFactory`, `ILogger`, `UserManager<Db_ApplicationUser>`
  - Reuses the private helper from `SnapController` if extracted, otherwise duplicates the HTTP call block (acceptable — same class preferred)

### OrderId uniqueness

The dummy `OrderId` sent to Midtrans is: `{env.Id[0..7]}_{callerOrderId}` (as per existing prefix convention). `callerOrderId = "test_{shortGuid}"`. Maximum total length = 8 + 1 + 5 + 8 = 22 chars — well within Midtrans' 50-char limit and the gateway's 42-char cap.

### IsSandbox dependency

This feature is designed to work before and after PRD 002 is merged:
- **Before PRD 002**: derive sandbox/production from environment name (`"production"` → false, anything else → true)
- **After PRD 002**: use `env.IsSandbox` directly

Prefer to ship PRD 002 first if sequencing allows.

### Frontend service addition

Add to `paymentgateway.client/app/services/application/utils/environment.api.ts`:

```ts
export async function testPurchase(environmentId: string): Promise<DataWrapper<Dto_SnapTokenResponse>> {
  const res = await apiFetch(`/api/snap/test/${environmentId}`, { method: "POST" });
  return res.json();
}
```

---

## Out of Scope

- Customising the dummy item names/amounts from the UI
- Viewing test transaction history from the dashboard (covered by a future transactions page)
- Non-Snap payment method tests (e.g. Xendit)

---

## Definition of Done

- [ ] Backend endpoint exists and is covered by manual test (no automated test required for this task)
- [ ] Frontend button renders on each environment row
- [ ] Sandbox warning modal works correctly
- [ ] Production warning modal works correctly (destructive confirm)
- [ ] New tab opens with Midtrans Snap payment page after confirm
- [ ] Transaction record appears in `payment.SnapTransactions` DB table with `CallerOrderId` starting with `test_`
