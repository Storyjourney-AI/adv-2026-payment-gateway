
## Checklist
- [x] Core Foundation Setup
- [x] Domain: Authentication & Security
- [ ] Domain: Core API & Data
- [ ] Final Deployment & Observability

---

## Domain: Authentication & Security (Updated Paths)

### Backend Implementation

* Target File: NEW `backend/Auth/Services/JwtService.cs`
    - Implement the token generation function (`createToken`).
    - Implement the token validation function (`validateToken`).

* Target File: NEW `backend/Auth/Controllers/AuthController.cs`
    - Define the `/login` endpoint (accepts `Auth/Model/Dto/Dto_LoginRequest.cs`).
    - Define the `/register` endpoint (accepts `Auth/Model/Dto/Dto_RegisterRequest.cs`).
    - Define the `/refresh` endpoint.

* Target File: NEW `backend/Auth/Middleware/AuthMiddleware.cs`
    - Add logic to extract JWT from request headers.
    - Integrate with `Auth/Services/JwtService.cs` to validate the token.
    - Implement role-based access control checks.

* Target File: EXISTING `backend/appsettings.json`
    - Add configuration keys for JWT secret and token expiry time.

### Integration & Testing

* Target File: NEW `backend/Auth/Tests/SecurityTests.cs`
    - Write unit tests for the authentication services and middleware.

* Target File: NEW `frontend/src/api/auth.api.ts`
    - Define client-side functions to call login, register, and refresh endpoints.

* Target File: NEW `frontend/src/state/auth/useAuth.ts`
    - Implement state management logic for storing user tokens and profile data.

### Visuals

* Target File: `frontend/src/pages/Page_Login.cshtml`
    - Create the user sign-in form UI and connect it to `auth.api.ts`.

* Target File: `frontend/src/pages/Page_Register.cshtml`
    - Build the user registration form UI.

* Target File: `frontend/src/components/layouts/Layout_Protected.tsx`
    - Use `useAuth.ts` hook to check authentication status and protect routes.

---

## Domain: Core API & Data

This domain represents the primary business logic for the application (e.g., managing files, products, or core entities). I will assume this is where your original Phase 1 (API Foundation) and Phase 2 (Integration) tasks for the *actual data* will live, using your new folder convention (e.g., `Files/Controllers/FileController.cs`).

### Backend Implementation

* Target File: NEW `backend/Files/Database/FilesDbContext.cs`
    - Define the database context for core data models.

* Target File: NEW `backend/Files/Model/Db/Db_File.cs`
    - Define the core database models (e.g., File, Item, Product).

* Target File: NEW `backend/Files/Model/Dto/Dto_FileRequest.cs`
    - Define the DTOs for data input and output.

* Target File: NEW `backend/Files/Services/FileService.cs`
    - Implement core business logic (CRUD operations, filtering, storage).

* Target File: NEW `backend/Files/Controllers/FileController.cs`
    - Define core API endpoints (e.g., GET list, GET by ID, POST, PUT, DELETE).
    - Apply `Auth/Middleware/AuthMiddleware.cs` to these endpoints.

### Integration & Testing

* Target File: EXISTING `frontend/src/components/api-client.ts`
    - Update the core client to handle authenticated requests (attaching JWT).

* Target File: NEW `frontend/src/api/files.api.ts`
    - Create functions for fetching, creating, and modifying core data.

* Target File: NEW `frontend/src/state/files/useFiles.ts`
    - Implement state management for displaying and manipulating the core data list.

* Target File: DELETE `frontend/src/mock/mock-api.ts`
    - Remove temporary mock data files now that real integration is occurring.

### Visuals

* Target File: EXISTING `frontend/src/pages/Page_Home.cshtml`
    - Connect the page to the real API endpoints defined in `files.api.ts`.

* Target File: NEW `frontend/src/components/Display_Table.tsx`
    - Create a reusable component to display the list of core data items (e.g., a file list).

* Target File: NEW `frontend/src/components/Form_Edit.tsx`
    - Create a form component for creating or editing a core data item.