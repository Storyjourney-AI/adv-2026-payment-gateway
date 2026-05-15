# Review Report — task-002 (prd002, prd003) + task-003 (prd001)
**Review Date:** 2026-03-11  
**Reviewer:** GitHub Copilot (c-reviewer)  
**Source Documents:**  
- `task-002.prd001.md` — Test Purchase button per environment  
- `task-002.prd002.md` — IsSandbox flag + unified Snap token endpoint  
- `task-002.prd003.md` — Webhook flow hardening (X-Custom-Notification)  
- `task-003.prd001.md` — Profile page + seed user update  

---

## Overview

This review covers the implementation produced for milestone task-002 (all three PRDs) and task-003 (prd001). The persona is the **Super Admin** — the sole authenticated user of the dashboard.

Three of the four PRDs are substantially complete with minor gaps. **One critical gap exists** that breaks the core Midtrans webhook flow: the `X-Custom-Notification` notification routing header is computed but never actually sent to Midtrans, meaning no payment notification will ever arrive at the gateway. An additional database migration default value is incorrect. Both are actionable bugs in existing backend files.

---

## PRD Validation

### task-002 prd002 — IsSandbox Flag + Unified Snap Token

| # | Requirement | Status | Notes |
|---|---|---|---|
| 1 | `Db_Environment.IsSandbox` property exists | ✅ Implemented | |
| 2 | EF Core migration created with correct defaults | ⚠️ Partially Implemented | Migration sets `defaultValue: false` for all rows; PRD requires default `true` (sandbox) for all non-production rows. The raw SQL to flip existing production rows to `false` is also absent. |
| 3 | `Dto_EnvironmentResponse` exposes `IsSandbox` (backend + frontend) | ✅ Implemented | `application.types.ts` line 44: `isSandbox: boolean` |
| 4 | `ApplicationController.CreateApplication` sets staging→`true`, production→`false` | ✅ Implemented | Lines 243, 258 confirmed |
| 5 | `EnvironmentController.CreateEnvironment` always forces `IsSandbox = true` | ✅ Implemented | Line 220 hardcodes `IsSandbox = true` |
| 6 | `IsSandbox` is NOT user-settable via create/edit form | ❌ Not Implemented | `UpdateEnvironment` (line 308–310) accepts and applies `request.IsSandbox`; frontend form also exposes a toggle on edit |
| 7 | Unified `POST /api/snap/token` endpoint | ✅ Implemented | Reads `env.IsSandbox` correctly |
| 8 | Deprecated `sandbox/token` and `production/token` endpoints still functional with mismatch guard | ✅ Implemented | Returns `400` on env type mismatch |
| 9 | Deprecated endpoints marked with `[Obsolete]` attribute | ⚠️ Partially Implemented | XML `<remarks>Deprecated...</remarks>` doc comment present; no C# `[Obsolete]` attribute applied (no tooling warning) |
| 10 | Badge (`Sandbox`/`Production`) on environment row | ✅ Implemented | `Page_ApplicationDetail.tsx` renders badges correctly |
| 11 | `Compo_EnvironmentForm` does NOT expose `IsSandbox` toggle | ❌ Not Implemented | Edit form renders a toggle switch for `IsSandbox` and sends it in the PUT payload |

### task-002 prd003 — Exclusive Webhook Routing (X-Custom-Notification)

| # | Requirement | Status | Notes |
|---|---|---|---|
| 1 | Change `X-Override-Notification` → `X-Custom-Notification` header in Midtrans request | ❌ Not Implemented | **Neither** header is sent. The `webhookCallbackUrl` parameter is computed and passed to `CreateTokenAsync` but is never added to the outgoing HTTP request. The variable is dead. |
| 2 | `webhookCallbackUrl` construction remains: `{BaseUrl}/api/webhook/midtrans/{sandbox\|production}` | ❌ Not Implemented | URL is computed but unused; additionally the path format `api/webhook/midtrans/{env}` doesn't match the actual webhook controller routes (see task-003). |
| 3 | No functional changes to `WebhookController.cs` | ✅ Implemented | |
| 4 | `IsWebhookUrlSafe()` SSRF guard confirmed active | ✅ Implemented | Lines 130–146: checks `https://` and non-loopback |
| 5 | Failed forward logs error, always returns `200 OK` to Midtrans | ✅ Implemented | |
| 6 | Null/empty `WebhookUrl` gracefully skipped, returns `200 OK` | ✅ Implemented | |

### task-002 prd001 — Test Purchase Button

| # | Requirement | Status | Notes |
|---|---|---|---|
| 1 | `POST /api/snap/test/{environmentId}` endpoint exists, requires JWT | ✅ Implemented | `[Authorize(Policy = "RequireUser")]` |
| 2 | Returns `404` if environment not found or soft-deleted | ✅ Implemented | |
| 3 | Ownership check (owner or Super Admin) | ✅ Implemented | |
| 4 | Fixed dummy payload with 3 items x 10,000 = 30,000 | ✅ Implemented | |
| 5 | `OrderId = "test_{Guid.NewGuid():N[..8]}"` | ✅ Implemented | `callerOrderId = "test_" + Guid.NewGuid().ToString("N")[..8]` |
| 6 | Routes via `IsSandbox` flag | ✅ Implemented | |
| 7 | Returns `503` if Midtrans environment is disabled | ✅ Implemented | |
| 8 | Logs as `Db_SnapTransaction` | ✅ Implemented | Passes through `CreateTokenAsync` which inserts the record |
| 9 | "Test Purchase" button on each environment row | ✅ Implemented | `FlaskConical` icon |
| 10 | `AlertDialog` confirmation (not Toast) | ✅ Implemented | |
| 11 | Sandbox dialog: neutral warning, "Proceed" | ✅ Implemented | |
| 12 | Production dialog: destructive warning, "Yes, Proceed" | ✅ Implemented | |
| 13 | Loading state during API call | ✅ Implemented | `Loader2` spinner |
| 14 | On success: open `redirectUrl` in new tab with `noopener,noreferrer` | ✅ Implemented | |
| 15 | On `503`: greyed-out button with tooltip | ✅ Implemented | `disabledEnvIds` state |
| 16 | On error: `toast.error` | ✅ Implemented | |

### task-003 prd001 — Profile Page + Seed User Update

| # | Requirement | Status | Notes |
|---|---|---|---|
| 1 | Seed email changed to `technical@advine.id` | ✅ Implemented | `AuthService.cs` line 558 |
| 2 | `POST /api/auth/change-password` is `[Authorize]`, unchanged | ✅ Implemented | |
| 3 | `Page_Profile.tsx` at route `dashboard/profile` | ✅ Implemented | |
| 4 | Displays logged-in user's email (read-only) | ✅ Implemented | Sourced from `useAuth().user.email` |
| 5 | Three-field form: currentPassword, newPassword, confirmPassword | ✅ Implemented | All `type="password"` |
| 6 | Client-side confirm password mismatch guard | ✅ Implemented | |
| 7 | `toast.success` + form reset on success | ✅ Implemented | |
| 8 | `toast.error` on failure | ✅ Implemented | |
| 9 | Loading state on submit button | ✅ Implemented | `Loader2` |
| 10 | Password policy hint below new password field | ✅ Implemented | |
| 11 | Route added in `routes.ts` inside `Layout_Protected > Layout_Dashboard` | ✅ Implemented | |
| 12 | "Profile" `SidebarMenuItem` with `UserCog` icon at `/dashboard/profile` | ✅ Implemented | |

---

## Userflow Validation

### Userflow A — Admin creates an application and verifies integration

| Step | Description | Status | Notes |
|---|---|---|---|
| 1 | Admin navigates to Applications | ✅ Covered | |
| 2 | Creates new application → auto-generates staging (sandbox) + production environments | ✅ Covered | IsSandbox flags set correctly |
| 3 | Sees Sandbox/Production badge on each environment row | ✅ Covered | |
| 4 | Clicks "Test Purchase" on staging → sandbox warning dialog | ✅ Covered | |
| 5 | Confirms → Midtrans Sandbox payment page opens | ⚠️ Partially Covered | The API call succeeds and the token/URL are returned, but the `X-Custom-Notification` header is never sent to Midtrans — Midtrans will not reliably send the payment notification back to the gateway after payment completion |
| 6 | Midtrans sends webhook → gateway verifies signature + updates DB + forwards to child app | ❌ Not Covered | Webhook will only arrive if no URL is configured in Midtrans dashboard (since no notification header is sent); gateway is not the exclusive recipient |

### Userflow B — Admin changes password

| Step | Description | Status | Notes |
|---|---|---|---|
| 1 | Admin navigates to Profile via sidebar | ✅ Covered | |
| 2 | Sees their email displayed | ✅ Covered | |
| 3 | Fills change-password form | ✅ Covered | |
| 4 | Submits with wrong current password → toast.error with errors | ✅ Covered | |
| 5 | Submits with mismatched confirm → inline error, no API call | ✅ Covered | |
| 6 | Submits correctly → toast.success + form reset | ✅ Covered | |

### Userflow C — Child app calls unified snap token endpoint

| Step | Description | Status | Notes |
|---|---|---|---|
| 1 | Child calls `POST /api/snap/token` with `X-Api-Key` | ✅ Covered | |
| 2 | Gateway looks up env by API key, reads `IsSandbox` | ✅ Covered | |
| 3 | Gateway routes to correct Midtrans tier | ✅ Covered | |
| 4 | Gateway sends `X-Custom-Notification` to force itself as sole webhook recipient | ❌ Not Covered | Header is never sent |
| 5 | Token + redirect URL returned to child app | ✅ Covered | |

---

## Security Findings

### 🔴 Critical — Missing X-Custom-Notification Header Breaks Webhook Integrity

- **Location:** [PaymentGateway.Server/Midtrans/Controllers/SnapController.cs](PaymentGateway.Server/Midtrans/Controllers/SnapController.cs) — `CreateTokenAsync` method (lines 393–401)
- **Issue:** The `webhookCallbackUrl` parameter is received by `CreateTokenAsync` but never attached to the `HttpRequestMessage` sent to Midtrans. No notification routing header (`X-Custom-Notification` or `X-Override-Notification`) is present. Midtrans will only fire the webhook based on its own dashboard configuration. If no dashboard webhook URL is configured, Midtrans silently discards the notification — gateway DB will never be updated and the child app will never be notified.
- **Suggestion:** Add the following line to the HTTP request construction block, immediately after the Authorization header:
  ```csharp
  httpRequest.Headers.Add("X-Custom-Notification", webhookCallbackUrl);
  ```
  Also update `webhookCallbackUrl` construction to use the correct path matching the current `WebhookController` routes:
  - Sandbox: `{BaseUrl}/api/midtrans/sandbox/payment`
  - Production: `{BaseUrl}/api/midtrans/payment`

---

### 🔴 Critical — IsSandbox Mutable via PUT Endpoint Bypasses Server Enforcement

- **Location:** [PaymentGateway.Server/Applications/Controllers/EnvironmentController.cs](PaymentGateway.Server/Applications/Controllers/EnvironmentController.cs) — `UpdateEnvironment` (lines 308–310)
- **Issue:** The PRD designates `IsSandbox` as server-enforced and immutable after creation. However, the `UpdateEnvironment` endpoint applies `request.IsSandbox` if provided. Any authenticated user with ownership of an application can silently flip an environment from Sandbox to Production (or vice versa) by sending a crafted PUT request — this could result in live Midtrans charges on what was intended as a test environment.
- **Suggestion:** Remove `IsSandbox` from `Dto_EnvironmentRequest` entirely, or ignore it in `UpdateEnvironment`:
  ```csharp
  // Remove lines 308–310 — IsSandbox is immutable after creation
  ```

---

### 🟠 High — Frontend Edit Form Exposes IsSandbox Toggle

- **Location:** [paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx](paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx) (lines 163–193)
- **Issue:** The edit form renders a toggle switch for `isSandbox` and includes it in the PUT payload (`...(isEditing && { isSandbox })`). A user who doesn't understand the implications could inadvertently switch a Production environment to Sandbox — silencing real payments — or vice versa.
- **Suggestion:** Remove the toggle section entirely. The `isSandbox` value should only appear as a read-only badge (already shown on the environment row in `Page_ApplicationDetail.tsx`).

---

### 🟠 High — Webhook Callback URL Path Mismatch

- **Location:** [PaymentGateway.Server/Midtrans/Controllers/SnapController.cs](PaymentGateway.Server/Midtrans/Controllers/SnapController.cs) — lines 77, 119, 199
- **Issue:** The `webhookCallbackUrl` is constructed as `{BaseUrl}/api/webhook/midtrans/{env}`, but the actual `WebhookController` routes (updated in task-003) now live at:
  - Sandbox: `/api/midtrans/sandbox/payment`
  - Production: `/api/midtrans/payment`
  Even after fixing the missing header, Midtrans would POST to a `404` endpoint.
- **Suggestion:** Update the URL construction in all three locations:
  ```csharp
  // Sandbox:
  var webhookCallbackUrl = $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/sandbox/payment";
  // Production:
  var webhookCallbackUrl = $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/payment";
  ```
  Or conditionally:
  ```csharp
  var webhookCallbackUrl = environment.IsSandbox
      ? $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/sandbox/payment"
      : $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/payment";
  ```

---

### 🟡 Medium — Migration Default Value Incorrect for IsSandbox

- **Location:** [PaymentGateway.Server/Migrations/20260311085017_add-is-sandbox-to-environment.cs](PaymentGateway.Server/Migrations/20260311085017_add-is-sandbox-to-environment.cs)
- **Issue:** The migration sets `defaultValue: false` (not sandbox) for all existing rows. The PRD specifies the default should be `true` (sandbox), with a separate SQL statement to flip rows where `name = 'production'` to `false`. With the current migration, any pre-existing environments (other than production) would be incorrectly marked as Production, routing real Midtrans charges.
- **Suggestion:** Correct the migration's `Up()` method:
  ```csharp
  migrationBuilder.AddColumn<bool>(
      name: "IsSandbox",
      schema: "payment",
      table: "Environments",
      type: "boolean",
      nullable: false,
      defaultValue: true);   // ← was false

  migrationBuilder.Sql(
      "UPDATE payment.\"Environments\" SET \"IsSandbox\" = false WHERE \"Name\" = 'production'");
  ```
  **Note:** If this migration has already been applied to production, a follow-up data migration is required.

---

### 🟡 Medium — change-password Does Not Validate Email Ownership

- **Location:** [PaymentGateway.Server/Authorization/Controllers/AuthController.cs](PaymentGateway.Server/Authorization/Controllers/AuthController.cs) — `ChangePasswordAsync` handler
- **Issue:** The endpoint is `[Authorize]` but accepts the target `email` in the request body. An authenticated user can technically supply a different user's email. The PRD notes this is low risk given the single-user system, but it is an authorization gap (Broken Access Control — OWASP A01).
- **Suggestion:** Ignore the `email` from the request body; derive it from the JWT instead:
  ```csharp
  var email = User.FindFirst(ClaimTypes.Email)?.Value
             ?? User.FindFirst("email")?.Value;
  ```

---

### 🟢 Low — Deprecated Endpoints Lack [Obsolete] Attribute

- **Location:** [PaymentGateway.Server/Midtrans/Controllers/SnapController.cs](PaymentGateway.Server/Midtrans/Controllers/SnapController.cs) — `CreateSandboxToken`, `CreateProductionToken`
- **Issue:** PRD002 requires adding the `[Obsolete]` C# attribute. Only an XML `<remarks>` doc comment is present. Without the attribute, no compile-time warning is emitted if these methods are referenced internally.
- **Suggestion:** Add `[Obsolete("Use POST /api/snap/token instead.")]` above each action.

---

## UI/UX Findings

### 🟠 Major — IsSandbox Toggle in Edit Form Creates User Confusion

- **Persona:** Super Admin
- **Location:** [paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx](paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx)
- **Issue:** A toggle switch labeled "Midtrans Environment" appears when editing an environment. This implies the admin can freely switch between Sandbox and Production, which is architecturally prohibited. A new admin could switch a live Production environment to Sandbox, silencing real payment notifications without knowing the consequences.
- **Suggestion:** Remove the toggle from the edit form. The Sandbox/Production classification should only be visible as a read-only badge (already present on the environment card). If display in the edit form is desired, render a non-interactive badge instead.

---

### 🟡 Minor — Error Feedback in EnvironmentForm Uses `alert()` Instead of Toast

- **Persona:** Super Admin
- **Location:** [paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx](paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx) — `onSubmit` catch blocks (lines 63, 66)
- **Issue:** Error cases use `alert(response.message)` and `alert("An unexpected error occurred")`, which creates a jarring browser-native modal inconsistent with the rest of the dashboard (which uses `sonner` toasts).
- **Suggestion:** Replace `alert()` calls with `toast.error(...)` from `sonner` for consistent UX.

---

### 🟢 Enhancement — Profile Page Has No Link Back to Dashboard

- **Persona:** Super Admin
- **Location:** [paymentgateway.client/app/routes/dashboard/Page_Profile.tsx](paymentgateway.client/app/routes/dashboard/Page_Profile.tsx)
- **Issue:** Minor navigational friction: after changing a password (especially if a reset flow is needed), there is no back-link or breadcrumb. The sidebar remains accessible, but a visual navigation hint reduces cognitive load.
- **Suggestion:** Add a breadcrumb or a `← Dashboard` back-link at the top of the page, matching the pattern used on `Page_ApplicationDetail.tsx`.

---

## Overall Verdict

### **Pass with Conditions**

The implementation is structurally sound and functionally complete for most requirements. The Profile page, Test Purchase feature, and IsSandbox routing architecture are production-quality. However, **two critical bugs and one high security issue must be resolved before shipping:**

| Priority | Item | File | Action |
|---|---|---|---|
| 🔴 Critical | Add `X-Custom-Notification` header to Midtrans HTTP request | `SnapController.cs` — `CreateTokenAsync` | Add 1 line |
| 🔴 Critical | Fix webhook callback URL path to match current `WebhookController` routes | `SnapController.cs` — 3 locations | Update URL string |
| 🔴 Critical | Block `IsSandbox` mutation in `UpdateEnvironment` | `EnvironmentController.cs` | Remove 3 lines |
| 🟠 High | Remove `IsSandbox` toggle from edit form | `Compo_EnvironmentForm.tsx` | Remove ~30 lines |
| 🟡 Medium | Fix migration default value (`false` → `true`) + add SQL flip for production rows | Migration file | 2 line fix + 1 SQL statement |
| 🟡 Medium | Derive email from JWT in `change-password` endpoint | `AuthController.cs` | 2 line fix |
