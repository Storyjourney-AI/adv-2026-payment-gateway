# [Feature] IsSandbox Flag on Environments + Unified Snap Token Routing

**Labels:** `feature` `backend` `database` `breaking-change` `midtrans`
**Milestone:** task-002
**Priority:** High — blocks PRD 001 (Test Purchase) full implementation
**Related:** task-001 (Midtrans Snap Integration), task-002.prd001

---

## Summary

Add an `IsSandbox` boolean column to `Db_Environment`. This flag becomes the **canonical indicator** of whether an environment routes to Midtrans Sandbox or Midtrans Production — replacing the current implicit coupling between URL path (`/sandbox/token` vs `/production/token`) and Midtrans environment selection. Introduce a single unified token endpoint `POST /api/snap/token` that reads `IsSandbox` from the matched environment. Auto-creation rules ensure consistency: the system-created "production" environment is always `IsSandbox = false`, "staging" is always `IsSandbox = true`, and **all admin-created environments are unconditionally `IsSandbox = true`**.

---

## Background / Context

### Current State

The `Db_Environment` entity has no awareness of whether it maps to a Midtrans sandbox or production context. The current routing is entirely endpoint-driven:

| Endpoint child app calls | Midtrans target |
|---|---|
| `POST /api/snap/sandbox/token` | Midtrans Sandbox |
| `POST /api/snap/production/token` | Midtrans Production |

This is ambiguous for the following reasons:
1. A "staging" environment registered by one child app and the "sandbox" call path are loosely coupled only by convention — there is nothing in the DB enforcing this.
2. Any environment can call either endpoint — a production-named environment could accidentally call `/sandbox/token` (and vice versa) with no guard.
3. Midtrans only provides two API environments: **Sandbox** and **Production**. Exposing two separate gateway endpoints for this is unnecessary complexity once `IsSandbox` provides the same routing signal at the environment level.
4. When the dashboard later lists environments, there is no visual indicator telling the admin whether an environment hits real Midtrans payments or test.

### Midtrans Reality

Midtrans provides exactly two environments:
- **Sandbox** — test transactions, fake money, dedicated server key
- **Production** — live transactions, real money, dedicated server key

These are captured in `appsettings.development.json` under `Midtrans.Sandbox` and `Midtrans.Production`. The gateway needs to keep supporting both, but the _selection_ should move from the endpoint URL to the environment's `IsSandbox` property.

---

## Problem Statement

1. There is no enforceable database-level constraint linking an environment to a Midtrans tier.
2. Child apps must know which endpoint path (`/sandbox` or `/production`) to call, creating a leaky abstraction — the gateway should abstract this away entirely.
3. Admin-created extra environments have no default or safeguard — they could accidentally be used for production calls.
4. The dashboard shows no visual distinction between sandbox and production environments.

---

## User Stories

**US-1 — Correct routing by convention:**
> As a developer integrating with the gateway, I want to call a single endpoint `POST /api/snap/token` and have the gateway determine whether my request goes to Midtrans Sandbox or Production based on the API key I supply — so I do not need to manage two different endpoint URLs.

**US-2 — Safe defaults for new environments:**
> As an admin creating additional environments beyond the default staging/production pair, I want those environments to be sandbox by default — so I never accidentally route test workloads through Midtrans Production.

**US-3 — Visual environment classification:**
> As an admin viewing the Application Detail page, I want each environment to display a "Sandbox" or "Production" badge — so I can immediately tell which tier each API key connects to.

**US-4 — Auto-correct on application creation:**
> As an admin creating a new application, I want the auto-created "staging" environment to be sandbox and the auto-created "production" environment to be live, without any extra configuration — so the naming is in sync with the actual Midtrans routing.

---

## Acceptance Criteria

### Database

- [ ] `Db_Environment` entity gains `bool IsSandbox { get; set; }` property
- [ ] EF Core migration created: column `IsSandbox` added to `app.Environments` table
  - Default value for existing rows set by migration: `Name = 'production'` → `false`, all others → `true`
  - SQL migration note: `ALTER TABLE app."Environments" ADD COLUMN "IsSandbox" BOOLEAN NOT NULL DEFAULT TRUE; UPDATE app."Environments" SET "IsSandbox" = FALSE WHERE "Name" = 'production';`
- [ ] `Dto_EnvironmentResponse` exposes `IsSandbox` (backend + frontend types)

### Application / Environment Creation Logic

- [ ] `ApplicationController.CreateApplication`: auto-created environments set explicitly:
  - `staging` → `IsSandbox = true`
  - `production` → `IsSandbox = false`
- [ ] `EnvironmentController.CreateEnvironment`: any admin-added environment always gets `IsSandbox = true`, regardless of the `Name` field value
  - The `IsSandbox` field is **not** user-settable via the create/edit form — it is server-enforced
  - Only the system-created `production` environment ever has `IsSandbox = false`
- [ ] No UI control for `IsSandbox` in the environment create/edit form (it is read-only, display-only)

### Snap Token Endpoint

- [ ] New endpoint: `POST /api/snap/token` (`[AllowAnonymous]`, `X-Api-Key` header auth, as per existing pattern)
  - Looks up environment by `X-Api-Key`
  - Reads `env.IsSandbox`:
    - `true` → uses `MidtransOptions.Sandbox` + `https://app.sandbox.midtrans.com/snap/v1/transactions`
    - `false` → uses `MidtransOptions.Production` + `https://app.midtrans.com/snap/v1/transactions`
  - Sets `X-Override-Notification` (or `X-Custom-Notification` per PRD 003) to match: sandbox → `/api/webhook/midtrans/sandbox`, production → `/api/webhook/midtrans/production`
  - All other logic (duplicate guard, DB logging, error handling) identical to existing endpoint pair
- [ ] Existing endpoints `POST /api/snap/sandbox/token` and `POST /api/snap/production/token` remain functional for backward compatibility but are **deprecated**:
  - Add `[Obsolete]` XML doc comment to each action
  - These endpoints use the IsSandbox-aware logic too (read env's IsSandbox but still validate it matches the called path; if mismatched, return `400 Bad Request` with message `"This API key belongs to a [sandbox/production] environment. Use /api/snap/token instead."`)

### Frontend

- [ ] `Dto_EnvironmentResponse` in `application.types.ts` gains `isSandbox: boolean`
- [ ] Environment row on `Page_ApplicationDetail` shows a badge:
  - `IsSandbox = true` → `<Badge variant="secondary">Sandbox</Badge>`
  - `IsSandbox = false` → `<Badge variant="destructive">Production</Badge>` (or a suitable distinct style)
- [ ] `Compo_EnvironmentForm` (create/edit) does **not** expose an `IsSandbox` toggle — it is display-only information

---

## Technical Notes

### DB Migration

Run after code change:
```
dotnet ef migrations add "add-is-sandbox-to-environment" -c PaymentGateway.Server.Databases.AppDbContext
```

In the migration's `Up()` method, after adding the column with `defaultValue: true`, add a raw SQL call to flip existing production rows:
```csharp
migrationBuilder.Sql("UPDATE app.\"Environments\" SET \"IsSandbox\" = false WHERE \"Name\" = 'production'");
```

### Endpoint Routing Decision

The gateway abstracts Midtrans' environment split from child apps. Child apps should **never** need to know which Midtrans API they are calling — that is the gateway's concern. The unified endpoint design enforces this:

```
Child App                 Gateway                        Midtrans
─────────                 ───────                        ────────
POST /api/snap/token  ──► lookup env by X-Api-Key
                          read env.IsSandbox
                          if true  ──────────────────► Sandbox API
                          if false ─────────────────► Production API
```

### Why IsSandbox is immutable after creation

Once an environment is tied to real transactions or sandbox tests, changing its Midtrans tier would orphan the existing `Db_SnapTransaction` rows (their `MidtransEnv` column would no longer match). To avoid this:
- System-created `production` and `staging` envs are seeded correctly on application creation
- User-created envs are always sandbox — if a user needs a production env, they use the auto-created one
- This rule is enforced server-side (not configurable through any API endpoint)

### Webhook URL in X-Override-Notification

With the unified endpoint, the gateway must still route webhooks to the correct server key for signature verification. Continue sending the env-specific webhook path:
```
IsSandbox = true  → X-Override-Notification: {BaseUrl}/api/webhook/midtrans/sandbox
IsSandbox = false → X-Override-Notification: {BaseUrl}/api/webhook/midtrans/production
```

See PRD 003 for the change from `X-Override-Notification` to `X-Custom-Notification`.

### Files to Modify

| File | Change |
|---|---|
| `Midtrans/Models/Dbs/Db_Environment.cs` → actually `Applications/Models/Dbs/Db_Environment.cs` | Add `IsSandbox` property |
| `Databases/AppDbContext.cs` | Ensure `IsSandbox` needs no special mapping (bool, non-nullable, default true) |
| `Applications/Models/Dtos/Dto_EnvironmentResponse.cs` | Add `IsSandbox` |
| `Applications/Controllers/ApplicationController.cs` | Set `IsSandbox` on auto-created environments |
| `Applications/Controllers/EnvironmentController.cs` | Force `IsSandbox = true` on create |
| `Midtrans/Controllers/SnapController.cs` | Add unified `/api/snap/token`, deprecate existing two endpoints |
| `paymentgateway.client/.../application.types.ts` | Add `isSandbox: boolean` to `Dto_EnvironmentResponse` |
| `paymentgateway.client/.../Page_ApplicationDetail.tsx` | Render sandbox/production badge |
| New migration file | `add-is-sandbox-to-environment` |

---

## Out of Scope

- Allowing admins to flip `IsSandbox` on an existing environment via UI or API
- Removing the old `/sandbox/token` and `/production/token` endpoints (kept for backward compat)
- Migrating existing `Db_SnapTransaction` rows when `IsSandbox` changes (not allowed by design)
- Multi-currency or non-IDR sandbox considerations

---

## Definition of Done

- [ ] `Db_Environment.IsSandbox` column exists in DB after migration
- [ ] Existing `production` rows have `IsSandbox = false`, all others `true`
- [ ] `POST /api/snap/token` routes to correct Midtrans tier based on `IsSandbox`
- [ ] New environments created via the dashboard always have `IsSandbox = true`
- [ ] Application creation auto-seeds staging (`IsSandbox = true`) and production (`IsSandbox = false`)
- [ ] Frontend badge visible per environment on Application Detail page
- [ ] Old endpoints still return valid responses (backward compat)
