## Checklist
- [x] Phase 1: Backend — Exclusive Webhook Routing Fix
- [x] Phase 2: Audit — Confirm Existing Infrastructure

---

## Feasibility Assessment

| Task | Feasibility | Notes |
|---|---|---|
| 1.1 Replace header name | ✅ HIGH | Single string token change in one place; no logic, no side effects |
| 2.1–2.6 Audits | ✅ HIGH | Read-only confirmation; already verified in code review |
| Build validation | ✅ HIGH | No new types, no migrations, no new dependencies |

No migrations, no new files, no frontend changes. Completely aligned with infrastructure rules: controller-only change, no service layer needed for a one-line fix.

---

## Phase 1: Backend — Exclusive Webhook Routing Fix

### Backend Implementation

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
    - **Task 1.1**: In `CreateTokenAsync`, change:
      ```csharp
      httpRequest.Headers.Add("X-Override-Notification", webhookCallbackUrl);
      ```
      to:
      ```csharp
      httpRequest.Headers.Add("X-Custom-Notification", webhookCallbackUrl);
      ```
    - This single line change affects all token creation paths (sandbox, production, unified from PRD002, and test purchase from PRD001) because they all delegate to `CreateTokenAsync`.
    - No other code changes required.

---

## Phase 2: Audit — Confirm Existing Infrastructure

All items in this phase are **read-only audits** — no code changes. They confirm that the order_id routing mechanism and SSRF guards are already correct.

### Confirm Order ID Routing Chain

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
    - **Task 2.1 (Confirm)**: Verify `midtransOrderId` is constructed as `{envId[0..8]}_{callerOrderId}` and saved into `Db_SnapTransaction.MidtransOrderId` before calling Midtrans.
    - **Task 2.2 (Confirm)**: Verify duplicate `MidtransOrderId` guard is in place (prevents re-use).
    - Status: ✅ Already confirmed — `line 255` and `line 259` in current code.

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs`
    - **Task 2.3 (Confirm)**: Verify `HandleWebhookAsync` looks up `Db_SnapTransaction` by `MidtransOrderId` (from Midtrans `order_id` field), includes `Environment`, and reads `Environment.WebhookUrl`.
    - **Task 2.4 (Confirm)**: Verify `IsWebhookUrlSafe()` is called before forwarding (SSRF guard — must be `https://` and non-loopback).
    - **Task 2.5 (Confirm)**: Verify that a failed forward to child app (exception or non-2xx) is only logged — gateway always returns `200 OK` to Midtrans regardless.
    - **Task 2.6 (Confirm)**: Verify that a null/empty `WebhookUrl` is handled gracefully (log + skip, still returns `200 OK`).
    - Status: ✅ All confirmed — `lines 108-170` in current code.

---

## Build Validation

* Run `dotnet build` from `PaymentGateway.Server/` after Task 1.1.
* No frontend changes — no TypeScript check required.

---

## Summary

| Task | File | Type | Lines Changed |
|---|---|---|---|
| 1.1 Replace header name | `SnapController.cs` | Code change | 1 |
| 2.1–2.6 | `SnapController.cs`, `WebhookController.cs` | Audit (no change) | 0 |

**Total code change: 1 line.**
