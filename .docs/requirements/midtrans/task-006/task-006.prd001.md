# [Security / Fix] Order ID Scoping — Enforce API Key Ownership & Composite Uniqueness

**Labels:** `security` `fix` `backend` `database`
**Milestone:** task-006
**Priority:** High
**Related:** task-001 (Midtrans Snap Integration), task-004 (Status Check & Cancel Endpoints)

---

## Summary

Harden two security properties of order ID handling across the gateway:

1. **Ownership** — An `orderId` submitted to any endpoint must belong to the environment identified by the `X-Api-Key`. If a caller supplies an order ID that exists for a different tenant, the response must be `403 Forbidden` (or a lookup-scoped miss treated as `404`) — never leaking cross-tenant data.
2. **Uniqueness** — Within a single environment, a `CallerOrderId` must be unique. Enforce this at the database level with a composite unique index on `(EnvironmentId, CallerOrderId)` and update the application-layer duplicate check to use the same semantics.

---

## Background / Context

### Current State

- `POST /api/snap/token` already performs a duplicate check before inserting, but it does so by constructing `MidtransOrderId` (`{envId[0..8]}_{callerOrderId}`) and looking for that value in the DB. This works in practice but has two weaknesses:
  - The check is on `MidtransOrderId` — a derived string — rather than on the semantic pair `(EnvironmentId, CallerOrderId)`.
  - Two environment GUIDs that share the same first 8 hex characters (extremely unlikely but theoretically possible) could cause a false positive collision between different tenants.
- The database has a `UNIQUE` constraint on `MidtransOrderId` only. There is **no** unique constraint on `(EnvironmentId, CallerOrderId)`. A duplicate insert would surface as an unhandled `DbUpdateException` rather than a clean `409 Conflict` if the application-layer check were to race or be bypassed.
- `GET /api/snap/status/{orderId}` and `POST /api/snap/cancel/{orderId}` (task-004) are new endpoints that will also accept a `CallerOrderId`. Their ownership enforcement depends on querying by `(EnvironmentId, CallerOrderId)` — making this schema constraint foundational.
- No existing endpoint validates that a submitted `CallerOrderId` belongs to the caller's environment before a write or read operation (the token creation endpoint implicitly scopes inserts because it always uses the resolved environment, but there is no explicit guard documented or enforced).

### What Needs to Change

1. **New EF Core migration** — add a composite unique index on `(EnvironmentId, CallerOrderId)` in the `SnapTransactions` table.
2. **Update `CreateTokenAsync`** — replace the existing `MidtransOrderId`-based duplicate check with a `(EnvironmentId, CallerOrderId)` check that directly matches the new DB constraint.
3. **Document the ownership contract** — `GET /api/snap/status/{orderId}` and `POST /api/snap/cancel/{orderId}` (task-004) must query by `(EnvironmentId, CallerOrderId)` where `EnvironmentId` is resolved from the caller's `X-Api-Key`. A miss on this scoped lookup is always `404` — the scope itself enforces the ownership boundary.

---

## User Stories

**US-1 — Tenant isolation:**
> As a gateway operator, I want it to be impossible for one tenant's API key to access, query, or cancel another tenant's orders so that the system is secure by design, not by convention.

**US-2 — Duplicate prevention:**
> As a child application developer, I want the gateway to reject a token creation request if I accidentally reuse an `orderId` for the same environment so that I get a clear `409 Conflict` error instead of silent failure or an unhandled exception.

**US-3 — DB-enforced safety net:**
> As a gateway operator, I want the uniqueness rule on `(EnvironmentId, CallerOrderId)` to be enforced at the database level so that even a concurrency race between two simultaneous token requests with the same order ID cannot produce duplicate records.

---

## Acceptance Criteria

### Database — EF Core Migration

- [ ] A new migration file is created (e.g. `add-unique-CallerOrderId-per-environment`) that adds:
  ```sql
  CREATE UNIQUE INDEX "IX_SnapTransactions_EnvironmentId_CallerOrderId"
    ON payment."SnapTransactions" ("EnvironmentId", "CallerOrderId");
  ```
- [ ] The migration is additive — no existing data is at risk because the current `MidtransOrderId` uniqueness constraint already prevents `(EnvironmentId, CallerOrderId)` duplicates in practice.
- [ ] `dotnet ef migrations add` and `dotnet ef database update` succeed cleanly.

### Backend — `SnapController.CreateTokenAsync` (duplicate check update)

- [ ] Replace the existing duplicate check:
  ```csharp
  // Before
  var duplicate = await m_dbContext.SnapTransactions
      .AnyAsync(t => t.MidtransOrderId == midtransOrderId);
  ```
  With a semantically correct check:
  ```csharp
  // After
  var duplicate = await m_dbContext.SnapTransactions
      .AnyAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == request.OrderId);
  ```
- [ ] The `409 Conflict` response message remains: `"A transaction with OrderId '{orderId}' already exists for this environment."`
- [ ] The `MidtransOrderId` UNIQUE constraint remains in the DB as a secondary safety net — no migration removes it.

### Backend — `GET /api/snap/status/{orderId}` and `POST /api/snap/cancel/{orderId}` (task-004 enforcement)

- [ ] Both endpoints query `SnapTransactions` by:
  ```csharp
  .FirstOrDefaultAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == orderId)
  ```
  where `environment` is resolved from the caller's `X-Api-Key`.
- [ ] If the query returns `null`, the response is `404 Not Found` — not `403`. The scoped query means the caller can only see their own records; a miss is always a genuine not-found within their tenant scope.
- [ ] Under no circumstances is a transaction from another tenant's environment returned, even if the same `CallerOrderId` string exists across multiple environments.

### Concurrency — Race Condition Handling

- [ ] If two simultaneous `POST /api/snap/token` calls for the same `(EnvironmentId, CallerOrderId)` both pass the application-layer check and one of them fails on the DB unique constraint, the `DbUpdateException` (unique violation) is caught and surfaced as `409 Conflict` — not `500 Internal Server Error`.
- [ ] The existing Midtrans Snap API call on the racing request may have already been made; log the conflict at `Warning` level for audit purposes.

---

## Technical Notes

### EF Core — Adding a Composite Unique Index

In `AppDbContext.OnModelCreating` (or via a data annotation on the entity), add:

```csharp
modelBuilder.Entity<Db_SnapTransaction>()
    .HasIndex(t => new { t.EnvironmentId, t.CallerOrderId })
    .IsUnique()
    .HasDatabaseName("IX_SnapTransactions_EnvironmentId_CallerOrderId");
```

Then generate the migration:
```
dotnet ef migrations add add-unique-CallerOrderId-per-environment
dotnet ef database update
```

### Why Not Just Keep the MidtransOrderId Check

The `MidtransOrderId` pattern (`{envId[0..8]}_{callerOrderId}`) encodes the environment prefix and works correctly for all realistic deployments. However:
- It relies on an encoding convention rather than a relational constraint.
- A direct `(EnvironmentId, CallerOrderId)` index is explicit, query-friendly, and aligns exactly with the ownership model.
- The new index also enables efficient lookups in the status and cancel endpoints without constructing the derived `MidtransOrderId`.

### Existing Data Is Safe

Any existing `SnapTransactions` rows satisfy the new uniqueness constraint because the `MidtransOrderId` UNIQUE constraint (which encodes both envId and callerOrderId) already prevents `(EnvironmentId, CallerOrderId)` duplicates. The migration will not fail on a populated database.

---

## Out of Scope

- Removing the `MidtransOrderId` UNIQUE constraint (kept as a secondary safety net)
- Changing the `MidtransOrderId` format
- Cross-tenant deduplication (order IDs from different tenants are independent; the same string is valid in multiple environments)

---

## Definition of Done

- [ ] Migration created and applies cleanly to an existing database: unique index on `(EnvironmentId, CallerOrderId)` in `payment.SnapTransactions`
- [ ] `AppDbContext.OnModelCreating` updated to declare the composite unique index
- [ ] `CreateTokenAsync` duplicate check updated to query by `(EnvironmentId, CallerOrderId)`
- [ ] Concurrent duplicate insertion is caught and returned as `409 Conflict` (not `500`)
- [ ] `GET /api/snap/status/{orderId}` and `POST /api/snap/cancel/{orderId}` query transactions scoped to the caller's `EnvironmentId`
- [ ] `dotnet build` passes with no new errors or warnings
- [ ] `dotnet ef migrations add` and `dotnet ef database update` succeed
- [ ] Manual test: submitting the same `orderId` twice for the same API key returns `409` on the second call
- [ ] Manual test: submitting the same `orderId` string for two different environments succeeds for both (cross-tenant isolation confirmed)
