**Project Overview**

Payment Gateway is a lightweight, centralized payment gateway service. It provides a single integration point for all products and SaaS offerings, forwarding payment links and webhook events from external payment providers to the appropriate internal application.

**Purpose**

- **Single Integration:** Consolidate payment processing for multiple products under one gateway.
- **Routing & Forwarding:** Route payment links and forward provider webhooks to the correct application endpoints.
- **Decoupling:** Keep product codebases independent from payment provider details.

**Primary Capabilities**

- **Payment Link Management:** Accept payment link creation requests and normalize links for downstream apps.
- **Webhook Receiver & Forwarder:** Receive webhooks (e.g., payment success, refund) from payment providers and forward them to registered app endpoints with retry and backoff.
- **App Routing Registry:** Maintain a mapping of products/apps to their webhook endpoints and credentials.
- **Logging & Auditing:** Centralized logs of inbound provider events and outbound forwarding attempts.
- **Security Controls:** Support HMAC signature verification, TLS-only endpoints, and optional IP allowlists.

**Architecture (high level)**

- Ingress: Provider webhooks and API calls -> Gateway API
- Processing: Validate signatures, map to app, transform payloads
- Egress: Forward to app endpoint, persist delivery status, retry on failure

**Integration Points**

- Payment Providers: Xendit (examples in `Xendit/`), others via adapter interfaces
- Internal Apps: Registered webhook URLs per product (managed in gateway config)

**Operational Notes**

- Configuration: Per-environment settings live in `appsettings.json` / `appsettings.Development.json`.
- Retries: Exponential backoff with configurable max attempts.
- Monitoring: Emit metrics for inbound events, forward success/failure rates, and latency.

**Security & Compliance**

- Verify provider signatures before forwarding.
- Mask or avoid storing sensitive payment details; store only metadata and delivery status.
- Use TLS for all external and internal traffic; rotate keys regularly.

**Deployment & Scaling**

- Designed to run as a small service (container or App Service). Scale horizontally behind a load balancer when webhook volume increases.

**Contact & Ownership**

- **Owners:** Payments Platform Team
- **Contact:** (replace with real contact)
