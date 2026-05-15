# Xendit Payment Gateway Integration - Execution Plan

## PRD Reference
- **Source**: `.docs/requirements/core/xendit.prd.md`
- **Goal**: Integrate Xendit Payment Session API to create payment links, store payment transactions, and handle webhooks
- **API Documentation**: https://docs.xendit.co/docs/payment-1
- **Key Change**: Using Payment Session API (newer version) instead of Invoice API

---

## Checklist
- [ ] Database Layer - Environment & Payment Models
- [ ] Backend - Payment Service & Controller
- [ ] Backend - Webhook Handler
- [ ] Backend - Configuration & Dependencies
- [ ] Integration Testing

---

## Domain: Payment Gateway (Xendit Integration)

### Phase 0: Environment Schema Update

#### Add IsDevelopment Flag to Environment

* **Target File**: EXISTING `PaymentGateway.Server/Applications/Models/Dbs/Db_Environment.cs`
    - Add property: `public bool IsDevelopment { get; set; } = true;`
    - Purpose: Determines if environment uses test API keys (development) or live API keys (production)
    - Default to `true` for safety - requires explicit opt-in to production
    - **Feasibility**: HIGH - Simple property addition

* **Target File**: EXISTING `PaymentGateway.Server/Databases/AppDbContext.cs`
    - In `ConfigurePaymentSchema` method, configure IsDevelopment default value:
    - `builder.Entity<Db_Environment>().Property(e => e.IsDevelopment).HasDefaultValue(true);`
    - This ensures existing environment records get `true` as default value
    - **Feasibility**: HIGH - Standard EF Core configuration

* **Target File**: TERMINAL - Create EF Core Migration
    - Run command: `cd PaymentGateway.Server; dotnet ef migrations add add-environment-isdevelopment`
    - Run command: `dotnet ef database update`
    - **Feasibility**: HIGH - Standard migration process

### Phase 1: Database Layer - Payment Models

#### Payment Transaction Models

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dbs/Db_PaymentXendit.cs`
    - Create payment entity to store Xendit Payment Session transaction data
    - Fields:
        - `Id` (Guid - our internal payment ID)
        - `EnvironmentId` (Guid, FK to Environment)
        - `PaymentSessionId` (string - Xendit's payment_session_id)
        - `ReferenceId` (string - caller's reference ID)
        - `CustomerReferenceId` (string - Xendit's customer reference)
        - `CustomerId` (string - Xendit's customer_id)
        - `Amount` (decimal)
        - `Currency` (string - IDR, USD, etc.)
        - `Country` (string - ID, PH, etc.)
        - `Status` (enum: Active/Completed/Expired/Canceled)
        - `PaymentLinkUrl` (string - the payment link to share)
        - `SuccessReturnUrl` (string)
        - `CancelReturnUrl` (string)
        - `CustomerEmail` (string)
        - `CustomerMobileNumber` (string)
        - `CustomerGivenNames` (string)
        - `CustomerSurname` (string)
        - `ExpiresAt` (DateTime)
        - `CompletedAt` (DateTime?)
        - `Locale` (string - default "en")
        - `SessionType` (string - default "PAY")
        - `Mode` (string - default "PAYMENT_LINK")
        - `CreatedAt` (DateTime)
        - `UpdatedAt` (DateTime)
        - `IsDeleted` (bool)
        - `DeletedAt` (DateTime?)
    - Implement `ISoftDelete` interface
    - Add navigation property: `public Db_Environment? Environment { get; set; }`
    - **Feasibility**: HIGH - Standard EF Core entity, follows existing patterns

* **Target File**: EXISTING `PaymentGateway.Server/Databases/AppDbContext.cs`
    - Add `DbSet<Db_PaymentXendit> PaymentsXendit { get; set; }`
    - Configure in `ConfigurePaymentSchema` method:
        - `.ToTable("PaymentsXendit", "payment")`
        - `.HasQueryFilter(p => !p.IsDeleted)`
        - `.HasOne(p => p.Environment).WithMany().HasForeignKey(p => p.EnvironmentId).OnDelete(DeleteBehavior.Restrict)`
        - `.HasIndex(p => p.PaymentSessionId).IsUnique()` - Xendit's unique ID
        - `.HasIndex(p => p.ReferenceId)` - Fast lookup by caller's reference
        - `.HasIndex(p => p.EnvironmentId)` - Fast filtering by environment
    - **Feasibility**: HIGH - DbContext already has payment schema

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dbs/Enums/PaymentStatus.cs`
    - Create enum: `Active`, `Completed`, `Expired`, `Canceled`
    - Matches Xendit Payment Session statuses exactly
    - **Feasibility**: HIGH - Simple enum creation

* **Target File**: TERMINAL - Create EF Core Migration
    - Run command: `cd PaymentGateway.Server; dotnet ef migrations add add-xendit-payment-session-table`
    - Run command: `dotnet ef database update`
    - **Feasibility**: HIGH - Standard migration process

### Phase 2: Backend DTOs (Data Transfer Objects)

#### Request DTOs (in Dtos/Xendit folder)

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/Dto_CreatePaymentSessionRequest.cs`
    - Properties:
        - `ReferenceId` (string, required)
        - `Amount` (decimal, required)
        - `Currency` (string, default "IDR")
        - `Country` (string, default "ID")
        - `Customer` (Dto_XenditCustomer, required)
        - `SuccessReturnUrl` (string, required)
        - `CancelReturnUrl` (string, required)
    - Add data annotations for validation
    - **Feasibility**: HIGH - Standard DTO pattern

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/Dto_XenditCustomer.cs`
    - Properties:
        - `ReferenceId` (string) - optional, generate UUID if not provided
        - `Type` (string, default "INDIVIDUAL")
        - `Email` (string, required, email format)
        - `MobileNumber` (string, required, phone format)
        - `IndividualDetail` (Dto_IndividualDetail, required)
    - **Feasibility**: HIGH - Nested DTO

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/Dto_IndividualDetail.cs`
    - Properties:
        - `GivenNames` (string, required)
        - `Surname` (string, required)
    - **Feasibility**: HIGH - Simple nested DTO

#### Response DTOs

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/Dto_CreatePaymentSessionResponse.cs`
    - Properties:
        - `PaymentId` (Guid - our internal ID)
        - `PaymentSessionId` (string - Xendit's ID)
        - `ReferenceId` (string)
        - `PaymentLinkUrl` (string - the link to share)
        - `Amount` (decimal)
        - `Currency` (string)
        - `Status` (string)
        - `ExpiresAt` (DateTime)
        - `CreatedAt` (DateTime)
    - **Feasibility**: HIGH - Standard response DTO

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/Dto_PaymentSessionDetailsResponse.cs`
    - Properties: All payment session details for retrieval operations
    - Maps from `Db_PaymentXendit` entity
    - **Feasibility**: HIGH - Direct mapping from entity

#### Xendit API DTOs (for external API calls)

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/XenditPaymentSessionRequest.cs`
    - Exact structure matching Xendit API documentation
    - Use `[JsonPropertyName]` for snake_case mapping
    - Properties: `reference_id`, `session_type`, `mode`, `amount`, `currency`, `country`, `customer`, `success_return_url`, `cancel_return_url`
    - **Feasibility**: HIGH - JSON DTO with property name mapping

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/XenditPaymentSessionResponse.cs`
    - Exact structure matching Xendit API response
    - Use `[JsonPropertyName]` for snake_case mapping
    - Properties: `payment_session_id`, `status`, `reference_id`, `amount`, `currency`, `country`, `customer_id`, `expires_at`, `payment_link_url`, etc.
    - **Feasibility**: HIGH - JSON response DTO

#### Webhook DTOs

* **Target File**: NEW `PaymentGateway.Server/Payments/Models/Dtos/Xendit/Dto_XenditWebhook.cs`
    - Properties matching Xendit webhook payload structure
    - Use `[JsonPropertyName]` for snake_case mapping
    - Include `event` (string), `payment_session_id`, `status`, `reference_id`, timestamp fields
    - **Feasibility**: HIGH - Standard webhook DTO

### Phase 3: Backend Service Layer

#### Xendit API Service

* **Target File**: NEW `PaymentGateway.Server/Payments/Services/XenditService.cs`
    - Inject `IConfiguration`, `IHttpClientFactory`, `ILogger<XenditService>`
    - Method: `CreatePaymentSessionAsync(XenditPaymentSessionRequest request, bool isDevelopment)`
        - Read API key from configuration based on isDevelopment flag:
            - If `isDevelopment = true`: Use `IConfiguration["Xendit:DevelopmentApiKey"]`
            - If `isDevelopment = false`: Use `IConfiguration["Xendit:ProductionApiKey"]`
        - Use Xendit API base URL from config: `IConfiguration["Xendit:ApiBaseUrl"]`
        - POST to `/v1/payment_sessions`
        - Set Authorization header: `Basic {base64(xenditApiKey:)}`
        - Set Content-Type: `application/json`
        - Return `DataWrapper<XenditPaymentSessionResponse>`
        - Handle HTTP errors properly (400, 401, 403, 429, 500)
    - Method: `GetPaymentSessionAsync(string paymentSessionId, bool isDevelopment)`
        - GET from `/v1/payment_sessions/{id}`
        - Read appropriate API key from configuration based on isDevelopment
        - For status checks and reconciliation
    - **Feasibility**: MEDIUM-HIGH - External HTTP calls, proper error handling needed

### Phase 4: Backend Controller Layer

#### Payment Controller (Xendit-specific)

* **Target File**: NEW `PaymentGateway.Server/Payments/Controllers/PaymentController_Xendit.cs`
    - Controller attribute: `[Route("api/payment/xendit")]`
    - `[AllowAnonymous]` - Uses API key authentication
    - Inject `AppDbContext`, `XenditService`, `ILogger<PaymentController_Xendit>`
    
    - **Endpoint**: `[HttpPost("create-session")]` - Create payment session
        - Route: `/api/payment/xendit/create-session`
        - Extract API key from header: `X-API-Key`
        - Validate API key against `Db_Environment.ApiKey`
        - Get environment details including `IsDevelopment` flag
        - Map `Dto_CreatePaymentSessionRequest` to `XenditPaymentSessionRequest`
        - Generate customer reference ID if not provided
        - Set `session_type: "PAY"`, `mode: "PAYMENT_LINK"` automatically
        - Call `XenditService.CreatePaymentSessionAsync()` with environment's `IsDevelopment` flag
        - XenditService will automatically use correct API key from appsettings based on isDevelopment
        - Create `Db_PaymentXendit` record with response data
        - Return `DataWrapper<Dto_CreatePaymentSessionResponse>`
        - **Feasibility**: MEDIUM-HIGH - Complex logic but follows ApplicationController pattern
    
    - **Endpoint**: `[HttpGet("{paymentId}")]` - Get payment by internal ID
        - Route: `/api/payment/xendit/{paymentId}`
        - Validate API key and environment ownership
        - Return payment details from database
        - **Feasibility**: HIGH - Standard read operation
    
    - **Endpoint**: `[HttpGet("reference/{referenceId}")]` - Get payment by reference ID
        - Route: `/api/payment/xendit/reference/{referenceId}`
        - Validate API key and environment ownership
        - Return payment details by caller's reference ID
        - **Feasibility**: HIGH - Standard read operation

#### Webhook Controller (Xendit-specific)

* **Target File**: NEW `PaymentGateway.Server/Payments/Controllers/WebhookController_Xendit.cs`
    - Controller attribute: `[Route("api/webhook/xendit")]`
    - `[AllowAnonymous]` - Webhooks come from Xendit
    - Inject `AppDbContext`, `IHttpClientFactory`, `ILogger<WebhookController_Xendit>`
    
    - **Endpoint**: `[HttpPost]` - Handle Xendit webhook
        - Route: `/api/webhook/xendit`
        - Parse `Dto_XenditWebhook` from request body
        - Verify webhook signature (Xendit webhook verification token)
        - Find payment by `PaymentSessionId`
        - Update payment status based on webhook event:
            - `payment_session.completed` → Status = Completed, set CompletedAt
            - `payment_session.expired` → Status = Expired
            - `payment_session.canceled` → Status = Canceled
        - If environment has `WebhookUrl`, forward webhook to application
        - Use HttpClient to POST to application's webhook URL
        - Include our internal PaymentId in forwarded webhook
        - Log all webhook events for audit trail
        - Return 200 OK to acknowledge receipt
        - **Feasibility**: MEDIUM - Webhook handling with forwarding logic

### Phase 5: Configuration & Dependencies

#### App Settings

* **Target File**: EXISTING `PaymentGateway.Server/appsettings.development.json`
    - Add Xendit configuration:
    ```json
    "Xendit": {
      "ApiBaseUrl": "https://api.xendit.co",
      "DevelopmentApiKey": "xnd_development_your_test_key_here",
      "ProductionApiKey": "xnd_production_your_live_key_here",
      "WebhookVerificationToken": "your_webhook_token_here"
    }
    ```
    - Note: DevelopmentApiKey used when `Environment.IsDevelopment = true`
    - Note: ProductionApiKey used when `Environment.IsDevelopment = false`
    - Store actual keys in appsettings or User Secrets for security
    - **Feasibility**: HIGH - Simple configuration

#### Service Registration

* **Target File**: EXISTING `PaymentGateway.Server/Program.cs`
    - Register XenditService: `builder.Services.AddScoped<XenditService>();`
    - HttpClient already configured
    - **Feasibility**: HIGH - Single line addition

### Phase 6: API Key Validation Helper

* **Target File**: NEW `PaymentGateway.Server/Payments/Utils/ApiKeyValidator.cs`
    - Static helper or service class
    - Method: `ValidateApiKeyAsync(string apiKey, AppDbContext dbContext)`
    - Returns `Db_Environment` if valid, null if invalid
    - Can be reused across payment controllers
    - **Feasibility**: HIGH - Reusable validation logic

---

## Integration & Testing

### Manual Testing Checklist

* **Test 1**: Create payment session with development API key
    - POST `/api/payment/xendit/create-session`
    - Verify environment with `IsDevelopment = true` uses test Xendit API key
    - Verify payment created in database with Status = Active
    - Verify Xendit API called successfully
    - Verify payment_link_url returned

* **Test 2**: Create payment session with production API key
    - POST `/api/payment/xendit/create-session`
    - Verify environment with `IsDevelopment = false` uses live Xendit API key
    - Verify payment created with production settings

* **Test 3**: Retrieve payment by internal ID
    - GET `/api/payment/xendit/{paymentId}`
    - Verify payment details returned
    - Verify environment isolation (can't access other env's payments)

* **Test 4**: Retrieve payment by reference ID
    - GET `/api/payment/xendit/reference/{referenceId}`
    - Verify lookup by caller's reference works

* **Test 5**: Handle webhook - payment_session.completed
    - POST `/api/webhook/xendit` with completed event
    - Verify payment status updated to Completed
    - Verify CompletedAt timestamp set
    - Verify application webhook called (if configured)

* **Test 6**: Handle webhook - payment_session.expired
    - POST `/api/webhook/xendit` with expired event
    - Verify payment status updated to Expired

* **Test 7**: API key validation
    - Test with invalid API key → 401 Unauthorized
    - Test with valid API key → Success

* **Test 8**: Environment isolation
    - Create payment with App1's API key
    - Try to retrieve with App2's API key → 404 or 403

---

## Infrastructure Rules Compliance

### ✅ Backend Architecture Compliance

1. **Folder Structure**: `PaymentGateway.Server/Payments/{Controllers,Services,Models/{Dbs,Dtos/Xendit}}`
   - ✅ Follows Authorization/ and Applications/ patterns

2. **Controllers**: 
   - ✅ Contain business logic and validation
   - ✅ Use DataWrapper<T> responses
   - ✅ Direct DbContext access
   - ✅ API key authentication (not JWT)

3. **Services**:
   - ✅ XenditService only for external API integration (DRY principle)
   - ✅ No unnecessary service layer

4. **Database**:
   - ✅ ISoftDelete implementation
   - ✅ EF Core migrations
   - ✅ Naming: Db_PaymentXendit, Dto_* pattern

5. **DTOs Organization**:
   - ✅ Xendit-specific DTOs in Dtos/Xendit/ subfolder
   - ✅ Prepared for future payment gateway additions

### ⚠️ Key Design Decisions

1. **Environment IsDevelopment Flag**:
   - Determines test vs live mode
   - Defaults to `true` (development) for safety
   - Admin must explicitly set to `false` for production

2. **Xendit API Keys in Configuration**:
   - Development and production Xendit API keys stored in appsettings
   - XenditService automatically selects correct key based on Environment.IsDevelopment flag
   - Simpler architecture - no per-environment Xendit key storage
   - Generic `ApiKey` remains for internal API authentication

3. **Payment Session API** (not Invoice API):
   - Using newer Xendit API
   - More flexible for future payment methods
   - Consistent status flow (Active/Completed/Expired/Canceled)

---

## Security Considerations

1. **API Key Security**:
   - Xendit API keys stored in appsettings (use User Secrets in development, environment variables in production)
   - Never log API keys
   - HTTPS only
   - Consider using Azure Key Vault or similar for production key storage

2. **Webhook Verification**:
   - Validate webhook signature using Xendit's verification token
   - Check payment_session_id exists before processing
   - Idempotent webhook processing

3. **Environment Isolation**:
   - Strict validation: API key must own the payment
   - Query filters by EnvironmentId

4. **Development vs Production**:
   - Clear separation via IsDevelopment flag
   - Test keys never used in production
   - Audit trail for environment mode changes

---

## Migration Strategy

### Phase 1: Database Changes
1. Add `IsDevelopment` to Db_Environment with default value true (migration 1)
2. Add `Db_PaymentXendit` table (migration 2)
3. Run migrations on development database
4. Verify schema changes

### Phase 2: Backend Implementation
1. Create DTOs (Xendit-specific folder structure)
2. Create XenditService (external API integration)
3. Create PaymentController_Xendit
4. Create WebhookController_Xendit
5. Add configuration

### Phase 3: Testing
1. Unit test XenditService with mocked HttpClient
2. Integration test payment creation flow
3. Integration test webhook handling
4. Test environment isolation

### Phase 4: Deployment
1. Update production database schema
2. Configure Xendit webhook URL in Xendit dashboard
3. Set production Xendit API keys in appsettings or environment variables
4. Update Environment records to set IsDevelopment = false for production environments
5. Monitor webhook delivery and payment creation

---

## Rollout Plan

**Phase 0**: Environment schema (1 hour)
- Add IsDevelopment field with default value true
- Create and run migration
- Update appsettings with Xendit API keys (DevelopmentApiKey and ProductionApiKey)

**Phase 1**: Database layer (1-2 hours)
- Create Db_PaymentXendit entity
- Configure DbContext
- Run migration

**Phase 2**: DTOs (1-2 hours)
- Create all Xendit-specific DTOs in proper subfolder
- Request, response, webhook DTOs

**Phase 3**: Service layer (2-3 hours)
- Implement XenditService
- Handle development/production mode
- Test Xendit API integration manually

**Phase 4**: Controller layer (3-4 hours)
- Implement PaymentController_Xendit
- API key validation
- Environment ownership checks
- Test payment creation end-to-end

**Phase 5**: Webhook handling (2-3 hours)
- Implement WebhookController_Xendit
- Webhook signature verification
- Application webhook forwarding
- Test with Xendit webhook simulator

**Phase 6**: Testing & validation (2-3 hours)
- Full integration testing
- Development vs production mode testing
- Environment isolation testing
- Error scenario testing

**Total Estimated Time**: 12-18 hours

---

## Post-Implementation Features

- Payment list/search with pagination
- Payment cancellation endpoint
- Payment status sync/reconciliation with Xendit
- Payment analytics dashboard
- Webhook retry mechanism
- Webhook delivery status tracking
- Multi-currency support validation
- Rate limiting per API key
