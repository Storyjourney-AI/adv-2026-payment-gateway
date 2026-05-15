# [Feature] Developer Documentation & Quick Start Guide

**Labels:** `feature` `frontend` `dx` `documentation`
**Milestone:** task-005
**Priority:** Medium
**Related:** task-001 (Midtrans Snap Integration), task-004 (Status Check & Cancel Endpoints)

---

## Summary

Add an in-app developer documentation experience in two parts: (1) a dedicated `/dashboard/docs` page with a full API reference for the payment gateway, and (2) an embedded Quick Start section on the Application Detail page (`/dashboard/applications/:id`) showing environment-specific API keys and copy-ready code examples so developers can start integrating in minutes.

---

## Background / Context

### Current State

- The payment gateway exposes several HTTP endpoints (`POST /api/snap/token`, `GET /api/snap/status/{orderId}`, `POST /api/snap/cancel/{orderId}`, webhooks) that child applications must integrate with.
- **There is no documentation anywhere in the application.** Developers integrating a new child app must read source code or ask internally to understand how to authenticate, format requests, and handle responses.
- The Application Detail page (`Page_ApplicationDetail.tsx`) shows environments and their API keys (masked), but provides no integration guidance alongside them.
- A `readme.md` exists at the repository root but is minimal and not accessible to non-technical stakeholders using the dashboard.

### What Needs to Change

1. **New route `dashboard/docs`** → `Page_Docs.tsx`: a full API reference page covering all child-app-facing endpoints with request/response schemas, authentication instructions, error codes, and webhook handling.
2. **Quick Start section embedded in `Page_ApplicationDetail.tsx`**: contextual code snippets that automatically use the selected environment's API key, so a developer can copy a working `curl` or JavaScript example directly.
3. **Sidebar navigation entry** for "Docs" in `Layout_Dashboard.tsx`.

---

## User Stories

**US-1 — API reference:**
> As a developer integrating my app with the payment gateway, I want a single page that documents every endpoint so that I don't need to read source code or ask for help to start building.

**US-2 — Contextual quick start:**
> As a developer on the Application Detail page, I want to see a Quick Start section with copy-ready code that already has my environment's API key filled in so that I can start making real requests immediately.

**US-3 — Webhook guidance:**
> As a developer, I want clear documentation on how to set up my webhook URL and what payload format to expect so that I can reliably update my order status on payment completion.

---

## Acceptance Criteria

### Page_Docs.tsx — Full API Reference

- [ ] Route: `dashboard/docs`
- [ ] Accessible from a "Docs" entry in the `Layout_Dashboard.tsx` sidebar (icon: `BookOpen`, Lucide).

**Authentication Section**
- [ ] Explains that all child-app endpoints require an `X-Api-Key` HTTP header.
- [ ] Shows where to find the API key (Application Detail page → environment card).
- [ ] Shows a copy-ready example header: `X-Api-Key: your_api_key_here`

**Standard Response Envelope Section**
- [ ] Documents the `DataWrapper<T>` response shape:
  ```json
  {
    "success": true,
    "message": "...",
    "data": { ... },
    "errors": null
  }
  ```
- [ ] Explains `success`, `message`, `data`, and `errors` fields.

**Endpoint Reference Section** — one subsection per endpoint:

| Endpoint | Method | Description |
|---|---|---|
| `/api/snap/token` | POST | Create a Snap payment token |
| `/api/snap/status/{orderId}` | GET | Check payment status |
| `/api/snap/cancel/{orderId}` | POST | Cancel a pending payment |

For each endpoint, document:
- [ ] HTTP method + path
- [ ] Required headers (`X-Api-Key`)
- [ ] Request body schema (for POST endpoints) with field names, types, required/optional, and constraints
- [ ] Success response schema with all fields described
- [ ] Relevant HTTP error codes and their meaning (`400`, `401`, `404`, `409`, `422`, `502`)
- [ ] A copy-ready `curl` example

**Webhook Section**
- [ ] Explains that Midtrans notifies the gateway, which stores the status and forwards the raw notification to the `WebhookUrl` configured on the environment.
- [ ] Shows the Midtrans notification payload shape (key fields: `order_id`, `transaction_status`, `fraud_status`, `gross_amount`, `transaction_id`).
- [ ] Notes that the `order_id` in the forwarded payload is the Midtrans-prefixed ID (`{envId[0..8]}_{callerOrderId}`), not the raw caller order ID.
- [ ] Advises that the child app's webhook endpoint must return `2xx` to acknowledge receipt.
- [ ] Notes the SSRF guard: the `WebhookUrl` must be a valid HTTPS URL (non-loopback, non-private IP).

**Order ID Rules Section**
- [ ] Documents that `orderId` must be unique per environment — reusing an order ID for the same API key returns `409 Conflict`.
- [ ] Documents the max length constraint (42 characters).

- [ ] Page uses consistent heading hierarchy, code blocks with syntax highlighting (e.g. via `<pre><code>` or a Shiki/highlight component), and a copy-to-clipboard button on each code block.
- [ ] Page is read-only — no forms or interactive mutations.

### Quick Start Section — Page_ApplicationDetail.tsx

- [ ] A new collapsible or always-visible "Quick Start" section is added below the environments table on `Page_ApplicationDetail.tsx`.
- [ ] The section contains an environment selector (a tab or dropdown) that defaults to the first environment in the list.
- [ ] When an environment is selected, the code examples update to use that environment's **unmasked API key** (the full key, already available in the `Dto_EnvironmentResponse` from the existing API call).
- [ ] Shows at minimum two copy-ready snippets:
  1. **Create a payment token** (`POST /api/snap/token`) — `curl` or JavaScript `fetch`
  2. **Check payment status** (`GET /api/snap/status/{orderId}`) — `curl` or JavaScript `fetch`
- [ ] Each snippet has a copy-to-clipboard button.
- [ ] A "View full API docs →" link navigates to `/dashboard/docs`.
- [ ] Sandbox environments display a `Sandbox` badge; production environments are clearly labeled.

### Sidebar Navigation

- [ ] Add a "Docs" `SidebarMenuItem` to `Layout_Dashboard.tsx`:
  - Icon: `BookOpen` (Lucide)
  - Label: `Docs`
  - URL: `/dashboard/docs`
- [ ] Add route in `routes.ts`:
  ```ts
  route("dashboard/docs", "routes/dashboard/Page_Docs.tsx"),
  ```
  inside the existing `Layout_Protected > Layout_Dashboard` layout block.

---

## Technical Notes

### No New Backend Work

This task is entirely frontend. All data needed (API keys, environment details, gateway base URL) is already available from existing API calls in the respective pages.

### Gateway Base URL

The gateway's public base URL (e.g. `https://yourdomain.com`) should be configurable or derived from `window.location.origin` for code examples. Hardcoding `https://your-gateway.example.com` as a placeholder is acceptable if dynamic base URL resolution is out of scope.

### Code Snippet Approach

Each snippet should be a static string rendered in a `<pre><code>` block. Template literals with the environment's API key interpolated are sufficient. A full syntax highlighting library is a nice-to-have, not required.

### Unmasked API Key on Detail Page

`Page_ApplicationDetail.tsx` already receives the full `ApiKey` string from the server (`Dto_EnvironmentResponse.apiKey`). The masking (`maskApiKey`) is only applied in the displayed table cell. Use the unmasked value for Quick Start snippets.

---

## Out of Scope

- SDK generation (e.g. OpenAPI/Swagger spec export)
- Multi-language snippet tabs (e.g. PHP, Python) — curl + JavaScript is sufficient
- Interactive API explorer (try-it-out UI)
- Search / full-text indexing of docs content
- Versioning of the API docs

---

## Definition of Done

- [ ] `Page_Docs.tsx` created at `routes/dashboard/Page_Docs.tsx` with all documented sections
- [ ] Route `dashboard/docs` added to `routes.ts`
- [ ] "Docs" sidebar entry added to `Layout_Dashboard.tsx`
- [ ] Quick Start section added to `Page_ApplicationDetail.tsx` with environment selector and at least two code snippets
- [ ] All code snippets have copy-to-clipboard functionality
- [ ] "View full API docs →" link present in the Quick Start section
- [ ] `npx tsc --noEmit` passes on all changed files
- [ ] Manual review: page renders correctly, no layout regressions in the dashboard
