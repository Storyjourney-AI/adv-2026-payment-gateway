# Execution Plan — task-002.prd001
# Test Purchase Button per Environment

**Status:** Ready for Implementation
**PRD:** task-002.prd001.md

---

## Checklist
- [x] Phase 1: Backend — New Test Endpoint
- [x] Phase 2: Frontend — API Service Function
- [x] Phase 3: Frontend — Types
- [x] Phase 4: Frontend — UI (Test Purchase Button + AlertDialog)

---

## Phase 1: Backend — New Test Endpoint

### Task 1.1 — Add `TestPurchase` action to `SnapController.cs`

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - Change controller-level `[AllowAnonymous]` to per-action (apply `[AllowAnonymous]` on the three existing token actions, remove it from controller level)
  - Add `[HttpPost("test/{environmentId}")]` action decorated with `[Authorize(Policy = "RequireUser")]`
  - Inject `UserManager<Db_ApplicationUser>` — add field and constructor parameter (already used in `ApplicationController.cs` and `EnvironmentController.cs`, pattern is established)
  - Action logic steps:
    1. Extract `userId` from `User.FindFirst("sub_id")?.Value` (consistent pattern across codebase)
    2. Query `m_dbContext.Environments` for `environmentId` where `IsDeleted == false`; return `404 DataWrapper` if not found
    3. Load the environment's Application to verify `application.UserId == userId` OR caller is Super Admin via `m_userManager.IsInRoleAsync`; return `403` if ownership check fails
    4. Determine `isSandbox = env.IsSandbox` (field already exists per prd002; no fallback needed if prd002 ships first)
    5. Select `envOptions` and `midtransUrl` from `m_midtransOptions` based on `isSandbox`
    6. Check `envOptions.IsEnabled`; return `503 DataWrapper` if disabled
    7. Build dummy `Dto_SnapTokenRequest`-equivalent inline:
       - `callerOrderId = "test_" + Guid.NewGuid().ToString("N")[..8]`
       - `GrossAmount = 30000`
       - Three `ItemDetails`: `test_item_1/2/3`, price `10000`, qty `1`
    8. Build `midtransOrderId = environment.Id.ToString("N")[..8] + "_" + callerOrderId`
    9. Skip duplicate-order check (test orders are always unique due to GUID suffix)
    10. Insert `Db_SnapTransaction` record with `CallerOrderId = callerOrderId`
    11. Delegate to the existing private `CreateTokenAsync` helper — **reuse** the shared HTTP call block
  - Add log statement: `m_logger.LogInformation("Test purchase initiated for environment {EnvId} by user {UserId}", environmentId, userId)`

> **Feasibility:** HIGH — The private `CreateTokenAsync` method already encapsulates the Midtrans HTTP call and transaction logging. The ownership check pattern is verbatim from `EnvironmentController`. The only structural change is adding `UserManager` injection and moving `[AllowAnonymous]` to individual actions. No new file, no new service needed.

---

### Task 1.2 — Restructure `[AllowAnonymous]` in `SnapController.cs`

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - Remove `[AllowAnonymous]` from the class declaration
  - Add `[AllowAnonymous]` individually to `CreateToken`, `CreateSandboxToken`, `CreateProductionToken`, and `WebhookController` (if in same file — check)
  - The new `TestPurchase` action must NOT have `[AllowAnonymous]`

> **Feasibility:** HIGH — Mechanical attribute relocation. No runtime logic changes to the existing token endpoints.

---

## Phase 2: Frontend — API Service Function

### Task 2.1 — Add `testPurchase` function to `environment.api.ts`

* Target File: EXISTING `paymentgateway.client/app/services/application/utils/environment.api.ts`
  - Import `Dto_SnapTokenResponse` type (to be added in Phase 3)
  - Add function:
    ```ts
    export async function testPurchase(environmentId: string): Promise<DataWrapper<Dto_SnapTokenResponse>> {
      return authenticatedFetch<Dto_SnapTokenResponse>(
        `/api/snap/test/${environmentId}`,
        { method: "POST" }
      );
    }
    ```
  - Uses the existing `authenticatedFetch` helper (already imported in this file) — no new imports of fetch utility needed

> **Feasibility:** HIGH — Follows identical pattern to all other functions in `environment.api.ts`. `authenticatedFetch` already handles JWT cookie forwarding.

---

## Phase 3: Frontend — Types

### Task 3.1 — Add `Dto_SnapTokenResponse` to `application.types.ts`

* Target File: EXISTING `paymentgateway.client/app/services/application/types/application.types.ts`
  - Add interface:
    ```ts
    export interface Dto_SnapTokenResponse {
      token: string;
      redirectUrl: string;
    }
    ```
  - Matches the backend `Dto_SnapTokenResponse` (`Token` → `token`, `RedirectUrl` → `redirectUrl` via camelCase JSON serialization)

> **Feasibility:** HIGH — Simple interface addition, no conflicts with existing types.

### Task 3.2 — Re-export `Dto_SnapTokenResponse` from barrel `index.ts`

* Target File: EXISTING `paymentgateway.client/app/services/application/index.ts`
  - The barrel already re-exports `./types/application.types` wholesale (`export * from "./types/application.types"`), so no change needed — `Dto_SnapTokenResponse` is automatically re-exported.

> **Feasibility:** HIGH — No action required; barrel re-export covers it.

---

## Phase 4: Frontend — UI (Test Purchase Button + AlertDialog)

### Task 4.1 — Add state for test purchase to `Page_ApplicationDetail.tsx`

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_ApplicationDetail.tsx`
  - Add state variables:
    - `testPurchaseEnv: Dto_EnvironmentResponse | null` — which environment the dialog is open for
    - `testPurchaseLoading: string | null` — the `env.id` currently loading (to show per-row spinner)
  - Import `testPurchase` from `@services/application`
  - Import `Dto_SnapTokenResponse` from `@services/application`
  - Import new Lucide icons: `FlaskConical` (test button icon), `AlertTriangle`
  - Import shadcn `AlertDialog` components: `AlertDialog`, `AlertDialogAction`, `AlertDialogCancel`, `AlertDialogContent`, `AlertDialogDescription`, `AlertDialogFooter`, `AlertDialogHeader`, `AlertDialogTitle` from `~/components/ui/alert-dialog`

> **Feasibility:** HIGH — `alert-dialog` is a standard shadcn component; the project uses shadcn (`components.json` present). State pattern is identical to existing `editingEnv` state. Lucide icons are already used throughout this file.

### Task 4.2 — Add `handleTestPurchase` function

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_ApplicationDetail.tsx`
  - Add handler called after user confirms the AlertDialog:
    ```ts
    const handleTestPurchase = async (env: Dto_EnvironmentResponse) => {
      setTestPurchaseEnv(null);
      setTestPurchaseLoading(env.id);
      try {
        const response = await testPurchase(env.id);
        if (response.success && response.data) {
          window.open(response.data.redirectUrl, "_blank", "noopener,noreferrer");
        } else {
          toast.error(response.message || "Test purchase failed");
        }
      } catch {
        toast.error("An unexpected error occurred");
      } finally {
        setTestPurchaseLoading(null);
      }
    };
    ```

> **Feasibility:** HIGH — Identical async pattern to `handleRegenerateKey` and `handleDelete` already in the file.

### Task 4.3 — Add "Test Purchase" button to each environment row

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_ApplicationDetail.tsx`
  - In the `div.flex.gap-2` action button group inside the `environments.map()` loop, add a new button **before** `Regenerate Key`:
    ```tsx
    <Button
      variant="outline"
      size="sm"
      onClick={() => setTestPurchaseEnv(env)}
      disabled={testPurchaseLoading === env.id}
    >
      {testPurchaseLoading === env.id ? (
        <Loader2 className="h-4 w-4 mr-2 animate-spin" />
      ) : (
        <FlaskConical className="h-4 w-4 mr-2" />
      )}
      Test Purchase
    </Button>
    ```

> **Feasibility:** HIGH — Button slots directly into the existing flex button group. `Loader2` is already imported in the file.

### Task 4.4 — Add `AlertDialog` confirmation modal

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_ApplicationDetail.tsx`
  - Add outside the `environments.map()` loop (alongside the existing Edit `Dialog`):
    ```tsx
    <AlertDialog open={testPurchaseEnv !== null} onOpenChange={(open) => { if (!open) setTestPurchaseEnv(null); }}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {testPurchaseEnv?.isSandbox ? "Test Purchase" : "⚠ Real Money Warning"}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {testPurchaseEnv?.isSandbox
              ? "This will create a test transaction using Midtrans Sandbox. No real money will be charged."
              : "This will create a LIVE Midtrans transaction. Real money will be charged to the payment method. Are you sure?"}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            className={testPurchaseEnv?.isSandbox ? "" : "bg-destructive text-destructive-foreground hover:bg-destructive/90"}
            onClick={() => testPurchaseEnv && handleTestPurchase(testPurchaseEnv)}
          >
            {testPurchaseEnv?.isSandbox ? "Proceed" : "Yes, Proceed"}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
    ```
  - Modal is controlled by `testPurchaseEnv` — no extra `open` boolean needed
  - Production confirm uses destructive styling directly via `className` prop (no custom variant needed)

> **Feasibility:** HIGH — `AlertDialog` is declarative; controlled via state. The `isSandbox` field already exists on `Dto_EnvironmentResponse` (prd002). Pattern matches existing Edit Dialog in the same file.

---

## Implementation Order

0. **Pre-step** — Install `alert-dialog` shadcn component: `cd paymentgateway.client && npx shadcn@latest add alert-dialog`
1. **Task 1.2** — Restructure `[AllowAnonymous]` (prerequisite for Task 1.1)
2. **Task 1.1** — Add `TestPurchase` action in `SnapController.cs`
3. **Task 3.1** — Add `Dto_SnapTokenResponse` type
4. **Task 2.1** — Add `testPurchase` API function
5. **Task 4.1** → **4.2** → **4.3** → **4.4** — Frontend UI in sequence

---

## Notes & Assumptions

- **PRD 002 first:** This plan assumes `IsSandbox` field on `Db_Environment` is already present (prd002 merged). If prd001 ships before prd002, Task 1.1 must add a name-based fallback: `env.Name.ToLower() == "production"` → production, else → sandbox.
- **No new file created on backend:** The test action is added directly to `SnapController.cs` to reuse `CreateTokenAsync`. This avoids duplication per infrastructure rules.
- **No duplicate check:** Test orders always use `Guid.NewGuid()` suffix — collision is statistically impossible. Skipping the duplicate DB guard is intentional.
- **`alert-dialog` shadcn component:** NOT yet installed (confirmed — `alert-dialog.tsx` not found in `app/components/ui/`). Must run `npx shadcn@latest add alert-dialog` inside `paymentgateway.client/` before implementing Task 4.4.
- **`WebhookController.cs` is a separate file** — the `[AllowAnonymous]` restructure only touches `SnapController.cs`.
