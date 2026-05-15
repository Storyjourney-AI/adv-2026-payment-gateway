# Execution Plan — Developer Documentation & Quick Start Guide

## Checklist
- [x] Routing & Navigation Setup
- [x] Page_Docs.tsx — Full API Reference
- [x] Quick Start Section — Page_ApplicationDetail.tsx
- [x] Build Validation

---

## Routing & Navigation Setup

* Target File: EXISTING `paymentgateway.client/app/routes.ts`
    - Add route `dashboard/docs` pointing to `routes/dashboard/Page_Docs.tsx`
    - Place inside the existing `Layout_Protected > Layout_Dashboard` layout block
    - Feasibility: ✅ Straightforward — one line addition

* Target File: EXISTING `paymentgateway.client/app/components/Layout_Dashboard.tsx`
    - Import `BookOpen` from `lucide-react`
    - Add "Docs" entry to `menuItems` array with icon `BookOpen` and URL `/dashboard/docs`
    - Feasibility: ✅ Straightforward — follows existing pattern

---

## Page_Docs.tsx — Full API Reference

* Target File: NEW `paymentgateway.client/app/routes/dashboard/Page_Docs.tsx`
    - Create a read-only documentation page at route `dashboard/docs`
    - **Authentication Section**: Explain `X-Api-Key` header, where to find the key, copy-ready header example
    - **Standard Response Envelope Section**: Document `DataWrapper<T>` shape with field explanations
    - **Endpoint Reference Section**: Document three endpoints:
      1. `POST /api/snap/token` — Create a Snap payment token
      2. `GET /api/snap/status/{orderId}` — Check payment status
      3. `POST /api/snap/cancel/{orderId}` — Cancel a pending payment
    - For each endpoint: method + path, required headers, request body schema (POST), success response schema, error codes, copy-ready curl example
    - **Webhook Section**: Midtrans notification flow, payload shape, order ID prefixing, 2xx acknowledgement, SSRF guard notes
    - **Order ID Rules Section**: Uniqueness constraint (409 Conflict), max length (42 chars)
    - Copy-to-clipboard button on each code block
    - Consistent heading hierarchy with `<pre><code>` blocks
    - Feasibility: ✅ Pure frontend — no backend dependencies, uses existing UI components (Badge, Button, etc.)

---

## Quick Start Section — Page_ApplicationDetail.tsx

* Target File: EXISTING `paymentgateway.client/app/routes/dashboard/Page_ApplicationDetail.tsx`
    - Add a "Quick Start" section below the environments list
    - Environment selector (tabs) defaulting to first environment
    - Code snippets update with selected environment's unmasked API key
    - Two copy-ready snippets per environment:
      1. **Create a payment token** (`POST /api/snap/token`) — curl + JavaScript fetch
      2. **Check payment status** (`GET /api/snap/status/{orderId}`) — curl + JavaScript fetch
    - Each snippet has a copy-to-clipboard button
    - "View full API docs →" link navigating to `/dashboard/docs`
    - Sandbox badge / Production label per environment
    - Only show when environments exist
    - Feasibility: ✅ All data already available from existing API calls (Dto_EnvironmentResponse has apiKey)

---

## Build Validation

* Run `npx tsc --noEmit` in `paymentgateway.client/` to verify all files compile
* Feasibility: ✅ Standard validation step
