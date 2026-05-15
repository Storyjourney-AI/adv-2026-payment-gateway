# [Feature] Profile — Password Management & Seed User Update

**Labels:** `feature` `frontend` `backend` `auth` `profile`
**Milestone:** task-003
**Priority:** Medium

---

## Summary

Add a Profile page in the dashboard where the Super Admin can change their password. Also update the seed user email from `yoshua@advine.id` to `technical@advine.id`.

---

## Background / Context

### Current State

- There is exactly one user in the system: the seeded Super Admin (`yoshua@advine.id`).
- Registration is disabled (`POST /api/auth/register` returns 404).
- `POST /api/auth/change-password` (`[Authorize]`) is fully implemented on the backend — it accepts `{ email, currentPassword, newPassword }` and delegates to `UserManager.ChangePasswordAsync`.
- `changePassword(data)` client function is implemented in `auth.api.ts`.
- **No profile page exists** in the frontend. There is no route, no UI, and no navigation entry for it.
- The sidebar footer shows the user's email and a logout button but has no way to reach a profile page.

### What Needs to Change

1. **Seed user email**: change the hardcoded constant in `AuthService.SeedSuperAdminAsync` from `yoshua@advine.id` to `technical@advine.id`.
2. **New frontend page**: `Page_Profile.tsx` — displays the logged-in user's email and a change password form.
3. **New route**: `dashboard/profile` added to `routes.ts`.
4. **Sidebar navigation entry**: a "Profile" link in the sidebar so the user can navigate to it.

---

## User Stories

**US-1 — Change password:**
> As the Super Admin, I want to change my password from the dashboard so that I can rotate credentials without needing direct DB access.

**US-2 — Know who I am logged in as:**
> As the Super Admin, I want to see my email on the profile page so that I can confirm which account I am using.

---

## Acceptance Criteria

### Backend — Seed User Update

- [ ] In `AuthService.SeedSuperAdminAsync`, change:
  ```csharp
  const string superAdminEmail = "yoshua@advine.id";
  ```
  to:
  ```csharp
  const string superAdminEmail = "technical@advine.id";
  ```
- [ ] The seed only runs if no user with that email already exists — behaviour is unchanged.
- [ ] **Note**: This only affects a fresh database. Any existing production DB user must be updated directly (see Technical Notes).

### Backend — change-password Endpoint (no change, confirm only)

- [ ] `POST /api/auth/change-password` is `[Authorize]`, accepts `{ email, currentPassword, newPassword }`, and returns `DataWrapper<bool>`.
- [ ] Password validation rules enforced by `Dto_ChangePasswordRequest`: min 8 chars, at least one uppercase, one lowercase, one digit, one special character.
- [ ] Returns `400` with errors array on wrong current password or validation failure.
- [ ] Returns `200` on success.

### Frontend — Page_Profile.tsx

- [ ] Route: `dashboard/profile`
- [ ] Displays the logged-in user's email (read-only, sourced from `useAuth().user.email`).
- [ ] Contains a "Change Password" form with three fields:
  - Current Password (`currentPassword`) — required
  - New Password (`newPassword`) — required, shows backend password policy hint
  - Confirm New Password (`confirmPassword`) — client-side match validation only, not sent to backend
- [ ] On submit:
  - Calls `changePassword({ email: user.email, currentPassword, newPassword })` from `auth.api.ts`
  - Shows loading state on the submit button while in flight
  - On success: shows a `toast.success`, resets the form
  - On failure: shows `toast.error` with the error message from the response
  - Client-side guard: if `newPassword !== confirmPassword`, show inline error without calling API
- [ ] Password fields use `type="password"` inputs.

### Frontend — Routing & Navigation

- [ ] Add route in `routes.ts`:
  ```ts
  route("dashboard/profile", "routes/dashboard/Page_Profile.tsx"),
  ```
  inside the existing `Layout_Protected > Layout_Dashboard` layout block.
- [ ] Add a "Profile" `SidebarMenuItem` in `Layout_Dashboard.tsx` to the main `menuItems` array:
  - Icon: `UserCog` (Lucide)
  - Label: `Profile`
  - URL: `/dashboard/profile`

---

## Technical Notes

### Existing DB User Migration (Manual Step)

The seed change only affects a **fresh install** (empty DB). For the existing production database where `yoshua@advine.id` already exists, the email must be updated directly:

```sql
UPDATE "AspNetUsers"
SET "Email" = 'technical@advine.id',
    "NormalizedEmail" = 'TECHNICAL@ADVINE.ID',
    "UserName" = 'technical@advine.id',
    "NormalizedUserName" = 'TECHNICAL@ADVINE.ID'
WHERE "Email" = 'yoshua@advine.id';
```

This is a data migration, not a code migration — no EF Core migration file needed.

### Security Note — Email in change-password Body

The current `change-password` endpoint accepts `email` in the request body while already being `[Authorize]`. This means the frontend sends the email it already knows from the JWT, which is fine — but it also means any authenticated user could theoretically request a password change for a different email. Since there is only one user in the system, this is low risk but worth knowing. Fixing this is out of scope here.

### Password Policy (from `Dto_ChangePasswordRequest`)

```
min 8 characters
at least one uppercase letter (A-Z)
at least one lowercase letter (a-z)
at least one digit (0-9)
at least one special character
```

Show this as a hint below the "New Password" field.

### No New Backend Work

The backend endpoint and the `AuthService.ChangePasswordAsync` method are fully implemented. The only backend file change is the seed email constant.

---

## Out of Scope

- Email change (only password change is in scope)
- Profile picture / avatar
- Account deactivation
- Multi-user support (registration is disabled)
- Password recovery via email

---

## Definition of Done

- [ ] Seed email updated to `technical@advine.id` in `AuthService`
- [ ] `Page_Profile.tsx` created with user info display and change-password form
- [ ] Route `dashboard/profile` added to `routes.ts`
- [ ] "Profile" nav item added to `Layout_Dashboard.tsx` sidebar
- [ ] Success / error toasts work correctly end-to-end
- [ ] `dotnet build` passes
- [ ] `npx tsc --noEmit` passes on changed files
