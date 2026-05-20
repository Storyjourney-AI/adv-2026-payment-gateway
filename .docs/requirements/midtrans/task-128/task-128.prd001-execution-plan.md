# Execution Plan — ADV-128 Delayed Midtrans Webhook Acknowledgment

## Checklist
- [x] Backend — Acknowledge Valid Delayed Webhooks
- [x] Tests — Cover Delayed Ack and Guardrails
- [x] Validation — Run Focused Webhook Tests and Backend Build

---

## Backend — Acknowledge Valid Delayed Webhooks

* Target File: EXISTING `PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs`
  - Adjust the replay-window failure branch so a signature-valid Midtrans notification with `transaction_time` older than the configured replay window is acknowledged with `200 OK` instead of `400 BadRequest`.
  - Keep other replay validation failures unchanged so malformed or future-skewed payloads still reject.
  - Feasibility: HIGH — localized change in the current replay decision point.

---

## Tests — Cover Delayed Ack and Guardrails

* Target File: EXISTING `PaymentGateway.Server.Tests/Midtrans/WebhookControllerTests.cs`
  - Add a regression test for an old but signature-valid sandbox settlement notification returning `200 OK`.
  - Add focused assertions that invalid signatures still return `400 BadRequest` and duplicate suppression still acknowledges without reprocessing.
  - Feasibility: HIGH — existing controller test fixture already covers signature generation and request setup.

---

## Validation — Run Focused Webhook Tests and Backend Build

* Run focused Midtrans webhook tests first to verify the changed behavior.
* Run `dotnet build` for `PaymentGateway.Server/` after the focused tests pass.
* Feasibility: HIGH — standard validation flow, no migration required.