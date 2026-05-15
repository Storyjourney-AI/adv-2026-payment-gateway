# User Flow — Developer Documentation & Quick Start Guide

## Use Case
**In-App Developer Documentation & Contextual Quick Start**

Developers integrating their child applications with the payment gateway need clear, accessible API documentation and copy-ready code examples. The documentation is available from a dedicated page (`/dashboard/docs`), and a Quick Start section on the Application Detail page provides environment-specific code snippets with the actual API key pre-filled.

---

## User Levels (Action × Role)

| Action / Capability | Authenticated User (any role) |
|--------------------|:-----------------------------:|
| View API Documentation page (`/dashboard/docs`) | ✅ |
| Navigate to Docs from sidebar | ✅ |
| Copy code snippets from Docs page | ✅ |
| View Quick Start on Application Detail page | ✅ |
| Switch environment in Quick Start selector | ✅ |
| Copy environment-specific code snippets | ✅ |
| Navigate from Quick Start to full Docs | ✅ |

All actions are read-only. No forms or mutations are involved.

---

## User Flows

### Flow #1 – Browse Full API Documentation
**As any authenticated user**

1. Log in to the dashboard
2. Click **"Docs"** in the sidebar (BookOpen icon)
3. System navigates to `/dashboard/docs`
4. See full API reference with sections:
   - Authentication (X-Api-Key header explanation)
   - Standard Response Envelope (DataWrapper shape)
   - Endpoint Reference (POST /api/snap/token, GET /api/snap/status/{orderId}, POST /api/snap/cancel/{orderId})
   - Webhook Handling
   - Order ID Rules
5. Click copy button on any code block to copy to clipboard
6. Use copied code to integrate their application

---

### Flow #2 – Use Quick Start from Application Detail
**As any authenticated user**

1. Navigate to `/dashboard/applications/:id`
2. View the application details and environments list
3. Scroll down to the **Quick Start** section (below environments)
4. See environment selector (tab/dropdown) defaulting to first environment
5. Switch between environments — code snippets update with the selected environment's **unmasked** API key
6. Sandbox environments show a `Sandbox` badge; production environments show `Production` label
7. Copy the "Create a payment token" snippet (curl + JavaScript fetch)
8. Copy the "Check payment status" snippet (curl + JavaScript fetch)
9. Click **"View full API docs →"** link to navigate to `/dashboard/docs`

---

## Key Rules / Constraints

- All documentation is read-only — no forms, no interactive mutations
- Quick Start snippets use the unmasked API key from `Dto_EnvironmentResponse.apiKey` (already available)
- Gateway base URL is derived from `window.location.origin`
- Code blocks include copy-to-clipboard functionality
- No new backend endpoints needed — this is entirely frontend work
- The Docs page uses consistent heading hierarchy and code blocks
- Quick Start section only appears when environments exist

---

## Page Mapping

| Page | Status | Changes |
|------|--------|---------|
| `Page_Docs.tsx` (`/dashboard/docs`) | **NEW** | Full API reference page |
| `Page_ApplicationDetail.tsx` (`/dashboard/applications/:id`) | **EXISTING** | Add Quick Start section below environments |
| `Layout_Dashboard.tsx` | **EXISTING** | Add "Docs" sidebar menu item |
| `routes.ts` | **EXISTING** | Add `dashboard/docs` route |

---

**End of Userflow**
