# Task Completion Summary — task-002.prd002

## Overall Impact

Environments now carry a built-in `IsSandbox` flag that tells the gateway exactly which Midtrans tier (Sandbox or Production) to use. A single new API endpoint handles all token requests — callers no longer need to pick the right URL; the gateway picks it for them. The dashboard now shows a clear Sandbox/Production badge on each environment card so admins can tell at a glance which tier is live.

---

### Task 1 — `IsSandbox` added to the Environment database model

**Change:** Added an `IsSandbox` boolean field to the `Db_Environment` entity (defaults to `true`) and exposed it in the `Dto_EnvironmentResponse` DTO used by all API responses.

**Impact:** The database now enforces which Midtrans tier an environment connects to, replacing the previous implicit coupling to endpoint URL paths.

---

### Task 2 — Auto-created environments seeded with correct flags

**Change:** When an application is created, the auto-generated `staging` environment gets `IsSandbox = true` and the auto-generated `production` environment gets `IsSandbox = false`.

**Impact:** Applications created from the dashboard are wired to the correct Midtrans tier from day one, with no extra admin configuration needed.

---

### Task 3 — Admin-created environments are always Sandbox

**Change:** Any additional environment created by an admin through the API always gets `IsSandbox = true`, regardless of the name chosen. This is enforced server-side and is not controllable via the form.

**Impact:** Eliminates the risk of accidentally routing test traffic through the live Midtrans Production account.

---

### Task 4 — New unified token endpoint `POST /api/snap/token`

**Change:** Added a single new endpoint that looks up the environment by API key and automatically routes the token request to Midtrans Sandbox or Production based on the environment's `IsSandbox` flag.

**Impact:** Child apps no longer need to know which Midtrans environment they're hitting — they call one endpoint and the gateway handles the rest.

---

### Task 5 — Old endpoints kept but guarded

**Change:** The old `/api/snap/sandbox/token` and `/api/snap/production/token` endpoints remain functional for backward compatibility but now validate that the API key matches the expected environment type. A mismatch returns a clear `400 Bad Request` message directing callers to the new unified endpoint.

**Impact:** Existing integrations keep working. Misconfigured calls (e.g. a production key calling the sandbox endpoint) are caught and reported clearly instead of silently doing the wrong thing.

---

### Task 6 — Database migration instructions documented

**Change:** Migration steps written to `Migrations/migrations.md`. The migration adds the `IsSandbox` column with a default of `true` and backfills existing `production` rows to `false`.

**Impact:** Developers can apply the schema change with a clear, step-by-step guide including the corrected schema name (`payment`, not `app` as stated in the original PRD).

---

### Task 7 — Sandbox/Production badge on the dashboard

**Change:** Each environment card on the Application Detail page now displays a colour-coded badge: grey "Sandbox" or red "Production". A new badge UI component was added to the component library.

**Impact:** Admins can immediately tell at a glance whether an environment connects to real Midtrans payments or test mode, reducing the risk of accidental live charges during development.
