dotnet ef migrations add "app-init" -c PaymentGateway.Server.Databases.AppDbContext
dotnet ef database update -c PaymentGateway.Server.Databases.AppDbContext
dotnet ef database update "20260117014643_AddContentElements" -c PaymentGateway.Server.Databases.AppDbContext
dotnet ef migrations remove -c PaymentGateway.Server.Databases.AppDbContext

## Pending Migrations (run manually after deployment)

### task-001: Midtrans Snap Transactions table
```
dotnet ef migrations add "add-snap-transactions" -c PaymentGateway.Server.Databases.AppDbContext
dotnet ef database update -c PaymentGateway.Server.Databases.AppDbContext
```
Creates the `payment.SnapTransactions` table used to log Snap token creation requests and map Midtrans webhook notifications back to the correct child app environment.

### task-002 (prd002): Add IsSandbox column to Environments table

**Step 1:** Generate the migration
```
dotnet ef migrations add "add-is-sandbox-to-environment" -c PaymentGateway.Server.Databases.AppDbContext
```

**Step 2:** After generation, open the newly created migration file and add the following raw SQL call inside the `Up()` method, **after** the `AddColumn` call for `IsSandbox`:
```csharp
migrationBuilder.Sql("UPDATE payment.\"Environments\" SET \"IsSandbox\" = false WHERE \"Name\" = 'production'");
```
This backfills all existing rows: production environments get `IsSandbox = false`, all others retain the default `true`.

**Step 3:** Apply the migration
```
dotnet ef database update -c PaymentGateway.Server.Databases.AppDbContext
```

> **Note:** The PRD references `app."Environments"` in the SQL, but the actual schema is `payment`. The corrected SQL above uses `payment."Environments"`.

---

### task-003 (prd001): Update Super Admin seed email

**No EF migration required.** This is a data-only change. Run the following SQL directly against the production database to rename the existing admin account:

```sql
UPDATE "AspNetUsers"
SET "Email" = 'technical@advine.id',
    "NormalizedEmail" = 'TECHNICAL@ADVINE.ID',
    "UserName" = 'technical@advine.id',
    "NormalizedUserName" = 'TECHNICAL@ADVINE.ID'
WHERE "Email" = 'yoshua@advine.id';
```

The code seed (`AuthService.SeedSuperAdminAsync`) has been updated to `technical@advine.id` and will use the new email on a fresh database.

---

### task-006 (prd001): Add composite unique index on (EnvironmentId, CallerOrderId)

```
dotnet ef migrations add "added-activity-log" -c PaymentGateway.Server.Databases.AppDbContext
dotnet ef database update -c PaymentGateway.Server.Databases.AppDbContext
```

Adds a composite unique index `IX_SnapTransactions_EnvironmentId_CallerOrderId` on the `payment.SnapTransactions` table. This enforces that each `CallerOrderId` is unique within a single environment at the database level, preventing duplicate orders even under race conditions. The existing `MidtransOrderId` unique index remains as a secondary safety net.

---

### task-007: Add PendingResponseUrl column to Environments table

```
dotnet ef migrations add "add-pending-response-url-to-environment" --project PaymentGateway.Server --context AppDbContext
```

Adds a nullable `PendingResponseUrl` column to `payment.Environments`.

No backfill is required. Existing rows and new default environments can leave `PendingResponseUrl` blank, and the Midtrans browser callback flow falls back to `FailureResponseUrl` when no dedicated pending URL is configured.
