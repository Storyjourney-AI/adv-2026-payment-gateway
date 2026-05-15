# Review Report — task-002.prd001
# Test Purchase Button per Environment

**Review Date:** 2026-03-11
**Reviewer:** GitHub Copilot (c-reviewer)
**PRD:** task-002.prd001.md
**Implementation:** SnapController.cs, Page_ApplicationDetail.tsx, environment.api.ts, application.types.ts

---

## Overview

This review covers the end-to-end implementation of the "Test Purchase" feature. The persona is the **Admin/Developer** — a JWT-authenticated dashboard user managing Midtrans-connected applications.

The implementation is largely complete and well-structured. One acceptance criterion from the PRD is partially met (the "disabled button on 503" behavior), and two minor backend code issues are noted. No critical security vulnerabilities were found.

---

## PRD Validation

### Backend Acceptance Criteria

| # | Criterion | Status | Notes |
|---|-----------|--------|-------|
| BE-1 | `POST /api/snap/test/{environmentId}` exists | ✅ Implemented | Route is under `[Route("api/snap")]` correctly |
| BE-2 | Requires JWT `[Authorize(Policy = "RequireUser")]` | ✅ Implemented | No `[AllowAnonymous]` on the action |
| BE-3 | Returns 404 if environment not found or soft-deleted | ✅ Implemented | `!e.IsDeleted` filter in LINQ query |
| BE-4 | Returns 403 if caller is not owner or Super Admin | ✅ Implemented | Ownership check matches pattern in `EnvironmentController` |
| BE-5 | `GrossAmount = 30000` | ✅ Implemented | |
| BE-6 | `OrderId = "test_" + 8-char GUID` | ✅ Implemented | `"test_" + Guid.NewGuid().ToString("N")[..8]` |
| BE-7 | Three item details (`test_item_1/2/3`, 10000 each) | ✅ Implemented | Uses `SnapItemDetail` (correct class name) |
| BE-8 | Routes via `env.IsSandbox` | ✅ Implemented | |
| BE-9 | Returns `DataWrapper<Dto_SnapTokenResponse>` on success | ✅ Implemented | Via `CreateTokenAsync` shared helper |
| BE-10 | Returns 503 if Midtrans environment disabled | ✅ Implemented | Checked before calling `CreateTokenAsync` |
| BE-11 | Logs `Db_SnapTransaction` with `CallerOrderId = "test_*"` | ✅ Implemented | Via `CreateTokenAsync` which saves the record |
| BE-12 | No new `[AllowAnonymous]` surface | ✅ Implemented | `[AllowAnonymous]` only on the 3 existing token actions |

### Frontend Acceptance Criteria

| # | Criterion | Status | Notes |
|---|-----------|--------|-------|
| FE-1 | "Test Purchase" button on each environment row | ✅ Implemented | Uses `FlaskConical` icon |
| FE-2 | Confirmation `AlertDialog` (not Toast) | ✅ Implemented | |
| FE-3 | Sandbox: title "Test Purchase", body correct, confirm "Proceed" | ✅ Implemented | |
| FE-4 | Production: title "⚠ Real Money Warning", body correct, confirm "Yes, Proceed" with destructive styling | ✅ Implemented | Destructive color via `className` prop |
| FE-5 | Loading state (`Loader2` spinner) on confirm | ✅ Implemented | Per-row `testPurchaseLoading` state |
| FE-6 | Opens `redirectUrl` in new tab with `noopener,noreferrer` | ✅ Implemented | |
| FE-7 | `toast.error` on API error | ✅ Implemented | |
| FE-8 | Button disabled with tooltip "Production payment environment is disabled" on 503 | ⚠️ Partially Implemented | 503 error is surfaced to the user via `toast.error` with the server's message. Button is NOT disabled and no tooltip is shown. See finding **FE-8** below. |
| FE-9 | No new page or route | ✅ Implemented | All changes within existing `Page_ApplicationDetail.tsx` |

---

## Userflow Validation

### US-1 — Admin verifies sandbox integration

| Step | Status | Notes |
|------|--------|-------|
| Navigate to Application Detail page | ✅ Covered | Existing page, no change needed |
| See "Test Purchase" button on each environment row | ✅ Covered | Button rendered in `environments.map()` |
| Click "Test Purchase" | ✅ Covered | Opens `AlertDialog` |
| See sandbox-specific disclaimer modal | ✅ Covered | Title "Test Purchase", neutral confirm "Proceed" |
| Confirm | ✅ Covered | `handleTestPurchase` called |
| Midtrans Sandbox payment page opens in new tab | ✅ Covered | `window.open(redirectUrl, "_blank", "noopener,noreferrer")` |

### US-2 — Admin verifies production integration

| Step | Status | Notes |
|------|--------|-------|
| See production "Test Purchase" button | ✅ Covered | |
| See prominent warning before proceeding | ✅ Covered | Title "⚠ Real Money Warning", destructive confirm button |
| Cancel does not trigger a charge | ✅ Covered | `AlertDialogCancel` closes dialog, no API call made |
| Confirm triggers live transaction | ✅ Covered | |

### US-3 — Clean test isolation

| Step | Status | Notes |
|------|--------|-------|
| `CallerOrderId` starts with `test_` | ✅ Covered | `"test_" + 8-char GUID suffix` |
| Record appears in `SnapTransactions` table | ✅ Covered | `CreateTokenAsync` inserts `Db_SnapTransaction` |
| Identifiable in Midtrans dashboard | ✅ Covered | `midtransOrderId = envPrefix + "_" + callerOrderId` |

---

## Security Findings

### 🟢 `S-1` — Duplicate `IsEnabled` Check (Low)

**Location:** `SnapController.cs` → `TestPurchase` action (line ~208) and `CreateTokenAsync` (line ~240)

**Issue:** `TestPurchase` checks `envOptions.IsEnabled` and returns 503 before calling `CreateTokenAsync`. `CreateTokenAsync` checks `IsEnabled` again as its first step. The 503 guard is executed twice for every test purchase call that hits a disabled environment.

**Suggestion:** This is harmless — the second check in `CreateTokenAsync` acts as a safety net for callers that bypass the pre-check. No change required unless you want to extract the check out of `CreateTokenAsync` into a dedicated guard. Not worth changing unless `CreateTokenAsync` is refactored.

---

### 🟡 `S-2` — Soft-deleted Application not excluded in ownership check (Medium)

**Location:** `SnapController.cs` → `TestPurchase` action, `Include(e => e.Application)` query

**Issue:** The query filters `!e.IsDeleted` on the `Environment`, but not on the included `Application`. If an `Application` has been soft-deleted while its `Environment` has not (e.g. a data inconsistency), the ownership check would still run against the deleted application's `UserId`. An unprivileged user whose application was deleted could theoretically still trigger a test purchase on an environment that wasn't individually soft-deleted.

**Suggestion:** Extend the query to also filter the application:
```csharp
.Include(e => e.Application)
.FirstOrDefaultAsync(e => e.Id == environmentId && !e.IsDeleted && e.Application != null && !e.Application.IsDeleted);
```
Note: this mirrors a gap that exists in other controllers (`EnvironmentController`) — it is a systemic pattern issue rather than a new risk introduced by this feature.

---

### 🟢 `S-3` — GUID type binding on route parameter (Low / Informational)

**Location:** `SnapController.cs` → `TestPurchase(Guid environmentId)`

**Issue:** Using `Guid` as the parameter type (rather than `string`) means ASP.NET Core's model binder automatically validates that the route segment is a valid GUID format, returning 400 Bad Request for malformed values. This is correct and secure.

**Suggestion:** No change needed. Noting as a positive security practice consistent with other controllers.

---

## UI/UX Findings

### 🔴 `UX-1` (Blocking) — 503 response does not disable the Test Purchase button

**Persona:** Admin
**Location:** `Page_ApplicationDetail.tsx` → `handleTestPurchase` → 503 error path

**Issue:** The PRD explicitly requires: *"The Test Purchase button is disabled (greyed out with tooltip 'Production payment environment is disabled') when the target Midtrans environment is disabled (response 503)."* The current implementation shows a `toast.error` with the server message (which is correct), but the button remains fully clickable. The admin can keep clicking it without understanding why it fails. There is also no tooltip to indicate the environment is disabled.

**Suggestion:** After receiving a 503 response, store the disabled environment ID in a Set state variable and disable the button with a `title` tooltip attribute:
```tsx
const [disabledEnvIds, setDisabledEnvIds] = useState<Set<string>>(new Set());

// In handleTestPurchase error path:
if (response.code === 503) {
  setDisabledEnvIds(prev => new Set(prev).add(env.id));
  toast.error("Payment environment is disabled");
  return;
}

// On button:
<Button
  title={disabledEnvIds.has(env.id) ? "Production payment environment is disabled" : undefined}
  disabled={testPurchaseLoading === env.id || disabledEnvIds.has(env.id)}
>
```

---

### 🟠 `UX-2` (Major) — Button row becomes very crowded on narrow viewports

**Persona:** Admin
**Location:** `Page_ApplicationDetail.tsx` → environment row action buttons (`div.flex.gap-2`)

**Issue:** The row now has **4 action controls**: "Test Purchase" (with text label), "Regenerate Key" (with text label), Edit (icon only), Delete (icon only). On viewports below ~900px the buttons will wrap or overflow, especially since "Test Purchase" now has the widest label. No `flex-wrap` or responsive overflow handling is present.

**Suggestion:** For "Test Purchase" and "Regenerate Key", consider hiding the text label on `md` and below (show icon only with a tooltip), for example using `<span class="hidden lg:inline">Test Purchase</span>`. Or group icon-only actions behind a dropdown menu (`DropdownMenu`) for compact mode.

---

### 🟡 `UX-3` (Minor) — No success feedback beyond new tab opening

**Persona:** Admin
**Location:** `Page_ApplicationDetail.tsx` → `handleTestPurchase` success path

**Issue:** After confirm, the button shows a spinner and a new tab opens. However, if the user has browser pop-ups blocked, the `window.open` call will be silently denied and no tab will open. The admin gets no feedback about why nothing happened.

**Suggestion:** Check the return value of `window.open` — it returns `null` when blocked by the browser:
```tsx
const newTab = window.open(response.data.redirectUrl, "_blank", "noopener,noreferrer");
if (!newTab) {
  toast.warning("Pop-up blocked. Please allow pop-ups for this site and try again.");
}
```

---

### 🟢 `UX-4` (Enhancement) — Production warning dialog lacks environmental context

**Persona:** Admin
**Location:** `Page_ApplicationDetail.tsx` → `AlertDialog` for production environments

**Issue:** The production warning dialog does not mention the environment name or application name. When there are multiple production environments visible on screen, the admin may lose track of which one they confirmed for.

**Suggestion:** Include the environment name in the dialog body:
> *"This will create a LIVE Midtrans transaction for **[env.name]**. Real money will be charged..."*

---

## Overall Verdict

**Pass with Conditions**

The implementation is solid, complete on all happy-path user stories, and follows the established codebase patterns throughout (ownership check, `DataWrapper` responses, `authenticatedFetch` service layer, `AlertDialog` UX). The backend is secure and correctly gated.

The single blocking item before this can be considered fully done to spec is **UX-1** — the 503 "disabled button with tooltip" behavior required by the PRD is not implemented. All other findings are improvements beyond the current acceptance criteria.

### Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| 🔴 Blocking | 1 | UX-1 (503 button disabled) |
| 🟠 High | 1 | UX-2 (button row overflow) |
| 🟡 Medium | 2 | S-2 (soft-deleted application), UX-3 (popup blocked feedback) |
| 🟢 Low / Enhancement | 2 | S-1 (double enabled check), UX-4 (env name in production dialog) |
