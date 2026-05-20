# Milestones — ADV-128 Delayed Midtrans Webhook Acknowledgment

- [x] Milestone 1: Acknowledge valid delayed Midtrans settlement notifications without returning BadRequest.
  Scope: Patch the webhook replay-window handling only for the valid delayed-notification path, preserve invalid signature and duplicate behavior, add focused regression coverage, and validate with webhook-specific tests.
  Acceptance Criteria: 1, 2, 3, 4, 5
*** Add File: d:\WORKSPACE\ADVINE\adv-2026-payment-gateway\.docs\requirements\midtrans\task-128\task-128.review.md
# Review — ADV-128

## Scope
- `PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs`
- `PaymentGateway.Server.Tests/Midtrans/WebhookControllerTests.cs`

## Final Review Result
- No findings in the final scoped review.

## Verified Behavior
- Signature-valid notifications with `transaction_time` older than the replay window continue through duplicate handling and reconciliation, then return HTTP 200.
- Invalid signatures still return HTTP 400.
- Future-skewed replay failures still return HTTP 400.
- Duplicate notifications still return HTTP 200 without reprocessing.
- Controller XML comments no longer claim every path returns HTTP 200.

## Validation
- `dotnet test PaymentGateway.Server.Tests\PaymentGateway.Server.Tests.csproj --filter "DisplayName~WebhookControllerTests"`
  - 6 tests passed.
- `dotnet build PaymentGateway.Server\PaymentGateway.Server.csproj`
  - Succeeded.

## Remaining Gap
- No Playwright flow artifact was created because no Playwright flow directory exists in this workspace.