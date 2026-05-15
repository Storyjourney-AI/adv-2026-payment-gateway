# Execution Plan: Infrastructure PRD

**Source**: infra.prd.md  
**Created**: January 22, 2026  
**Status**: Planning

---

## Checklist
- [ ] Phase 1: Backend Data Models & Database Setup
- [ ] Phase 2: Backend API Implementation
- [ ] Phase 3: Frontend Service Layer & State Management
- [ ] Phase 4: Frontend UI Components & Pages
- [ ] Phase 5: Integration & Testing

---

## Phase 1: Backend Data Models & Database Setup

### 1.1 Create Database Models

* Target File: NEW `PaymentGateway.Server/Applications/Models/Dbs/Db_Application.cs`
    - Create `Db_Application` entity with properties:
        - `Id` (Guid, PK)
        - `Name` (string, required)
        - `Description` (string, nullable)
        - `UserId` (Guid, FK to AspNetUsers)
        - `CreatedAt` (DateTime)
        - `UpdatedAt` (DateTime)
        - `IsDeleted` (bool, for soft delete)
        - `DeletedAt` (DateTime?, nullable)
    - Implement `ISoftDelete` interface
    - Add navigation property: `User` (Db_ApplicationUser)
    - Add navigation property: `Environments` (List<Db_Environment>)
    - **Feasibility**: HIGH - Follows existing pattern from Authorization models

* Target File: NEW `PaymentGateway.Server/Applications/Models/Dbs/Db_Environment.cs`
    - Create `Db_Environment` entity with properties:
        - `Id` (Guid, PK)
        - `ApplicationId` (Guid, FK to Db_Application)
        - `Name` (string, required - "staging", "production", etc.)
        - `ApiKey` (string, auto-generated, indexed)
        - `AllowedOrigins` (string, JSON array or comma-separated, "*" for all)
        - `WebhookUrl` (string, nullable)
        - `SuccessResponseUrl` (string, required)
        - `FailureResponseUrl` (string, required)
        - `CreatedAt` (DateTime)
        - `UpdatedAt` (DateTime)
        - `IsDeleted` (bool)
        - `DeletedAt` (DateTime?, nullable)
    - Implement `ISoftDelete` interface
    - Add navigation property: `Application` (Db_Application)
    - **Feasibility**: HIGH - Standard entity model creation

### 1.2 Update DbContext

* Target File: EXISTING `PaymentGateway.Server/Databases/AppDbContext.cs`
    - Add DbSet<Db_Application> for Applications table
    - Add DbSet<Db_Environment> for Environments table
    - Configure table schema in OnModelCreating:
        - Applications table in "payment" schema
        - Environments table in "payment" schema
        - Configure soft delete query filters for both entities
        - Configure one-to-many relationship (Application -> Environments)
        - Configure foreign key relationship (Application -> User)
        - Add unique index on Environment.ApiKey
    - **Feasibility**: HIGH - DbContext already exists with similar configurations

### 1.3 Create Initial Migration

* Target File: TERMINAL COMMAND
    - Run: `dotnet ef migrations add create-application-and-environment --project PaymentGateway.Server`
    - Run: `dotnet ef database update --project PaymentGateway.Server`
    - **Feasibility**: HIGH - EF Core already configured, migrations folder exists

### 1.4 Seed Super Admin User

* Target File: EXISTING `PaymentGateway.Server/Authorization/Services/AuthService.cs`
    - Create method: `SeedSuperAdminAsync()` in AuthService
    - Check if user "yoshua@advine.id" exists
    - If not, create user with:
        - Email: yoshua@advine.id
        - Password: P@ssw0rd
        - Assign "Super Admin" role
        - Set `EmailConfirmed = true`
    - **Feasibility**: HIGH - UserManager already available in AuthService

* Target File: EXISTING `PaymentGateway.Server/Program.cs`
    - Call `AuthService.SeedSuperAdminAsync()` during application startup
    - Place call after services are built but before `app.Run()`
    - Use service scope to resolve AuthService
    - **Feasibility**: HIGH - Standard startup seeding pattern

* Target File: NEW `PaymentGateway.Server/Authorization/Services/PolicyService.cs` (if policy seeding exists)
    - Move any policy seeding logic to PolicyService for separation of concerns
    - Create method: `SeedPoliciesAsync()` if policies need database seeding
    - Call from Program.cs similar to AuthService seeding
    - **Feasibility**: HIGH - Clear separation of concerns, optional if no policy DB seeding needed

---

## Phase 2: Backend API Implementation

### 2.1 Create DTOs for Applications

* Target File: NEW `PaymentGateway.Server/Applications/Models/Dtos/Dto_ApplicationRequest.cs`
    - Properties: `Name` (string, required), `Description` (string, nullable)
    - Add data annotations for validation
    - **Feasibility**: HIGH - Similar DTOs exist in Authorization folder

* Target File: NEW `PaymentGateway.Server/Applications/Models/Dtos/Dto_ApplicationResponse.cs`
    - Properties: `Id`, `Name`, `Description`, `UserId`, `CreatedAt`, `UpdatedAt`
    - Include nested `List<Dto_EnvironmentResponse>` for environments
    - **Feasibility**: HIGH - Standard DTO pattern

* Target File: NEW `PaymentGateway.Server/Applications/Models/Dtos/Dto_ApplicationListItem.cs`
    - Properties: `Id`, `Name`, `Description`, `CreatedAt`, `EnvironmentCount`
    - For list/grid display purposes
    - **Feasibility**: HIGH - Pagination wrapper already exists

### 2.2 Create DTOs for Environments

* Target File: NEW `PaymentGateway.Server/Applications/Models/Dtos/Dto_EnvironmentRequest.cs`
    - Properties: `Name`, `AllowedOrigins`, `WebhookUrl`, `SuccessResponseUrl`, `FailureResponseUrl`
    - Add validation: URLs must be valid, Name required
    - **Feasibility**: HIGH - Standard DTO

* Target File: NEW `PaymentGateway.Server/Applications/Models/Dtos/Dto_EnvironmentResponse.cs`
    - Properties: `Id`, `ApplicationId`, `Name`, `ApiKey`, `AllowedOrigins`, `WebhookUrl`, `SuccessResponseUrl`, `FailureResponseUrl`, `CreatedAt`, `UpdatedAt`
    - **Feasibility**: HIGH - Standard DTO

### 2.3 Create Application Controller

* Target File: NEW `PaymentGateway.Server/Applications/Controllers/ApplicationController.cs`
    - **Endpoint**: `GET /api/application` - Get paginated list of applications
        - Use `PaginationWrapper<Dto_ApplicationListItem>`
        - Filter by current user (unless Super Admin)
        - Include search functionality
        - Return `DataWrapper<PaginationWrapper<Dto_ApplicationListItem>>`
    - **Endpoint**: `GET /api/application/{id}` - Get single application by ID
        - Include all environments
        - Return `DataWrapper<Dto_ApplicationResponse>`
    - **Endpoint**: `POST /api/application` - Create new application
        - Accept `Dto_ApplicationRequest`
        - Auto-create "staging" and "production" environments with generated API keys
        - Return `DataWrapper<Dto_ApplicationResponse>`
    - **Endpoint**: `PUT /api/application/{id}` - Update application
        - Accept `Dto_ApplicationRequest`
        - Return `DataWrapper<Dto_ApplicationResponse>`
    - **Endpoint**: `DELETE /api/application/{id}` - Soft delete application
        - Set IsDeleted = true, DeletedAt = DateTime.UtcNow
        - Also soft delete all related environments
        - Return `DataWrapper<bool>`
    - Add `[Authorize(Policy = "RequireUser")]` to all endpoints
    - Inject `AppDbContext`, `UserManager<Db_ApplicationUser>`, `ILogger`
    - **Feasibility**: HIGH - Similar controller patterns exist in Authorization/Controllers

### 2.4 Create Environment Controller

* Target File: NEW `PaymentGateway.Server/Applications/Controllers/EnvironmentController.cs`
    - **Endpoint**: `GET /api/environment/by-application/{applicationId}` - Get all environments for an application
        - Return `DataWrapper<List<Dto_EnvironmentResponse>>`
    - **Endpoint**: `GET /api/environment/{id}` - Get single environment by ID
        - Return `DataWrapper<Dto_EnvironmentResponse>`
    - **Endpoint**: `POST /api/environment` - Create new environment
        - Accept `Dto_EnvironmentRequest` + `ApplicationId`
        - Auto-generate new API key using `Guid.NewGuid()` or secure random generator
        - Return `DataWrapper<Dto_EnvironmentResponse>`
    - **Endpoint**: `PUT /api/environment/{id}` - Update environment
        - Accept `Dto_EnvironmentRequest`
        - Return `DataWrapper<Dto_EnvironmentResponse>`
    - **Endpoint**: `POST /api/environment/{id}/regenerate-key` - Regenerate API key
        - Generate new API key
        - Return `DataWrapper<Dto_EnvironmentResponse>`
    - **Endpoint**: `DELETE /api/environment/{id}` - Soft delete environment
        - Return `DataWrapper<bool>`
    - Add `[Authorize(Policy = "RequireUser")]` to all endpoints
    - **Feasibility**: HIGH - Standard CRUD controller

### 2.5 Add Change Password Endpoint

* Target File: EXISTING `PaymentGateway.Server/Authorization/Controllers/AuthController.cs`
    - **Endpoint**: `POST /api/auth/change-password`
    - Accept: Email, CurrentPassword, NewPassword
    - Create DTO: `Dto_ChangePasswordRequest`
    - Validate current password
    - Check password complexity
    - Update password using `UserManager.ChangePasswordAsync`
    - Return `DataWrapper<bool>`
    - Add `[Authorize]` attribute
    - **Feasibility**: HIGH - UserManager already available, similar patterns exist

### 2.6 Comment Out Registration Endpoint

* Target File: EXISTING `PaymentGateway.Server/Authorization/Controllers/AuthController.cs`
    - Comment out or add `[Obsolete]` attribute to `[HttpPost("register")]` endpoint
    - Keep code for future reference
    - **Feasibility**: HIGH - Simple code modification

---

## Phase 3: Frontend Service Layer & State Management

### 3.1 Create Application Types

* Target File: NEW `paymentgateway.client/app/services/application/types/application.types.ts`
    - Define interfaces matching DTOs:
        - `Dto_ApplicationRequest`
        - `Dto_ApplicationResponse`
        - `Dto_ApplicationListItem`
        - `Dto_EnvironmentRequest`
        - `Dto_EnvironmentResponse`
    - Use camelCase for properties (frontend convention)
    - **Feasibility**: HIGH - Similar types exist in auth service

### 3.2 Create Application API Wrapper

* Target File: NEW `paymentgateway.client/app/services/application/utils/application.api.ts`
    - Function: `getApplications(params: PaginationRequest)` - GET paginated list
    - Function: `getApplicationById(id: string)` - GET single application
    - Function: `createApplication(data: Dto_ApplicationRequest)` - POST create
    - Function: `updateApplication(id: string, data: Dto_ApplicationRequest)` - PUT update
    - Function: `deleteApplication(id: string)` - DELETE soft delete
    - All functions return `Promise<DataWrapper<T>>`
    - Use `import.meta.env.VITE_API_BASE_URL`
    - Include `credentials: 'include'` for cookies
    - **Feasibility**: HIGH - Pattern exists in auth service

### 3.3 Create Environment API Wrapper

* Target File: NEW `paymentgateway.client/app/services/application/utils/environment.api.ts`
    - Function: `getEnvironmentsByApplication(applicationId: string)`
    - Function: `getEnvironmentById(id: string)`
    - Function: `createEnvironment(data: Dto_EnvironmentRequest & { applicationId: string })`
    - Function: `updateEnvironment(id: string, data: Dto_EnvironmentRequest)`
    - Function: `regenerateApiKey(id: string)`
    - Function: `deleteEnvironment(id: string)`
    - Same patterns as application.api.ts
    - **Feasibility**: HIGH - Reuse existing API patterns

### 3.4 Create Application Store (Optional - for caching)

* Target File: NEW `paymentgateway.client/app/services/application/store/application.store.ts`
    - Use Zustand for state management
    - Cache applications list with TTL (5 minutes)
    - Provide cache invalidation functions
    - Store: `applications`, `currentApplication`, `isCacheValid`, `setCache`, `clearCache`
    - **Feasibility**: HIGH - Zustand v5.0.10 is installed, existing useAuth pattern uses Zustand store

### 3.5 Create Application Hook

* Target File: NEW `paymentgateway.client/app/services/application/hooks/useApplications.ts`
    - Hook for reactive application management
    - Functions: `fetchApplications`, `createApplication`, `updateApplication`, `deleteApplication`
    - Manage loading, error states
    - Update store cache after mutations
    - **Feasibility**: HIGH - Similar hooks exist for auth

### 3.6 Create Barrel Export

* Target File: NEW `paymentgateway.client/app/services/application/index.ts`
    - Export all types, hooks, and API functions
    - **Feasibility**: HIGH - Simple export file

### 3.7 Add Change Password API

* Target File: EXISTING `paymentgateway.client/app/services/auth/utils/auth.api.ts`
    - Add function: `changePassword(data: { email, currentPassword, newPassword })`
    - Return `Promise<DataWrapper<boolean>>`
    - **Feasibility**: HIGH - Add to existing file

---

## Phase 4: Frontend UI Components & Pages

### 4.1 Remove Registration from Login Page

* Target File: EXISTING `paymentgateway.client/app/routes/auth/Page_Login.tsx`
    - Remove "Register" link/button
    - Remove navigation to registration page
    - Keep only login form
    - **Feasibility**: HIGH - Simple UI modification

### 4.2 Remove/Hide Registration Page

* Target File: EXISTING `paymentgateway.client/app/routes/auth/Page_Login.tsx`
    - Registration UI is embedded in Page_Login.tsx using Tabs component
    - Remove the "Register" tab from TabsList
    - Remove the "register" TabsContent section
    - Remove registerForm, handleRegister, and registerSchema
    - Keep only login form and functionality
    - **Feasibility**: HIGH - Registration is in same file as login, just remove tab UI

### 4.3 Create Application List Page (Admin Dashboard)

* Target File: NEW `paymentgateway.client/app/routes/dashboard/Page_Applications.tsx`
    - Use `useApplications` hook
    - Display table with columns: Name, Description, Environment Count, Created At, Actions
    - Add search input (filter by name)
    - Add pagination controls (use existing PaginationWrapper pattern)
    - Add "Create Application" button → opens dialog/modal
    - Add Edit/Delete actions per row
    - Use shadcn components: `Table`, `Button`, `Input`, `Dialog`
    - **Feasibility**: HIGH - Similar patterns exist, UI components available

### 4.4 Create Application Form Component

* Target File: NEW `paymentgateway.client/app/routes/dashboard/components/Compo_ApplicationForm.tsx`
    - Form fields: Name (required), Description (optional)
    - Use React Hook Form + Zod for validation
    - Schema: Name required (min 3 chars), Description optional
    - OnSubmit: Call `createApplication` or `updateApplication` from hook
    - Use shadcn components: `Form`, `Input`, `Textarea`, `Button`
    - **Feasibility**: HIGH - React Hook Form already in use

### 4.5 Create Application Detail Page

* Target File: NEW `paymentgateway.client/app/routes/dashboard/Page_ApplicationDetail.tsx`
    - Display application details
    - List all environments in a table
    - Show environment details: Name, API Key (with copy button), URLs, Created At
    - Add "Create Environment" button
    - Add Edit/Delete/Regenerate Key actions per environment row
    - Use route parameter for application ID
    - **Feasibility**: HIGH - Standard detail page

### 4.6 Create Environment Form Component

* Target File: NEW `paymentgateway.client/app/routes/dashboard/components/Compo_EnvironmentForm.tsx`
    - Form fields: Name, Allowed Origins, Webhook URL, Success URL, Failure URL
    - Validation: URLs must be valid, Name required
    - Use React Hook Form + Zod
    - **Feasibility**: HIGH - Similar form patterns

### 4.7 Update Dashboard Layout

* Target File: EXISTING `paymentgateway.client/app/components/Layout_Dashboard.tsx`
    - File exists with sidebar navigation
    - Update navigation to show "Applications" menu item
    - Keep or update existing dashboard menu structure
    - Use existing Sidebar components (SidebarMenu, SidebarMenuItem, etc.)
    - **Feasibility**: HIGH - Layout exists, just needs menu item updates

### 4.8 Update Routes Configuration

* Target File: EXISTING `paymentgateway.client/app/routes.ts`
    - Add route: `/dashboard/applications` → `routes/dashboard/Page_Applications.tsx`
    - Add route: `/dashboard/applications/:id` → `routes/dashboard/Page_ApplicationDetail.tsx`
    - Place routes under Layout_Dashboard (which is under Layout_Protected)
    - No separate registration route exists (embedded in login page)
    - **Feasibility**: HIGH - Routes file exists, just add new application routes under Layout_Dashboard

---

## Phase 5: Integration & Testing

### 5.1 Test Super Admin Seeding

* Target File: TERMINAL/MANUAL TEST
    - Clear database or run fresh migration
    - Start application
    - Verify "yoshua@advine.id" user is created
    - Verify user has "Super Admin" role
    - Test login with P@ssw0rd
    - **Feasibility**: HIGH - Manual testing required

### 5.2 Test Application CRUD

* Target File: MANUAL TEST / Playwright (future)
    - Create new application via UI
    - Verify default staging & production environments are created
    - Verify API keys are auto-generated
    - Update application name/description
    - Delete application (verify soft delete)
    - **Feasibility**: HIGH - Standard integration testing

### 5.3 Test Environment CRUD

* Target File: MANUAL TEST / Playwright (future)
    - Create additional environment for an application
    - Update environment URLs
    - Regenerate API key
    - Delete environment
    - **Feasibility**: HIGH - Standard integration testing

### 5.4 Test Change Password

* Target File: MANUAL TEST
    - Login as super admin
    - Change password via new endpoint
    - Logout and login with new password
    - **Feasibility**: HIGH - Manual testing

### 5.5 Test Pagination & Search

* Target File: MANUAL TEST
    - Create multiple applications (10+)
    - Test pagination controls
    - Test search by application name
    - Verify results are filtered correctly
    - **Feasibility**: HIGH - Standard UI testing

### 5.6 Verify Registration is Disabled

* Target File: MANUAL TEST
    - Verify registration page is not accessible
    - Verify login page has no registration link
    - Verify `/api/auth/register` endpoint returns 404 or is commented out
    - **Feasibility**: HIGH - Simple verification

---

## Notes & Considerations

### Architecture Alignment
- ✅ Follows domain-based folder structure (Applications/)
- ✅ Uses DataWrapper pattern for API responses
- ✅ Implements soft delete with ISoftDelete interface
- ✅ Uses JWT + Refresh Token authentication
- ✅ Follows naming conventions (Db_*, Dto_*, Page_*, Compo_*)
- ✅ Uses policy-based authorization (RequireUser, RequireSuperAdmin)
- ✅ Separation of concerns: User seeding in AuthService, Policy seeding in PolicyService
- ✅ Controllers contain business logic directly (no service layer unless needed for DRY)

### Potential Issues
- **API Key Generation**: Need to ensure API keys are cryptographically secure (use `Guid.NewGuid().ToString("N")` or better)
- **Allowed Origins**: Consider implementing as JSON array in database for better querying
- **Soft Delete Cascading**: Deleting application should cascade soft delete to environments
- **Authorization**: Verify users can only access their own applications (unless Super Admin)

### Future Enhancements (Out of Scope)
- API key authentication middleware for payment endpoints
- Webhook validation and retry logic
- Environment-specific rate limiting
- Audit logs for application/environment changes
