# Review Report — Task-006: Order ID Scoping — Enforce API Key Ownership & Composite Uniqueness

**Review Date:** 2025-07-15
**Source Documents:** `task-006.prd001.md`, `task-006.prd001-userflow.md`, `task-006.prd001-execution-plan.md`
**Completion Summary:** `task-006.prd001-completion.md`
**Reviewer:** Automated Code Review Agent

---

## Overview

Task-006 hardens order ID handling across the Snap payment gateway by:
1. Adding a composite unique database index on `(EnvironmentId, CallerOrderId)` to enforce per-tenant uniqueness
2. Updating the duplicate check in `CreateTokenAsync` from a `MidtransOrderId`-based query to a semantic `(EnvironmentId, CallerOrderId)` query
3. Adding `DbUpdateException` catch for race condition handling, surfacing as `409 Conflict`

**Personas identified:**
- **Child App Developer** — integrates with the payment gateway via `X-Api-Key`
- **Gateway Operator** — manages the system and expects tenant isolation guarantees

**Files changed (3):**
- `PaymentGateway.Server/Databases/AppDbContext.cs` — composite unique index declaration
- `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs` — duplicate check + race condition handling
- `PaymentGateway.Server/Migrations/migrations.md` — migration instructions

---

## PRD Validation

### Database — EF Core Migration

| # | Acceptance Criterion | Status | Details |
|---|---------------------|--------|---------|
| 1 | A new migration file is created that adds `CREATE UNIQUE INDEX "IX_SnapTransactions_EnvironmentId_CallerOrderId"` on `(EnvironmentId, CallerOrderId)` | ⚠️ Partially Implemented | The index is correctly declared in `AppDbContext.ConfigureMidtransSchema` via fluent API (`.HasIndex(t => new { t.EnvironmentId, t.CallerOrderId }).IsUnique().HasDatabaseName("IX_SnapTransactions_EnvironmentId_CallerOrderId")`). However, **no actual migration `.cs` file was generated** — only instructions in `migrations.md`. The `AppDbContextModelSnapshot.cs` does NOT contain the new index, meaning the EF Core model is out of sync. All prior tasks (task-001, task-002) committed their migration files. |
| 2 | The migration is additive — no existing data at risk | ✅ Implemented | The existing `MidtransOrderId` unique constraint prevents `(EnvironmentId, CallerOrderId)` duplicates in practice, so the new index can be applied safely to a populated database. |
| 3 | `dotnet ef migrations add` and `dotnet ef database update` succeed cleanly | ⚠️ Partially Implemented | Commands are documented in `migrations.md` but were not executed as part of the commit. The migration file and updated snapshot are deferred to deployment time. |

### Backend — `SnapController.CreateTokenAsync` (Duplicate Check Update)

| # | Acceptance Criterion | Status | Details |
|---|---------------------|--------|---------|
| 4 | Replace `MidtransOrderId`-based duplicate check with `(EnvironmentId, CallerOrderId)` check | ✅ Implemented | Line 527–528: `m_dbContext.SnapTransactions.AnyAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == request.OrderId)` — exact match to PRD specification. |
| 5 | 409 Conflict response message: `"A transaction with OrderId '{orderId}' already exists for this environment."` | ✅ Implemented | Line 530–531: Message matches exactly. |
| 6 | `MidtransOrderId` UNIQUE constraint remains as secondary safety net | ✅ Implemented | `AppDbContext.cs` line 97–99 retains `builder.Entity<Db_SnapTransaction>().HasIndex(t => t.MidtransOrderId).IsUnique()`. No migration removes it. |

### Backend — `GET /api/snap/status/{orderId}` and `POST /api/snap/cancel/{orderId}`

| # | Acceptance Criterion | Status | Details |
|---|---------------------|--------|---------|
| 7 | Both endpoints query by `(EnvironmentId, CallerOrderId)` where `EnvironmentId` is from `X-Api-Key` | ✅ Implemented | `GetPaymentStatus` (line 258–259) and `CancelPayment` (line 342–343) both use `.FirstOrDefaultAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == orderId)`. These were already correctly scoped before task-006; no changes were needed. |
| 8 | If query returns null, response is `404 Not Found` (not `403`) | ✅ Implemented | Both methods return `NotFound(DataWrapper<object>.NotFound(...))` on null result. |
| 9 | No cross-tenant data leakage possible | ✅ Implemented | The scoped `(EnvironmentId, CallerOrderId)` query makes it structurally impossible to return another tenant's transaction. |

### Concurrency — Race Condition Handling

| # | Acceptance Criterion | Status | Details |
|---|---------------------|--------|---------|
| 10 | `DbUpdateException` on unique constraint violation is caught and returned as `409 Conflict` | ✅ Implemented | Lines 551–564: `try { await m_dbContext.SaveChangesAsync(); } catch (DbUpdateException) { ... return Conflict(...); }` |
| 11 | Log the conflict at Warning level for audit purposes | ✅ Implemented | Lines 557–560: `m_logger.LogWarning("Concurrent duplicate detected for OrderId '{OrderId}' in environment {EnvId}. The DB unique constraint prevented a duplicate insert.", request.OrderId, environment.Id)` |

### Definition of Done

| # | Criterion | Status |
|---|-----------|--------|
| D1 | Migration created and applies cleanly | ⚠️ Index declared in fluent API but no migration file committed |
| D2 | `AppDbContext.OnModelCreating` updated | ✅ Done |
| D3 | `CreateTokenAsync` duplicate check updated | ✅ Done |
| D4 | Concurrent duplicate → `409` (not `500`) | ✅ Done |
| D5 | Status/Cancel endpoints scoped to caller's `EnvironmentId` | ✅ Done (pre-existing) |
| D6 | `dotnet build` passes with no new errors or warnings | ✅ Done (per completion summary) |
| D7 | `dotnet ef migrations add` / `dotnet ef database update` succeed | ⚠️ Not executed; deferred to deployment |
| D8 | Manual test: same orderId twice → `409` | ⚠️ Not verifiable from code review alone |
| D9 | Manual test: same orderId in different environments → both succeed | ⚠️ Not verifiable from code review alone |

---

## Userflow Validation

| Flow | Description | Status | Details |
|------|------------|--------|---------|
| **#1** | Create Snap Token — Happy Path | ✅ Covered | Environment resolution → `(EnvironmentId, CallerOrderId)` duplicate check → insert → Midtrans API call → `200 OK`. Flow is fully intact. |
| **#2** | Duplicate orderId — Same Environment | ✅ Covered | `AnyAsync` check on `(EnvironmentId, CallerOrderId)` returns true → `409 Conflict` with correct message. |
| **#3** | Same orderId — Different Environment | ✅ Covered | Different `EnvironmentId` from different API key means the `AnyAsync` check returns false → proceeds normally. Structural correctness confirmed. |
| **#4** | Concurrent Duplicate — Race Condition | ✅ Covered | Both pass app-layer check → first insert succeeds → second hits DB unique constraint → `DbUpdateException` caught → `409 Conflict` + Warning log. |
| **#5** | Check Payment Status — Own Environment | ✅ Covered | `GetPaymentStatus` queries `(EnvironmentId, CallerOrderId)` → transaction found → Midtrans status API → `200 OK`. |
| **#6** | Check Payment Status — Another Tenant's Order | ✅ Covered | Scoped query returns null → `404 Not Found`. No data leakage. |
| **#7** | Cancel Payment — Own Environment | ✅ Covered | `CancelPayment` queries `(EnvironmentId, CallerOrderId)` → transaction found → Midtrans cancel API → DB update → `200 OK`. |

---

## Security Findings

### SEC-1 — Broad `DbUpdateException` Catch May Mask Non-Duplicate Errors
- **Priority:** 🟡 Medium
- **Location:** `SnapController.cs`, lines 555–564 (`CreateTokenAsync`)
- **Issue:** The `catch (DbUpdateException)` block catches ALL database update exceptions, not just unique constraint violations. If a different DB error occurs during `SaveChangesAsync` (e.g., foreign key violation on a deleted environment, connection failure mid-write, data truncation), it would be misreported as a `409 Conflict` duplicate-order error. The developer would see a misleading warning log about a "concurrent duplicate" when the real cause is something else entirely.
- **Suggestion:** Inspect the inner exception for the unique constraint violation specifically. For PostgreSQL (Npgsql), check for `PostgresException` with `SqlState == "23505"` (unique violation):
  ```csharp
  catch (DbUpdateException ex) when (
      ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
  {
      m_logger.LogWarning(...);
      return Conflict(...);
  }
  ```
  This lets other `DbUpdateException` types propagate to the global error handler as `500`, preserving correct error semantics.

### SEC-2 — No EF Core Migration File Committed — Index Not Enforced Until Manual Step
- **Priority:** 🟠 High
- **Location:** `PaymentGateway.Server/Migrations/` (missing migration file), `AppDbContextModelSnapshot.cs` (not updated)
- **Issue:** The composite unique index `IX_SnapTransactions_EnvironmentId_CallerOrderId` is declared in the fluent API but no migration was generated or committed. The `AppDbContextModelSnapshot.cs` does not reflect the new index. This means:
  1. The database does NOT have the index until someone manually runs `dotnet ef migrations add` + `dotnet ef database update`.
  2. Until the index exists in the DB, the race-condition catch (SEC-1) is only a safety net for the *existing* `MidtransOrderId` unique index — it does NOT enforce `(EnvironmentId, CallerOrderId)` uniqueness at the DB level.
  3. Future `dotnet ef migrations add` commands for other tasks will auto-include this index in their migration, leading to unexpected migration scope.
  4. The model snapshot being out of sync could cause EF Core tooling warnings or confusion.
- **Suggestion:** Generate and commit the migration file as part of this task, consistent with the project's established convention (task-001, task-002, task-003 all committed their migration files). Run:
  ```
  dotnet ef migrations add "add-unique-CallerOrderId-per-environment" -c PaymentGateway.Server.Databases.AppDbContext
  ```
  and commit the resulting `.cs` and `.Designer.cs` files plus the updated `AppDbContextModelSnapshot.cs`.

### SEC-3 — Callback Redirect Queries by `MidtransOrderId` Without Environment Scoping
- **Priority:** 🟢 Low
- **Location:** `SnapController.cs`, `HandleCallbackRedirectAsync` (lines 464–494)
- **Issue:** The callback endpoints (`/api/midtrans/snap/callback`, etc.) look up transactions by `MidtransOrderId` only, without scoping to an environment. This is architecturally correct because the `MidtransOrderId` is a system-derived value (not user-controlled) that includes the env prefix, and Midtrans sends it back as-is. However, it's worth noting that this endpoint does not validate the request originates from Midtrans (no signature check), so a malicious actor who knows or guesses a `MidtransOrderId` could trigger a redirect.
- **Suggestion:** This is out-of-scope for task-006 and low-risk since the callback only performs a redirect to a pre-configured URL (not attacker-controlled). No immediate action needed; consider adding Midtrans IP allowlisting or a callback signature check as a future hardening task.

### SEC-4 — Repeated Environment Resolution Pattern Without Centralized Middleware
- **Priority:** 🟢 Low
- **Location:** `SnapController.cs`, all 5 `X-Api-Key` resolution blocks (lines ~63, ~100, ~137, ~242, ~326)
- **Issue:** The `X-Api-Key` → environment resolution logic is duplicated across every endpoint. While not a security vulnerability today, if one endpoint's resolution logic diverges (e.g., forgetting the `!e.IsDeleted` check), it could create a tenant-isolation bypass.
- **Suggestion:** Extract into a shared middleware or action filter (e.g., `ApiKeyAuthorizationFilter`) that resolves the environment and injects it into the `HttpContext.Items`. This is a code-quality improvement, not a task-006 blocker.

---

## UI/UX Findings

Per the PRD and userflow, this is a **backend-only security hardening task** with no frontend changes. The UI/UX review scope is limited to the API consumer experience (child app developer persona).

### UX-1 — Clear Error Message on Duplicate Order ID
- **Priority:** 🟢 Enhancement
- **Persona:** Child App Developer
- **Location:** `CreateTokenAsync` — `409 Conflict` response
- **Issue:** The error message `"A transaction with OrderId '{orderId}' already exists for this environment."` is clear and actionable. No issue.
- **Status:** ✅ Satisfactory

### UX-2 — 404 Response for Cross-Tenant Lookup Prevents Information Leakage
- **Priority:** 🟢 Enhancement
- **Persona:** Child App Developer
- **Location:** `GetPaymentStatus`, `CancelPayment` — `404 Not Found` response
- **Issue:** Returning `404` (not `403`) on a scoped miss is the correct security pattern — it reveals no information about whether the order ID exists for another tenant. No issue.
- **Status:** ✅ Satisfactory

### UX-3 — Race Condition Response Is Indistinguishable from Normal Duplicate
- **Priority:** 🟢 Enhancement
- **Persona:** Child App Developer
- **Location:** `CreateTokenAsync` — `DbUpdateException` catch → `409 Conflict`
- **Issue:** When the race condition handler fires, the response message is identical to the application-layer duplicate check: `"A transaction with OrderId '{orderId}' already exists for this environment."`. From the API consumer's perspective, this is ideal — they don't need to know whether the rejection was app-layer or DB-layer. The Warning log provides internal audit trail for operators.
- **Status:** ✅ Satisfactory

---

## Summary of Findings

| Category | ✅ Pass | ⚠️ Partial | ❌ Fail | Total |
|----------|---------|-----------|---------|-------|
| PRD Acceptance Criteria | 8 | 3 | 0 | 11 |
| Definition of Done | 5 | 4 | 0 | 9 |
| Userflow Steps | 7 | 0 | 0 | 7 |
| Security | — | — | — | 4 findings |
| UI/UX | — | — | — | 3 findings (all satisfactory) |

### Key Issues Requiring Action

| ID | Priority | Finding | Recommendation |
|----|----------|---------|----------------|
| SEC-2 | 🟠 High | No EF Core migration file committed; model snapshot out of sync; DB index not enforced until manual intervention | Generate and commit the migration file + updated snapshot |
| SEC-1 | 🟡 Medium | Broad `DbUpdateException` catch may mask non-duplicate DB errors as 409 | Narrow the catch to PostgreSQL unique constraint violation (`SqlState == "23505"`) |
| SEC-3 | 🟢 Low | Callback redirect queries by `MidtransOrderId` without origin validation | Out of scope; document for future hardening |
| SEC-4 | 🟢 Low | Repeated API key resolution pattern across endpoints | Extract to shared filter/middleware (code quality) |

---

## Overall Verdict

### ⚠️ Pass with Conditions

The **application-layer implementation is correct and complete** — the duplicate check, race condition handling, tenant-scoped queries, and error responses all match the PRD and userflow specifications precisely. The code changes are minimal, focused, and non-breaking.

**Two conditions must be addressed before this task can be considered fully done:**

1. **[Required] Generate and commit the EF Core migration file** (SEC-2). Without it, the composite unique index does not exist in the database, the model snapshot is stale, and the DB-level safety net (US-3) is not operational. This was an explicit acceptance criterion in the PRD ("A new migration file is created").

2. **[Recommended] Narrow the `DbUpdateException` catch** (SEC-1). The current broad catch could misclassify unrelated DB errors as `409 Conflict`. Adding a `when` clause for PostgreSQL's unique violation error code (`23505`) would make the error handling precise without changing the happy path.
