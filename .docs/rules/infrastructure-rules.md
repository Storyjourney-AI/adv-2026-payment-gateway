# Infrastructure Rules

## Stack Overview

### Frontend
- **Framework**: React Router 7 (SPA mode, SSR disabled)
- **Build Tool**: Vite 7.1
- **Styling**: Tailwind CSS 4.1
- **Port**: localhost:5400 (dev)
- **Environment**: `.env` with `VITE_API_BASE_URL=http://localhost:5450`

### Backend
- **Framework**: ASP.NET Core 8.0
- **Database**: PostgreSQL with Entity Framework Core
- **Authentication**: JWT + Refresh Token (HttpOnly cookie)
- **Storage**: Google Cloud Storage (GCS)
- **Port**: localhost:5450 (dev)
- **CORS**: Configured for localhost:5400

### Containerization
- **Docker**: Multi-stage build
  - Frontend: Node 18 Alpine → builds to `build/client`
  - Backend: .NET 8 SDK → publishes to `wwwroot`
  - Final: .NET 8 Runtime serving SPA

---

## Backend Architecture

### Folder Structure Pattern
```
RabbitHole.Server/
├── Authorization/
│   ├── Controllers/        # API endpoints
│   ├── Services/          # Business logic
│   ├── Models/
│   │   ├── Dbs/          # Database entities (Db_*)
│   │   └── Dtos/         # Data transfer objects (Dto_*)
│   └── Utils/            # Helper classes (Constants, Extensions)
├── SystemConfig/          # System-level features (Email, Settings)
│   ├── Email/
│   │   ├── Controllers/
│   │   ├── Services/
│   │   └── Models/Dtos/
├── Common/
│   ├── Models/           # Shared models (DataWrapper, PaginationWrapper)
│   └── Interfaces/       # Shared interfaces (ISoftDelete)
├── Databases/            # DbContext classes
└── Migrations/           # EF Core migrations
```

### Separation of Concerns

#### Controllers (API Layer)
- Handle HTTP requests/responses
- **Contain business logic and validation by default**
- Validate ModelState
- Interact with DbContext, UserManager/RoleManager directly
- Return `DataWrapper<T>` responses with proper HTTP status codes
- Set cookies for refresh tokens
- Use `[Authorize]` attributes with Policies or Roles
- **Keep logic in controllers to minimize layers**

#### Services (Optional - Mandatory in DRY Principle)
- **Only create services when:**
  - Logic is reused across multiple controllers
  - Complex business rules need isolation
  - Transaction orchestration spans multiple operations
- Contain shared business rules and validation
- Return `DataWrapper<T>` with success/failure states
- Log important operations
- **Never access HttpContext directly**
- **Default approach: Start without services, refactor when duplication occurs**

#### Authentication & Authorization Pattern
- **JWT Access Token**: Short-lived (15 min), stored in memory, contains claims
- **Refresh Token**: Long-lived (7 days), HttpOnly cookie, stored in database
- **First User**: Automatically assigned Super Admin role with all permissions
- **Role Seeding**: Super Admin and User roles seeded on startup

##### Authorization Strategy (MVP: Role-based Policies)
- **Use policy-based authorization** via `AuthorizationPolicyExtensions.cs`
- **Policies wrap role requirements** for cleaner controller code
- **Future-ready**: Permission-based policies commented out, ready to enable when needed

##### Adding New Authorization Policies
1. Add policy in `Authorization/Utils/AuthorizationPolicyExtensions.cs`:
   ```csharp
   // Role-based policy (MVP approach)
   options.AddPolicy("RequireAdmin", policy => 
       policy.RequireRole("Admin", "Super Admin"));
   
   ```

2. Use in controllers:
   ```csharp
   [Authorize(Policy = "RequireAdmin")]      // Requires Admin or Super Admin role
   [Authorize(Policy = "RequireSuperAdmin")] // Requires Super Admin role only
   [Authorize(Policy = "RequireUser")]       // Requires User or Super Admin role
   ```

##### Existing Policies (in AuthorizationPolicyExtensions.cs)
| Policy | Roles Required |
|--------|----------------|
| `RequireSuperAdmin` | Super Admin |
| `RequireUser` | User, Super Admin |
| `RequireAdmin` | Admin, Super Admin |

##### When to Create New Policies
- **New feature domain** → Create domain-specific policy (e.g., `RequireAdmin` for LLM management)
- **Different role combination** → Create new policy with required roles
- **Fine-grained permissions** → Uncomment permission-based pattern when MVP complete

#### Response Pattern
```csharp
// Success
return Ok(DataWrapper<T>.Succeed(data, message: "Success message"));

// Not Found
return NotFound(DataWrapper<T>.NotFound(message: "Resource not found"));

// Bad Request
return BadRequest(DataWrapper<T>.BadRequest(
    message: "Validation failed", 
    errors: errorsList));

// Unauthorized
return Unauthorized(DataWrapper<T>.Unauthorized(message: "Invalid credentials"));

// Internal Error
return StatusCode(500, DataWrapper<T>.Fail_InternalError(
    message: "An error occurred"));
```

---

## Frontend Architecture

### Folder Structure Pattern
```
rabbithole.client/
├── app/
│   ├── services/              # Domain services (@services alias)
│   │   ├── auth/
│   │   │   ├── index.ts          # Barrel export
│   │   │   ├── hooks/
│   │   │   │   └── useAuth.ts    # React hook
│   │   │   ├── store/
│   │   │   │   └── auth.store.ts # Zustand store
│   │   │   ├── types/
│   │   │   │   ├── auth.types.ts      # DTOs (Dto_*)
│   │   │   │   └── authStore.types.ts # Store types
│   │   │   └── utils/
│   │   │       ├── auth.api.ts   # API calls
│   │   │       └── jwt.utils.ts  # JWT helpers
│   │   ├── user/
│   │   │   ├── index.ts
│   │   │   ├── hooks/
│   │   │   │   └── useUsers.ts
│   │   │   ├── store/
│   │   │   │   └── user.store.ts
│   │   │   ├── types/
│   │   │   │   └── user.types.ts
│   │   │   └── utils/
│   │   │       └── user.api.ts
│   │   ├── role/
│   │   └── email/
│   ├── components/           # Shared components
│   │   ├── Layout_Protected.tsx  # Auth guard layout
│   │   ├── Layout_Admin.tsx      # Admin layout
│   │   ├── Compo_Navbar.tsx      # Reusable navbar
│   │   └── ui/                   # UI primitives (shadcn)
│   │       ├── button.tsx
│   │       ├── card.tsx
│   │       └── input.tsx
│   ├── routes/                   # Page components (all Page_*)
│   │   ├── Page_Home.tsx         # Home page (/)
│   │   ├── Page_400.tsx          # Bad request error
│   │   ├── Page_401.tsx          # Unauthorized error
│   │   ├── Page_403.tsx          # Forbidden error
│   │   ├── auth/                 # Auth pages
│   │   │   ├── Page_Login.tsx
│   │   │   └── Page_Register.tsx
│   │   └── admin/                # Admin pages
│   │       ├── Page_Users.tsx
│   │       └── Page_Roles.tsx
│   ├── root.tsx                  # Root layout
│   └── routes.ts                 # Route config
├── public/                  # Static assets
├── .env                     # Environment variables
├── vite.config.ts           # Vite config with @services alias
└── react-router.config.ts   # React Router config (ssr: false)
```

### Naming Conventions

#### File Naming Conventions

- **Page/Route components**: `Page_*` (e.g., `Page_Home.tsx`, `Page_Login.tsx`, `Page_400.tsx`)
  - All route files in `/app/routes/` must use `Page_` prefix
  - Use PascalCase after prefix: `Page_UserManagement.tsx`
  - Exception: React Router special files (`_index.tsx`, `_layout.tsx`)

- **Layout components**: `Layout_*` (e.g., `Layout_Protected.tsx`, `Layout_Admin.tsx`)
  - Used for layout wrappers, route guards, and structural components
  - Typically contain `<Outlet />` for nested routes
  - Placed in `/app/components/` or route-specific folders

- **Regular components**: `Compo_*` (e.g., `Compo_UserCard.tsx`, `Compo_RoleForm.tsx`)
  - Used for all other reusable components
  - Placed in `/app/components/` or route-specific component folders

- **UI primitives**: No prefix (e.g., `button.tsx`, `card.tsx`, `input.tsx`)
  - Shadcn components in `/app/components/ui/`
  - Keep lowercase filenames for UI library components

#### Other Naming Conventions
- **Types**: 
  - Backend DTOs: `Dto_RegisterResponse`, `Dto_UserListItem`
  - Database entities: `Db_ApplicationUser`, `Db_ApplicationRole`
  - Store types: `AuthStore`, `UserStore`
- **Services/Utils**: lowercase with dots
  - `auth.api.ts`, `user.store.ts`, `jwt.utils.ts`
- **Route configuration**: Reference pages without prefix in routes.ts
  - `index("routes/Page_Home.tsx")` → accessible at `/`
  - `route("login", "routes/Page_Login.tsx")` → accessible at `/login`

### Separation of Concerns

#### `*.api.ts` - API Wrapper Layer
- Pure functions that wrap backend API calls
- Use `fetch` with proper error handling
- Return `DataWrapper<T>` from backend
- Include `credentials: 'include'` for cookies
- **Must be part of their respective service domain** (e.g., `user.api.ts` in `/app/services/user/utils/`)
- **Use `import.meta.env.VITE_API_BASE_URL` for API base URL**
- **No state management or React hooks**
- **Can be called directly from components for simple, one-off API calls**
```typescript
const API_BASE = import.meta.env.VITE_API_BASE_URL || ''; // empty string for single deployment same base as backend

export async function getUsers(params: PaginationRequest): Promise<DataWrapper<PaginationWrapper<UserListItem>>> {
  const response = await fetch(`${API_BASE}/api/users?page=${params.page}&pageSize=${params.pageSize}`, {
    credentials: 'include'
  });
  return response.json();
}
```

#### `*.store.ts` - State Management (Zustand)
- In-memory cache with TTL (Time To Live)
- CRUD operations on cached data
- Cache invalidation utilities
- **Only use when reactive state management is needed**
- **No API calls directly** (call from hooks)
```typescript
export const useUserStore = create<UserStore>((set, get) => ({
  cache: { data: [], timestamp: 0, ttl: 5 * 60 * 1000 },
  isCacheValid: () => Date.now() - get().cache.timestamp < get().cache.ttl,
  setCache: (data) => set({ cache: { ...get().cache, data, timestamp: Date.now() } })
}));
```

#### `useX.ts` - Domain Logic Hook (REACTIVE ONLY)
- **Only create hooks when reactive behavior is needed:**
  - Data needs to be shared across multiple components
  - State changes should trigger re-renders
  - Caching/synchronization is required
  - Complex state orchestration (loading, error, data states)
- React hook for specific domain (useAuth, useUsers, useRoles)
- Manages loading, error states
- Calls `*.api.ts` functions
- Updates `*.store.ts` cache
- Provides domain-specific utilities
```typescript
export function useUsers() {
  const [isLoading, setIsLoading] = useState(false);
  const store = useUserStore();

  const fetchUsers = async (params) => {
    setIsLoading(true);
    const result = await getUsers(params);
    if (result.success) store.setCache(result.data);
    setIsLoading(false);
  };

  return { users: store.cache.data, isLoading, fetchUsers };
}
```

#### Pages & Components
- **Pages**: Top-level route components in `pages/`
- **Components**:
  - `/components/ui/`: Reusable UI primitives (button, card, input)
  - `/pages/*/components/`: Page-specific components
- **For simple API calls**: Call `*.api.ts` directly (e.g., form submissions, one-time actions)
  - Preferred for one-off operations (create, update, delete)
  - Avoids useEffect complexity and infinite loops
- **For reactive data**: Use hooks when you need state management, caching, or cross-component synchronization
- **useEffect usage**: Only when reactive behavior is absolutely necessary
  - Avoid for simple fetch/post operations
  - Minimize to prevent infinite loops and excessive re-renders
  - Prefer direct API calls in event handlers
- Handle user interactions
```typescript
// ✅ PREFERRED: Direct API call in event handler
const handleSubmit = async (data) => {
  const result = await createUser(data); // Direct API call
  if (result.success) toast.success("User created");
};

// ❌ AVOID: useEffect for simple operations
useEffect(() => {
  fetchUsers(); // Can cause infinite loops if not careful
}, []); // Easy to miss dependencies

// ✅ ACCEPTABLE: useEffect only when reactive behavior is needed
const { users, isLoading, fetchUsers } = useUsers(); // Hook manages state reactively
useEffect(() => {
  fetchUsers(); // Only if list must update on mount
}, []); // Hook handles dependencies internally
```

#### Forms Pattern (React Hook Form + Zod)
```typescript
const schema = z.object({
  email: z.string().email(),
  password: z.string().min(8).regex(/[A-Z]/).regex(/[0-9]/).regex(/[^A-Za-z0-9]/)
});

const { register, handleSubmit, formState: { errors, isSubmitting }, control } = useForm({
  resolver: zodResolver(schema),
  mode: "onBlur"
});

// Standard input
<Input {...register("email")} />

// Checkbox with Controller
<Controller
  control={control}
  name="isActive"
  render={({ field }) => <Checkbox checked={field.value} onCheckedChange={field.onChange} />}
/>
```

#### Routing Pattern
```typescript
// Protected routes with authorization
<Route element={<Outlet context={{ requiredClaims: [{ claim: 'permission', value: 'user_view' }] }} />}>
  <Route element={<Layout_Protected />}>
    <Route path="admin/users" element={<Page_UsersManagement />} />
  </Route>
</Route>
```

#### Types Convention
- Backend DTOs: `Dto_RegisterResponse`, `Dto_UserListItem`
- Database entities: `Db_ApplicationUser`, `Db_ApplicationRole`
- Match backend casing in types, convert to camelCase in runtime usage
- Use TypeScript interfaces for all data shapes

---

## Testing Architecture (Playwright)

### Folder Structure Pattern
```
[project].tester/
├── tests/                    # Test suites organized by feature
│   ├── auth/
│   │   └── login.spec.ts
│   ├── user/
│   │   ├── user-crud.spec.ts
│   │   └── user-permissions.spec.ts
│   ├── pre-event/
│   │   ├── pre-event-creation.spec.ts
│   │   └── pre-event-publish.spec.ts
│   └── event/
│       └── event-gameplay.spec.ts
├── helpers/                  # Test utilities
│   ├── test-data-helper.ts  # Random data generators
│   ├── screenshot-helper.ts # Screenshot utilities
│   └── auth-helper.ts       # Reusable auth flows
├── assets/                   # Test files (images, videos)
│   ├── image-test.jpg
│   └── video-test.mp4
├── screenshots/              # Auto-generated screenshots
│   └── {test-suite}/
│       └── screenshot-{nnn}.png
└── playwright.config.ts
```