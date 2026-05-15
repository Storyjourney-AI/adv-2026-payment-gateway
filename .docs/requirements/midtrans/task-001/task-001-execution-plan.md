## Checklist
- [x] AppSettings — Midtrans config block + strongly-typed options
- [x] DTOs — Snap token request and response
- [x] DB Entity + Migration — Db_SnapTransaction log table
- [x] Snap Controller — Sandbox + Production token endpoints
- [x] Webhook Utility — Signature verification helper
- [x] Webhook Controller — Sandbox + Production webhook receivers

---

## Context

This plan adds Midtrans Snap integration. The gateway acts as a **secure intermediary** — child apps never hold the Midtrans Server Key.

**Flow:**
1. Child app `POST api/snap/sandbox/token` with `X-Api-Key` header → gateway calls Midtrans Snap API → returns `{ token, redirect_url }`.
2. Customer completes payment on Midtrans hosted page.
3. Midtrans POSTs notification to `api/webhook/midtrans/sandbox` → gateway verifies signature → forwards to `Db_Environment.WebhookUrl`.

**Authentication model:**

| Direction | Method |
|---|---|
| Child app → this gateway | `X-Api-Key` header (matched against `Db_Environment.ApiKey`) |
| This gateway → Midtrans Snap | `Authorization: Basic Base64(ServerKey + ":")` |
| Midtrans → this gateway (webhook) | No auth header; verified via SHA-512 `signature_key` field |

**Infrastructure rules compliance:**
- Business logic in controllers; no services unless DRY principle forces it.
- `DataWrapper<T>` response pattern throughout.
- Signature verification is shared by both webhook endpoints → extracted to `Midtrans/Utils/MidtransSignatureHelper.cs` (DRY: static utility class, not a service).
- `IHttpClientFactory` is already registered in `Program.cs` — no new registration needed.
- Only `appsettings.development.json` exists (no base `appsettings.json`) — add Midtrans block there only.

**Reference docs:**
- `.docs/documentation/midtrans-snap-docs.md`
- `.docs/documentation/midtrans-webhook.md`

---

## Task 1: AppSettings + Strongly-Typed Options

* Target File: EXISTING `PaymentGateway.Server/appsettings.development.json`
    - Append a `Midtrans` block alongside the existing `Jwt` block.
    - `Sandbox.IsEnabled = true`, `Production.IsEnabled = false` — production key access is restricted by the flag.
    - **Feasibility: HIGH** — Pure JSON addition; no code change needed beyond this task.

```json
"Midtrans": {
  "Sandbox": {
    "ServerKey": "SB-Mid-server-YOUR-DEV-KEY-HERE",
    "IsEnabled": true
  },
  "Production": {
    "ServerKey": "",
    "IsEnabled": false
  }
}
```

* Target File: NEW `PaymentGateway.Server/Midtrans/Models/MidtransOptions.cs`
    - Two nested POCOs: `MidtransOptions` (top-level) and `MidtransEnvironmentOptions` (per-env).
    - `MidtransEnvironmentOptions` fields: `ServerKey: string`, `IsEnabled: bool`.
    - `MidtransOptions` fields: `Sandbox: MidtransEnvironmentOptions`, `Production: MidtransEnvironmentOptions`.
    - **Feasibility: HIGH** — Simple POCO; no dependencies.

* Target File: EXISTING `PaymentGateway.Server/Program.cs`
    - Add one line after the JWT config block: `builder.Services.Configure<MidtransOptions>(builder.Configuration.GetSection("Midtrans"));`
    - No other changes to Program.cs needed (`AddHttpClient()` is already registered).
    - **Feasibility: HIGH** — Identical pattern to the existing Jwt config registration.

---

## Task 2: DTOs — Snap Token

* Target File: NEW `PaymentGateway.Server/Midtrans/Models/Dtos/Dto_SnapTokenRequest.cs`
    - Fields:
        - `OrderId` (string, `[Required]`, max 42 chars — enforced because gateway prefixes 9 chars to form the full Midtrans `order_id`)
        - `GrossAmount` (int, `[Required]`, `[Range(1, int.MaxValue)]`)
        - `CustomerDetails` (optional nested class: `FirstName`, `LastName`, `Email`, `Phone`)
        - `ItemDetails` (optional list of nested class: `Id`, `Price`, `Quantity`, `Name`)
    - **Feasibility: HIGH** — Simple DTO matching Midtrans request structure.

* Target File: NEW `PaymentGateway.Server/Midtrans/Models/Dtos/Dto_SnapTokenResponse.cs`
    - Fields: `Token (string)`, `RedirectUrl (string)`.
    - Returned inside `DataWrapper<Dto_SnapTokenResponse>`.
    - **Feasibility: HIGH** — Direct passthrough of Midtrans response.

---

## Task 3: DB Entity + AppDbContext + Migration

**Order ID routing decision:** To route an incoming Midtrans webhook back to the correct child app, the gateway stores a transaction log keyed by `order_id`. The `order_id` sent to Midtrans is constructed as `{env.Id[0..7]}_{callerOrderId}` (8-char Guid prefix + underscore + the caller's `OrderId`). The webhook receiver strips the prefix to look up `Db_SnapTransaction`, then reads `EnvironmentId` to get the target `WebhookUrl`.

* Target File: NEW `PaymentGateway.Server/Midtrans/Models/Dbs/Db_SnapTransaction.cs`
    - Fields:
        - `Id` (Guid, PK)
        - `EnvironmentId` (Guid, FK → `Db_Environment.Id`)
        - `MidtransOrderId` (string, unique index) — the full prefixed ID sent to Midtrans
        - `CallerOrderId` (string) — the raw `OrderId` from the child app's request
        - `GrossAmount` (int)
        - `MidtransEnv` (string: `"sandbox"` or `"production"`)
        - `TransactionStatus` (string?, updated by webhook)
        - `MidtransTransactionId` (string?, updated by webhook)
        - `CreatedAt` (DateTime)
        - `UpdatedAt` (DateTime)
    - No `ISoftDelete` — these are audit records, never logically deleted.
    - **Feasibility: MEDIUM** — EF migration required; schema is straightforward but adds a deploy step.

* Target File: EXISTING `PaymentGateway.Server/Databases/AppDbContext.cs`
    - Add `DbSet<Db_SnapTransaction> SnapTransactions { get; set; }`.
    - Inside `OnModelCreating`: map to `"payment"` schema, add unique index on `MidtransOrderId`, configure FK to `Db_Environment`.
    - **Feasibility: HIGH** — Follows existing `DbSet` + `OnModelCreating` pattern in the file.

* Migration command (to be run by executor):
    ```
    dotnet ef migrations add "add-snap-transactions" -c PaymentGateway.Server.Databases.AppDbContext
    ```
    - **Feasibility: HIGH** — Standard EF Core migration; documented in `Migrations/migrations.md`.

---

## Task 4: Snap Controller

* Target File: NEW `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`
    - `[AllowAnonymous]` — no JWT; authenticated via `X-Api-Key` header.
    - Inject: `AppDbContext`, `IOptions<MidtransOptions>`, `IHttpClientFactory`, `ILogger<SnapController>`.

    **`POST api/snap/sandbox/token`** and **`POST api/snap/production/token`** share the same logic via a private helper `CreateTokenAsync(MidtransEnvironmentOptions envOptions, string midtransUrl, string webhookCallbackUrl, Dto_SnapTokenRequest request)`:

    1. Check `envOptions.IsEnabled`. Return `StatusCode(503)` wrapping `DataWrapper<object>.Fail_InternalError("This environment is currently disabled")` if false.
    2. Read `X-Api-Key` from request header. Query `Db_Environment` by `ApiKey`. Return `Unauthorized(DataWrapper<object>.Unauthorized(...))` if not found.
    3. Check `ModelState.IsValid`. Return `BadRequest(DataWrapper<object>.BadRequest(...))` if invalid.
    4. Build `MidtransOrderId = env.Id.ToString("N")[..8] + "_" + request.OrderId`.
    5. Insert new `Db_SnapTransaction` record (status `"pending"`). Save to DB.
    6. Build Midtrans JSON body matching the Snap API schema.
    7. Call Midtrans Snap endpoint via `IHttpClientFactory`:
        - `Authorization: Basic Base64(envOptions.ServerKey + ":")`
        - `X-Override-Notification: {webhookCallbackUrl}` (routes Midtrans notification back to this gateway)
    8. On Midtrans success: return `Ok(DataWrapper<Dto_SnapTokenResponse>.Succeed(response))`.
    9. On Midtrans error: log, update transaction status to `"error"`, return `StatusCode(502, DataWrapper<object>.Fail_InternalError("Midtrans error"))`.

    - **Feasibility: HIGH** — `IHttpClientFactory` already registered. `IOptions<T>` pattern proven with JWT config.

---

## Task 5: Signature Verification Utility

* Target File: NEW `PaymentGateway.Server/Midtrans/Utils/MidtransSignatureHelper.cs`
    - Static class with one method: `bool Verify(string orderId, string statusCode, string grossAmount, string receivedSignature, string serverKey)`.
    - Computes `SHA512(orderId + statusCode + grossAmount + serverKey)` as lowercase hex string.
    - Returns true if computed hash equals `receivedSignature` (case-insensitive).
    - **Feasibility: HIGH** — Pure static utility; no DI or DB needed. DRY justification: called by both sandbox and production webhook endpoints.

---

## Task 6: Webhook Controller

* Target File: NEW `PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs`
    - `[AllowAnonymous]` — Midtrans sends unauthenticated POST.
    - Inject: `AppDbContext`, `IOptions<MidtransOptions>`, `IHttpClientFactory`, `ILogger<WebhookController>`.

    **`POST api/webhook/midtrans/sandbox`** and **`POST api/webhook/midtrans/production`** share logic via `HandleWebhookAsync(MidtransEnvironmentOptions envOptions)`:

    1. Check `envOptions.IsEnabled`. Return `200 OK` even if disabled (avoid Midtrans retries on intentionally disabled env).
    2. Read raw request body as string. Deserialize to extract `order_id`, `status_code`, `gross_amount`, `signature_key`, `transaction_status`, `transaction_id`.
    3. Call `MidtransSignatureHelper.Verify(...)`. If invalid: log warning, return `BadRequest()`.
    4. Look up `Db_SnapTransaction` by `MidtransOrderId == order_id`. If not found: log warning, return `200 OK` (already processed or unknown).
    5. Fetch related `Db_Environment` via `EnvironmentId`.
    6. Update `Db_SnapTransaction.TransactionStatus`, `MidtransTransactionId`, `UpdatedAt`. Save to DB.
    7. If `env.WebhookUrl` is non-null: POST raw JSON body to `WebhookUrl` via `IHttpClientFactory`. Log result.
    8. **Always return `200 OK`** to Midtrans.

    - **Feasibility: HIGH** — Straightforward HttpClient forwarding; no complex dependencies.

---

## Out of Scope for This Task

| Item | Reason |
|---|---|
| Midtrans refund/cancel API | Separate task |
| GET transaction status polling | Separate task |
| Frontend Snap.js pop-up | Child app responsibility |
| Retry logic for failed webhook forwarding | Complexity deferred; initial version logs and moves on |

---

## New Folder Structure

```
PaymentGateway.Server/
└── Midtrans/
    ├── Controllers/
    │   ├── SnapController.cs           (Task 4)
    │   └── WebhookController.cs        (Task 6)
    ├── Models/
    │   ├── Dbs/
    │   │   └── Db_SnapTransaction.cs   (Task 3)
    │   ├── Dtos/
    │   │   ├── Dto_SnapTokenRequest.cs  (Task 2)
    │   │   └── Dto_SnapTokenResponse.cs (Task 2)
    │   └── MidtransOptions.cs           (Task 1)
    └── Utils/
        └── MidtransSignatureHelper.cs   (Task 5)
```

---

## Execution Order

1. **Task 1** — `MidtransOptions.cs` + appsettings block + `Program.cs` one-liner
2. **Task 2** — DTOs (`Dto_SnapTokenRequest`, `Dto_SnapTokenResponse`)
3. **Task 3** — `Db_SnapTransaction.cs` + `AppDbContext` update + EF migration
4. **Task 5** — `MidtransSignatureHelper.cs` utility (no DB dependency)
5. **Task 4** — `SnapController.cs`
6. **Task 6** — `WebhookController.cs`
