# Execution Plan — Order ID Scoping: Enforce API Key Ownership & Composite Uniqueness

## Checklist
- [x] Database — Composite Unique Index Declaration
- [x] Backend — Update CreateTokenAsync Duplicate Check
- [x] Backend — Race Condition Handling
- [x] Documentation — Migration Instructions
- [x] Build Validation

---

## Database — Composite Unique Index Declaration

* Target File: EXISTING `PaymentGateway.Server/Databases/AppDbContext.cs`
    - In `ConfigureMidtransSchema`, add a composite unique index on `(EnvironmentId, CallerOrderId)`:
      ```csharp
      builder.Entity<Db_SnapTransaction>()
          .HasIndex(t => new { t.EnvironmentId, t.CallerOrderId })
          .IsUnique()
          .HasDatabaseName("IX_SnapTransactions_EnvironmentId_CallerOrderId");
      ```
    - The existing `MidtransOrderId` unique index remains untouched
    - Feasibility: ✅ HIGH — straightforward EF Core fluent API addition

---

## Backend — Update CreateTokenAsync Duplicate Check

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
    - In `CreateTokenAsync` method (around line 527), replace the existing duplicate check:
      ```csharp
      // Before
      var duplicate = await m_dbContext.SnapTransactions
          .AnyAsync(t => t.MidtransOrderId == midtransOrderId);
      ```
      With:
      ```csharp
      // After
      var duplicate = await m_dbContext.SnapTransactions
          .AnyAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == request.OrderId);
      ```
    - The 409 Conflict response message remains unchanged
    - Feasibility: ✅ HIGH — single line change in an existing method

---

## Backend — Race Condition Handling

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
    - Wrap the `m_dbContext.SaveChangesAsync()` call (around line 550, after inserting `snapTransaction`) in a try/catch for `DbUpdateException`
    - On catch: check if it's a unique constraint violation, log at Warning level, and return `409 Conflict`
    - This handles the race condition where two simultaneous requests both pass the application-layer check but one fails on the DB constraint
    - Feasibility: ✅ HIGH — standard EF Core exception handling pattern

---

## Documentation — Migration Instructions

* Target File: EXISTING `PaymentGateway.Server/Migrations/migrations.md`
    - Append a new section for task-006 with migration commands:
      ```
      dotnet ef migrations add "add-unique-CallerOrderId-per-environment" -c PaymentGateway.Server.Databases.AppDbContext
      dotnet ef database update -c PaymentGateway.Server.Databases.AppDbContext
      ```
    - Describe what the migration creates (composite unique index on `(EnvironmentId, CallerOrderId)`)
    - Feasibility: ✅ HIGH — documentation update only

---

## Build Validation

* Run `dotnet build` in `PaymentGateway.Server/` to verify all files compile
* Feasibility: ✅ Standard validation step
