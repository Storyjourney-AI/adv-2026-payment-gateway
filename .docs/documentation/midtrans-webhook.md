# Midtrans Webhook / HTTP(S) Notifications — Reference Documentation

**Source:** https://docs.midtrans.com/docs/https-notification-webhooks  
**Scope:** How Midtrans sends payment status events back to this gateway, and how this gateway forwards them to the correct child app.

---

## Overview

When a customer completes a payment (or a transaction status changes), Midtrans sends an HTTP POST notification to a configured **Payment Notification URL**. This is how the backend learns about payment outcomes without polling.

**This gateway's role in the webhook flow:**

```
Midtrans → POST /api/webhook/midtrans/{env}  (this gateway)
                ↓
          1. Verify signature_key authenticity
          2. Look up the order_id to find which child app owns it
          3. Forward the payload to the child app's WebhookUrl (Db_Environment.WebhookUrl)
```

The child app's `WebhookUrl` is stored in `Db_Environment` and registered when the environment is created/updated.

---

## Configuring the Notification URL on Midtrans Dashboard

Set this in: **Midtrans Dashboard → Settings → Configuration → Payment Notification URL**

- Must be a publicly reachable HTTPS URL
- Cannot be localhost, behind a VPN, behind Basic Auth, or on an unusual port
- For development: use a tunneling tool (ngrok, localhost.run) to expose local endpoints

For sandbox this gateway's notification URL would be something like:
```
https://your-gateway.com/api/webhook/midtrans/sandbox
```
For production:
```
https://your-gateway.com/api/webhook/midtrans/production
```

### Per-Transaction Override (Advanced)

Midtrans supports overriding the notification URL per transaction via request headers during token creation:

| Header | Behaviour |
|---|---|
| `X-Override-Notification` | Replaces the dashboard-configured URL entirely |
| `X-Append-Notification` | Adds to the dashboard URL (both receive the notification) |

Both headers support up to 3 comma-separated URLs. This gateway can use `X-Override-Notification` during Snap token creation to route notifications directly to its own endpoint regardless of dashboard config.

---

## Webhook Payload Structure

Midtrans sends a JSON POST body. The core fields present on all payment methods:

| Field | Type | Notes |
|---|---|---|
| `transaction_id` | string | Midtrans internal transaction ID |
| `order_id` | string | The `order_id` sent during token creation — use this to identify the child app |
| `transaction_status` | string | The primary status — see table below |
| `fraud_status` | string | May be absent for low-risk payment methods |
| `gross_amount` | string | Total amount as a decimal string (e.g. `"10000.00"`) |
| `payment_type` | string | e.g. `"credit_card"`, `"gopay"`, `"bank_transfer"` |
| `status_code` | string | HTTP-like status string (e.g. `"200"`) |
| `status_message` | string | Human-readable status |
| `signature_key` | string | SHA-512 hash for authenticity verification |
| `merchant_id` | string | Midtrans merchant ID |
| `currency` | string | Always `"IDR"` |
| `transaction_time` | string | `"YYYY-MM-DD HH:mm:ss"` (WIB, UTC+7) |

### Sample Payload (Credit Card — Success)

```json
{
  "transaction_time": "2020-01-09 18:27:19",
  "transaction_status": "capture",
  "transaction_id": "57d5293c-e65f-4a29-95e4-5959c3fa335b",
  "status_message": "midtrans payment notification",
  "status_code": "200",
  "signature_key": "16d6f84b2fb0468e2a9cf99a8ac4e5d803d42180347aaa70cb2a7abb13b5c6130458ca9c71956a962c0827637cd3bc7d40b21a8ae9fab12c7c3efe351b18d00a",
  "payment_type": "credit_card",
  "order_id": "ORDER-123456",
  "merchant_id": "G141532850",
  "masked_card": "48111111-1114",
  "gross_amount": "10000.00",
  "fraud_status": "accept",
  "currency": "IDR",
  "bank": "bni",
  "approval_code": "1578569243927"
}
```

---

## Transaction Status Reference

### `transaction_status` Values

| Value | Meaning | Action |
|---|---|---|
| `capture` | ✅ Card payment captured successfully. Will auto-settle. | Treat as success |
| `settlement` | ✅ Fully settled. Funds credited to merchant. | Treat as success |
| `pending` | 🕒 Waiting for customer to complete payment (e-wallet, bank transfer, OTP) | Wait for next notification |
| `deny` | ❌ Rejected by payment provider or Midtrans FDS | Treat as failed |
| `cancel` | ❌ Cancelled (by merchant action) | Treat as failed |
| `expire` | ❌ Payment window expired before customer paid | Treat as failed |
| `failure` | ❌ Unexpected processing error | Treat as failed |
| `refund` | ↩️ Full refund initiated | Update as refunded |
| `partial_refund` | ↩️ Partial refund initiated | Update as partially refunded |
| `authorize` | 🕒 Pre-authorized (advanced card feature only) | Wait for capture or auto-release |

### `fraud_status` Values

| Value | Meaning |
|---|---|
| `accept` | ✅ Safe to proceed |
| `deny` | ❌ Rejected by FDS — typically will NOT reach `capture`/`settlement` |

> **Important:** `fraud_status` is not always present (e.g. Indomaret, Alfamart, and some low-risk methods skip FDS). The absence of `fraud_status` does not indicate fraud.

### Definitive Success Logic

```
transaction is SUCCESS if:
  transaction_status == "settlement"
  OR (transaction_status == "capture" AND (fraud_status == "accept" OR fraud_status is absent))
```

---

## Signature Verification (Authenticity Check)

Every notification includes a `signature_key`. **Always verify it before processing** to prevent spoofed payment notifications.

### Algorithm

```
signature_key = SHA512( order_id + status_code + gross_amount + ServerKey )
```

Concatenate the **string values** (not encoded) of those four fields in that exact order, then SHA-512 hash the result. Compare with the `signature_key` in the payload.

### C# Implementation

```csharp
using System.Security.Cryptography;
using System.Text;

public static bool VerifySignature(
    string orderId,
    string statusCode,
    string grossAmount,
    string serverKey,
    string receivedSignatureKey)
{
    var raw = orderId + statusCode + grossAmount + serverKey;
    var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(raw));
    var computed = Convert.ToHexString(bytes).ToLowerInvariant();
    return computed == receivedSignatureKey;
}
```

> **Security note:** Use the **Server Key for the environment that sent the notification** (sandbox key for sandbox events, production key for production events). Never skip this check — failing to verify is a financial security liability.

---

## Required Response to Midtrans

Midtrans expects an HTTP `200 OK` response to confirm delivery. If the gateway returns any non-200 status, Midtrans will retry the notification.

Return `200 OK` even if the downstream child app's webhook fails — log the failure and handle it separately. Do not let child app errors propagate as non-200 responses to Midtrans.

---

## Handling Best Practices

1. **Verify `signature_key` first** — reject with no processing if invalid.
2. **Respond 200 quickly** — do forwarding to child app asynchronously if needed to avoid timeout.
3. **Idempotency** — Midtrans may send the same notification multiple times. Use `transaction_id` + `transaction_status` to deduplicate.
4. **Use `transaction_status` as truth** — it is the most accurate indicator, not the `status_code` string.
5. **Handle missing `fraud_status`** — not all payment methods return it. Absence is not a failure signal.
6. **No custom headers supported** — Midtrans does not support custom headers on notification requests. The gateway's webhook endpoint must accept plain unauthenticated POST requests (authenticity is verified via `signature_key` instead).
7. **TLS v1.2 required** — Midtrans notification engine currently only supports up to TLS 1.2. Ensure the gateway's endpoint accepts TLS 1.2 connections.

---

## Delayed or Missed Notifications

Midtrans notifications can occasionally be delayed or lost due to network/infra issues. When this happens:

- Use the **GET Status API** (`GET https://api.sandbox.midtrans.com/v2/{order_id}/status`) to poll Midtrans for the current transaction status.
- Trigger a GET Status check when a transaction has been pending for longer than expected.
- Use GET Status as the failover mechanism — do not treat the absence of a webhook as a definitive failure.

---

## Audit Trail

Midtrans keeps a notification history in the dashboard:
**Dashboard → Settings → Configuration → See History**

Searchable by `order_id`. Shows whether each notification was delivered successfully. Useful for debugging missed notifications.

---

## Webhook Flow in This Gateway (Implementation Notes)

```
POST /api/webhook/midtrans/{env}
  ↓
1. Read order_id from payload
2. Verify signature_key using ServerKey for {env}
   → If invalid: return 400 (log the attempt)
3. Look up Db_Environment.WebhookUrl by matching order_id to registered app
   → order_id convention must encode or reference which Db_Environment created it
4. Forward original payload to Db_Environment.WebhookUrl via HTTP POST
   → Log result (success/failure, response code)
5. Return 200 OK to Midtrans regardless of forwarding result
```

> **Note:** The `order_id` used during Snap token creation must be structured to allow this gateway to route the webhook back to the correct child app environment. This needs to be defined during implementation (e.g. prefix with environment `ApiKey` hash, or store a mapping table).
