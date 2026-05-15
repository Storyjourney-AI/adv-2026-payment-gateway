## Checklist
- [x] Solution File Rename
- [x] Frontend Package Names
- [x] Frontend Page Titles (RabbitHole → Payment Gateway)
- [x] readme.md Display Name
- [x] appsettings JWT Audience/Issuer Labels (optional/cosmetic)

---

## Context

The goal is to rename all user-facing names, package identifiers, solution names, and display titles from legacy names (`RabbitHole`, `rabbithole.client`, `Advine Payment Gateway`, `adv-payment-gateway`) to a clean, brand-neutral **Payment Gateway**. No structural folder renames, no namespace changes, no migration changes — only textual/config identifiers are in scope.

---

## Task 1: Solution File Rename

* Target File: EXISTING `adv-payment-gateway.sln`
    - Rename the physical file from `adv-payment-gateway.sln` → `payment-gateway.sln`
    - The inner content (project references, GUIDs) is already correct (`PaymentGateway.Server`) — no content edits needed.
    - **Feasibility: HIGH** — File rename only; .sln content already uses correct project names.

---

## Task 2: Frontend `package.json` Name Fix

* Target File: EXISTING `paymentgateway.client/package.json`
    - Change `"name": "rabbithole.client"` → `"name": "paymentgateway.client"`
    - **Feasibility: HIGH** — Single string replacement; no downstream dependency on this name at runtime.

* Target File: EXISTING `paymentgateway.client/package-lock.json`
    - Change `"name": "rabbithole.client"` → `"name": "paymentgateway.client"` (appears at lines 2 and 7)
    - **Feasibility: HIGH** — Cosmetic lockfile metadata; safe to update.

---

## Task 3: Frontend Route Page Titles

* Target File: EXISTING `paymentgateway.client/app/routes/Page_Home.tsx`
    - Change `"RabbitHole - Home"` → `"Payment Gateway - Home"`
    - Change `"Welcome to RabbitHole!"` → `"Payment Gateway"`
    - Change `Welcome to RabbitHole` (heading text) → `Welcome to Payment Gateway`
    - **Feasibility: HIGH** — Simple string substitution in metadata/JSX.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_AdminPanel.tsx`
    - Change `"Admin Panel - RabbitHole"` → `"Admin Panel - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_Dashboard.tsx`
    - Change `"Dashboard - RabbitHole"` → `"Dashboard - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_Agents.tsx`
    - Change `"Agents - RabbitHole"` → `"Agents - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_Worksheets.tsx`
    - Change `"Worksheets - RabbitHole"` → `"Worksheets - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_Bookmarks.tsx`
    - Change `"Bookmarks - RabbitHole"` → `"Bookmarks - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_TopicNew.tsx`
    - Change `"New Topic - RabbitHole"` → `"New Topic - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_ChatTest.tsx`
    - Change `"Chat Test - RabbitHole"` → `"Chat Test - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_Topics.tsx`
    - Change `"Topics - RabbitHole"` → `"Topics - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_TopicOverview.tsx`
    - Change `"Topic Overview - RabbitHole"` → `"Topic Overview - Payment Gateway"`
    - **Feasibility: HIGH** — Single title string.

---

## Task 4: readme.md Display Name

* Target File: EXISTING `readme.md`
    - Change `Advine Payment Gateway` → `Payment Gateway` (in title/opening description)
    - Remove or genericize references to "Advine" branding
    - **Feasibility: HIGH** — Cosmetic documentation change only.

---

## Task 5: Root package.json Description

* Target File: EXISTING `package.json` (root)
    - Current: `"description": "Root management for Payment Gateway"` — already correct, no change needed.
    - `"name": "paymentgateway-root"` — already correct, no change needed.
    - **Feasibility: N/A** — Already uses correct name.

---

## Out of Scope (No Changes Required)

| Item | Reason |
|---|---|
| `PaymentGateway.Server/` folder name | Already correct |
| `PaymentGateway.Server.csproj` | Already correct |
| `paymentgateway.client/` folder name | Already correct |
| C# namespaces (`PaymentGateway.Server.*`) | Already correct |
| `appsettings.development.json` JWT Issuer/Audience | Already `PaymentGateway.Server` / `PaymentGateway.Client` — correct |
| `Dockerfile` | Already uses `paymentgateway.client` and `PaymentGateway.Server` — correct |
| `vite.config.ts` | No app name reference — no change needed |
| `.docs/rules/infrastructure-rules.md` | Internal dev doc; RabbitHole references are template boilerplate — low priority |
| Database name in connection string | `db_paymentgateway` — already correct |

---

## Infrastructure Rules Compliance

| Rule | Status |
|---|---|
| Folder structure (`PaymentGateway.Server/`, `paymentgateway.client/`) | Already correct — no folder renames needed |
| Page naming convention (`Page_*`) | Not affected — no page file renames |
| Backend namespaces (`PaymentGateway.Server.*`) | Already correct — no changes needed |
| CORS port config (5400 / 5450) | Not affected |
| `vite.config.ts` `@services` alias and port | Not affected |
| `react-router.config.ts` SSR setting | Not affected |
| `.docs/rules/infrastructure-rules.md` internal template names | Out of scope — doc references `rabbithole.client` as boilerplate template only |

All tasks in this plan are purely cosmetic/metadata — no structural, namespace, or database changes are involved. Plan is fully compliant with infrastructure rules.

---

## Execution Order

1. Rename `adv-payment-gateway.sln` → `payment-gateway.sln` (Task 1)
2. Update `paymentgateway.client/package.json` name (Task 2)
3. Update `paymentgateway.client/package-lock.json` name (Task 2)
4. Batch-update all 10 page title strings in TSX files (Task 3)
5. Update `readme.md` Advine references (Task 4)
