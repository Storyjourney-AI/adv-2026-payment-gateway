# Task Completion Summary — task-002.prd001

## Overall Impact

Admins can now trigger a test Midtrans payment directly from the Application Detail dashboard without any API client, copy-pasting, or manual payload construction. Each environment row gains a "Test Purchase" button that opens a confirmation dialog appropriate to the environment type (neutral for Sandbox, destructive warning for Production), then calls a new JWT-authenticated gateway endpoint that fires a real Snap token request with a fixed dummy payload and opens the resulting Midtrans payment page in a new browser tab.

---

### Task 1.2 — `[AllowAnonymous]` moved from controller to individual actions in `SnapController.cs`

**Change:** Removed the `[AllowAnonymous]` attribute from the `SnapController` class declaration and added it explicitly to each of the three existing public token actions (`CreateToken`, `CreateSandboxToken`, `CreateProductionToken`).

**Impact:** The controller can now host a mix of anonymous and authenticated endpoints. The existing token endpoints remain fully backward-compatible — no real-world behaviour changes. This is a structural prerequisite for the new authenticated test endpoint.

---

### Task 1.1 — New `POST /api/snap/test/{environmentId}` endpoint

**Change:** Added a `TestPurchase` action to `SnapController.cs` decorated with `[Authorize(Policy = "RequireUser")]`. The action:
- Resolves the environment by GUID, returning `404` if not found or soft-deleted
- Verifies the caller owns the environment's application (or is Super Admin); returns `403` otherwise
- Checks the target Midtrans environment is enabled; returns `503` if disabled
- Builds a fixed dummy payload: order ID `test_<8-char-guid>`, gross amount 30,000, three items at 10,000 each
- Delegates to the existing private `CreateTokenAsync` helper — no HTTP call code is duplicated
- Logs the test initiation and stores a `Db_SnapTransaction` record (auditable with `CallerOrderId` starting `test_`)

`UserManager<Db_ApplicationUser>` was injected into the controller to support the Super Admin ownership bypass check.

**Impact:** Admins can verify end-to-end Midtrans connectivity from the dashboard. Test transactions are clearly identifiable in the database and Midtrans dashboard by the `test_` prefix. No new anonymous surface is exposed.

---

### Task 3.1 — `Dto_SnapTokenResponse` type added to frontend

**Change:** Added `Dto_SnapTokenResponse` interface (`{ token: string; redirectUrl: string }`) to `application.types.ts`. It is automatically re-exported via the existing barrel `index.ts`.

**Impact:** Frontend code can type-check against the Snap token response without importing from an ad-hoc location.

---

### Task 2.1 — `testPurchase` API function added to `environment.api.ts`

**Change:** Added `testPurchase(environmentId: string)` to the existing environment API module. It calls `POST /api/snap/test/{environmentId}` using `authenticatedFetch` (JWT cookie forwarded automatically).

**Impact:** The Application Detail page can call the test endpoint with a single import, following the established service-layer pattern.

---

### Tasks 4.1–4.4 — "Test Purchase" button and confirmation dialog on Application Detail page

**Changes:**
- Installed the `alert-dialog` shadcn component (`alert-dialog.tsx`) into the frontend component library
- Added `FlaskConical` icon and `AlertDialog` component imports to `Page_ApplicationDetail.tsx`
- Added two new state variables: `testPurchaseEnv` (which environment's dialog is open) and `testPurchaseLoading` (per-row spinner tracking)
- Added `handleTestPurchase` async handler: closes dialog → sets loading → calls API → opens `redirectUrl` in new tab (`noopener,noreferrer`) on success → shows `toast.error` on failure
- Added "Test Purchase" button to each environment row (before "Regenerate Key"), showing a `Loader2` spinner while loading
- Added an `AlertDialog` modal outside the map loop — shows a neutral "Test Purchase" prompt for Sandbox environments and a destructive "⚠ Real Money Warning" prompt for Production environments

**Impact:** Admins have a one-click flow to verify Midtrans is wired correctly for any environment, with a clear guard against accidentally triggering a live production charge. The UI is consistent with the existing page design (same button sizing, same toast pattern, same dialog styling).
