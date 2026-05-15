# About Advine Payment Gateway

## Vision
A world where every Advine product handles payments without duplicating infrastructure or maintaining separate provider integrations.

## Problem
As Advine grows its portfolio of edtech products and SaaS offerings, each product would otherwise need to independently integrate with payment providers (e.g. Midtrans, Xendit), handle webhook verification, manage retries, and maintain audit logs. This creates fragmented, hard-to-maintain payment code scattered across products.

## Solution
Advine Payment Gateway is a centralized internal payment service that acts as the single integration point between all Advine products and external payment providers. Products register with the gateway and delegate payment link creation, webhook reception, and forwarding to it — keeping their own codebases free of payment provider complexity.

## Core Features
- **Payment Link Management:** Accept requests from internal apps and generate normalized payment links via registered provider adapters.
- **Webhook Receiver & Forwarder:** Receive provider webhooks (e.g. payment success, refund), verify signatures, and forward to the correct registered app endpoint with retry and exponential backoff.
- **App Routing Registry:** Maintain a registry of Advine products, their webhook endpoints, and their provider credentials.
- **Purchase Reporting:** Provide ops and finance teams with centralized transaction logs, delivery status, and inbound event history across all products.
- **Invoice Generation (Planned):** Generate and dispatch invoices on behalf of registered products.
- **Security Controls:** HMAC signature verification, TLS-only endpoints, optional IP allowlists, and masked storage of sensitive data.
- **Audit Logging:** Immutable logs of all inbound provider events and outbound forwarding attempts with full status tracking.

## Elevator Pitch
Advine Payment Gateway is an internal platform service that consolidates payment processing for all Advine products under a single, secure integration point. Instead of each product managing its own payment provider connections, webhook handling, and retry logic, teams simply register with the gateway and let it handle provider communication, routing, reporting, and compliance — freeing product teams to focus on their core domain.
