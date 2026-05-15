# Target Market

## Primary Persona — Internal Developer (80%)

**Role:** Backend or fullstack developer on an Advine product team.

**Goals:**
- Integrate payment capabilities into their product quickly without building provider-specific logic.
- Receive reliable webhook delivery with built-in retries so they don't need to handle flaky provider callbacks.
- Access clear API documentation and predictable response contracts.

**Pain Points:**
- Duplicating payment provider integrations across multiple products is expensive and error-prone.
- Handling signature verification, retries, and webhook ordering edge cases takes time away from core product work.
- Provider SDKs and contracts change; maintaining them per-product is a maintenance burden.

**How they use the product:**
- Register their application in the gateway (endpoint URL, credentials, provider config).
- Call the gateway API to create payment links.
- Receive forwarded webhook events at their registered endpoint.
- Query the gateway API for transaction history and delivery status during debugging.

---

## Secondary Persona — Ops / Finance Team (20%)

**Role:** Operations staff or finance analyst at Advine.

**Goals:**
- Monitor payment activity across all products from a single interface.
- Pull purchase reports for reconciliation, bookkeeping, or management reporting.
- Trigger or review invoice generation for clients or learners.

**Pain Points:**
- Payment data is siloed across multiple product dashboards.
- No centralized view of what was charged, to whom, and whether delivery succeeded.
- Invoice creation is manual or ad hoc today.

**How they use the product:**
- Access the dashboard to view transaction logs, filter by product/date/status.
- Export reports for reconciliation.
- (Planned) Generate and send invoices directly from the gateway.

---

## Market Positioning
This is not a product sold externally. It is an internal platform service owned by Advine Engineering. Its value is measured in developer-hours saved across product teams and in the reliability and auditability of payment operations across the Advine portfolio.

Compared to each product doing its own provider integration:
- The gateway reduces duplication, standardizes contracts, and centralizes compliance.
- The ops/finance team gains a single source of truth instead of hunting across product-specific dashboards.

---

## Non-targets
- **External clients or partners** — this system is internal to Advine. External parties interact with Advine products, not with this gateway directly.
- **Business development or sales teams** — no use case here; they operate outside the payment flow.
- **End consumers / learners** — they interact with Advine products; the gateway is invisible to them.
- **Third-party SaaS customers** — this is not a product offered to the market; it serves Advine products only.
