# Execution Plan — task-002.prd002
# IsSandbox Flag on Environments + Unified Snap Token Routing

**Status:** Ready for Implementation
**PRD:** task-002.prd002.md

---

## Checklist
- [ ] Phase 1: Database Layer
- [ ] Phase 2: Backend — Entity & DTO
- [ ] Phase 3: Backend — Business Logic (Controllers)
- [ ] Phase 4: Backend — Unified Snap Endpoint
- [ ] Phase 5: EF Core Migration
- [ ] Phase 6: Frontend — Types
- [ ] Phase 7: Frontend — UI Badge

---

## Phase 1: Database Layer

### Task 1.1 — Add `IsSandbox` to `Db_Environment` entity

* Target File: EXISTING `PaymentGateway.Server/Applications/Models/Dbs/Db_Environment.cs`
  - Add `public bool IsSandbox { get; set; } = true;` property after `IsDeleted`
  - Default `true` is intentional — all admin-created environments are sandbox by default

> **Feasibility:** HIGH — Simple property addition to an existing entity class. No dependency issues.

---

### Task 1.2 — `AppDbContext` — no mapping change needed

* Target File: EXISTING `PaymentGateway.Server/Databases/AppDbContext.cs`
  - `bool` non-nullable property with a CLR default is handled automatically by EF Core
  - No fluent API configuration required unless a database default is needed
  - **Note:** Schema is `payment`, not `app` as stated in the PRD. The migration SQL must reference `payment."Environments"`, not `app."Environments"`.

> **Feasibility:** HIGH — No file changes needed; EF Core picks up the property via convention.

---

## Phase 2: Backend — Entity & DTO

### Task 2.1 — Expose `IsSandbox` in `Dto_EnvironmentResponse`

* Target File: EXISTING `PaymentGateway.Server/Applications/Models/Dtos/Dto_EnvironmentResponse.cs`
  - Add `public bool IsSandbox { get; set; }` property

> **Feasibility:** HIGH — Straightforward DTO property addition. All mapping sites will need updating (Phase 3).

---

## Phase 3: Backend — Business Logic (Controllers)

### Task 3.1 — Set `IsSandbox` on auto-created environments in `ApplicationController.CreateApplication`

* Target File: EXISTING `PaymentGateway.Server/Applications/Controllers/ApplicationController.cs`
  - In the `stagingEnv` initializer (around line 233): add `IsSandbox = true`
  - In the `productionEnv` initializer (around line 247): add `IsSandbox = false`
  - In the `Dto_EnvironmentResponse` projection inside `CreateApplication` (around line 275): add `IsSandbox = e.IsSandbox`
  - Also update any other `Dto_EnvironmentResponse` projection in this file (GetApplicationById, etc.) to include `IsSandbox = e.IsSandbox`

> **Feasibility:** HIGH — The environment initializer block already exists. Two `IsSandbox = X` assignments need to be inserted. Projections also need the new field.

---

### Task 3.2 — Force `IsSandbox = true` in `EnvironmentController.CreateEnvironment`

* Target File: EXISTING `PaymentGateway.Server/Applications/Controllers/EnvironmentController.cs`
  - In the `Db_Environment` initializer inside `CreateEnvironment` (around line 215): add `IsSandbox = true`
  - In the `Dto_EnvironmentResponse` response projection (around line 228): add `IsSandbox = environment.IsSandbox`
  - Check any other `Dto_EnvironmentResponse` projections in this file (GetEnvironment, UpdateEnvironment, etc.) and add `IsSandbox = e.IsSandbox` to each

> **Feasibility:** HIGH — Hard-coded `true` assignment enforces server-side policy. No UI exposure required.

---

## Phase 4: Backend — Unified Snap Endpoint

### Task 4.1 — Add unified `POST /api/snap/token` endpoint to `SnapController`

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - Add a new action method `CreateToken` decorated with `[HttpPost("token")]`
  - Logic: look up environment by `X-Api-Key`, read `env.IsSandbox`, then:
    - `IsSandbox = true` → use `m_midtransOptions.Sandbox` + `MidtransSandboxUrl` + webhook `/api/webhook/midtrans/sandbox`
    - `IsSandbox = false` → use `m_midtransOptions.Production` + `MidtransProductionUrl` + webhook `/api/webhook/midtrans/production`
  - Delegate to the existing `CreateTokenAsync` private method (already contains all duplicate-guard, DB logging, and error logic)
  - `midtransEnv` string argument: pass `"sandbox"` or `"production"` based on `env.IsSandbox`

> **Feasibility:** HIGH — The private `CreateTokenAsync` method already encapsulates all shared logic. The new endpoint is a thin dispatcher on top of it. No structural changes needed to the helper.

---

### Task 4.2 — Deprecate old endpoints with mismatch validation

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
  - `CreateSandboxToken`: Add `/// <remarks>Deprecated. Use POST /api/snap/token instead.</remarks>` XML doc; add mismatch guard — if resolved `env.IsSandbox == false`, return `400 BadRequest` with message `"This API key belongs to a production environment. Use /api/snap/token instead."`
  - `CreateProductionToken`: Same pattern — if `env.IsSandbox == true`, return `400 BadRequest` with message `"This API key belongs to a sandbox environment. Use /api/snap/token instead."`
  - The mismatch guard requires resolving the environment **before** calling `CreateTokenAsync`, which means extracting the API key lookup into each deprecated action (or resolving env first and passing it in)

> **Feasibility:** MEDIUM — Requires a small refactor of the two deprecated actions to look up the environment before dispatching to `CreateTokenAsync`. The guard logic is simple, but `CreateTokenAsync` signature may need a minor adjustment to accept an already-resolved `Db_Environment` to avoid double DB queries.

---

## Phase 5: EF Core Migration

### Task 5.1 — Generate and configure the migration

* Target File: NEW `PaymentGateway.Server/Migrations/[timestamp]_add-is-sandbox-to-environment.cs` (auto-generated)
  - Run: `dotnet ef migrations add "add-is-sandbox-to-environment" -c PaymentGateway.Server.Databases.AppDbContext`
  - After generation, edit the `Up()` method to add the raw SQL backfill:
    ```csharp
    migrationBuilder.Sql("UPDATE payment.\"Environments\" SET \"IsSandbox\" = false WHERE \"Name\" = 'production'");
    ```
  - **Important:** The PRD references `app."Environments"` but the actual schema in `AppDbContext.cs` is `payment`. Use `payment."Environments"` in the raw SQL.
  - Verify the `Down()` method drops the column correctly

> **Feasibility:** HIGH — Standard EF Core migration workflow. One manual SQL line needs to be inserted after generation. Schema name discrepancy (`payment` vs `app`) must be corrected.

---

## Phase 6: Frontend — Types

### Task 6.1 — Add `isSandbox` to `Dto_EnvironmentResponse` type

* Target File: EXISTING `paymentgateway.client/app/services/application/types/application.types.ts`
  - Add `isSandbox: boolean;` to the `Dto_EnvironmentResponse` interface

> **Feasibility:** HIGH — One-line addition to a TypeScript interface. No breaking changes; existing usages will now have the field available.

---

## Phase 7: Frontend — UI Badge

### Task 7.1 — Render Sandbox/Production badge on environment card

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_ApplicationDetail.tsx`
  - Import `Badge` from `~/components/ui/badge` (already present in shadcn per `components.json`)
  - In the environment card header (the `<div>` containing `<h3>{env.name}</h3>`), render next to the name:
    ```tsx
    {env.isSandbox
      ? <Badge variant="secondary">Sandbox</Badge>
      : <Badge variant="destructive">Production</Badge>
    }
    ```
  - No form or input change is needed — `isSandbox` is read-only display

> **Feasibility:** HIGH — Badge component is part of the existing shadcn setup. The environment card structure already exists and the insertion point is clear. No state or API changes needed beyond the type update in Phase 6.

---

### Task 7.2 — Verify `Compo_EnvironmentForm` has no `isSandbox` input

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx`
  - Confirm there is no existing `isSandbox` field in the form
  - No changes needed — PRD explicitly states `isSandbox` must NOT be user-settable via the form
  - This task is a verification/audit step only

> **Feasibility:** HIGH — Read-only audit. No code changes required unless an `isSandbox` field was accidentally added.

---

## Implementation Order (Recommended Sequence)

```
Phase 1 (Db entity)
  → Phase 2 (DTO)
  → Phase 3 (Controllers — projections & creation logic)
  → Phase 4 (SnapController — unified endpoint + deprecation)
  → Phase 5 (Migration — generate, then insert backfill SQL)
  → Phase 6 (Frontend types)
  → Phase 7 (Frontend UI badge)
```

Phases 1–5 are back-end only and must be completed (and the migration run successfully) before the frontend changes are meaningful. Phases 6–7 are independent of each other and can be done in any order.

---

## Schema Discrepancy Note

> The PRD's Technical Notes section references the SQL migration as:
> ```sql
> UPDATE app."Environments" SET "IsSandbox" = FALSE WHERE "Name" = 'production';
> ```
> **This is incorrect.** Inspecting `AppDbContext.cs` confirms the `Environments` table lives in the `payment` schema, not `app`. The correct SQL to use in the migration is:
> ```sql
> UPDATE payment."Environments" SET "IsSandbox" = false WHERE "Name" = 'production';
> ```
> All migration tasks in this plan use the corrected schema name.

---

## Files Summary

| File | Action | Phase |
|---|---|---|
| `Applications/Models/Dbs/Db_Environment.cs` | ADD `IsSandbox` property | 1.1 |
| `Databases/AppDbContext.cs` | No change needed | 1.2 |
| `Applications/Models/Dtos/Dto_EnvironmentResponse.cs` | ADD `IsSandbox` property | 2.1 |
| `Applications/Controllers/ApplicationController.cs` | SET `IsSandbox` on env init + update projections | 3.1 |
| `Applications/Controllers/EnvironmentController.cs` | FORCE `IsSandbox = true` + update projections | 3.2 |
| `Midtrans/Controllers/SnapController.cs` | ADD unified `/api/snap/token` + deprecation guards | 4.1, 4.2 |
| NEW Migration file | GENERATE via dotnet ef + insert backfill SQL | 5.1 |
| `paymentgateway.client/…/application.types.ts` | ADD `isSandbox: boolean` to interface | 6.1 |
| `paymentgateway.client/…/Page_ApplicationDetail.tsx` | ADD `<Badge>` per environment | 7.1 |
| `paymentgateway.client/…/Compo_EnvironmentForm.tsx` | VERIFY no `isSandbox` input | 7.2 |
