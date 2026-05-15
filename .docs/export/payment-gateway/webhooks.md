# Webhooks

The gateway forwards verified Midtrans payment notifications to your application's registered `WebhookUrl`. This is the primary mechanism for receiving real-time payment status updates.

---

## How It Works

```
Midtrans → POST /api/midtrans/payment (gateway)
               │
               ├─ Verify HMAC-SHA512 signature
               ├─ Anti-replay check (transaction_time window)
               ├─ Idempotency guard (deduplication)
               ├─ Update transaction status in DB
               │
               └─ POST your WebhookUrl ← raw Midtrans payload forwarded here
```

You do **not** receive webhooks directly from Midtrans. The gateway validates every notification before forwarding it to your server.

---

## Registering Your Webhook URL

Set the `WebhookUrl` field on your environment via the gateway dashboard or API. Requirements:

- Must be **HTTPS**
- Must be **publicly reachable** (not a local/private IP address)
- Must accept `POST` requests with `Content-Type: application/json`
- Responding with a non-2xx status is logged but **does not block** Midtrans acknowledgement

> The gateway attempts delivery up to **2 times** (1 retry). The retry only triggers on **HTTP 5xx** responses or network-level exceptions. A `4xx` response from your server stops delivery immediately with no retry.

---

## Webhook Payload

The payload forwarded to your `WebhookUrl` is the **raw Midtrans notification body**, unchanged. Below is the full structure you can expect:

```json
{
  "transaction_time": "2026-03-31 08:05:00",
  "transaction_status": "settlement",
  "transaction_id": "abc123def456",
  "status_message": "midtrans payment notification",
  "status_code": "200",
  "signature_key": "...",
  "payment_type": "bank_transfer",
  "order_id": "a1b2c3d4_ORDER-2026-0001",
  "merchant_id": "G123456789",
  "gross_amount": "150000.00",
  "fraud_status": "accept",
  "currency": "IDR"
}
```

> **Important:** `order_id` in the Midtrans payload is the **gateway's internal ID** (prefixed). To identify your original order, strip the prefix or match by another field. The easiest approach: use the `transaction_id` or extract the suffix after the first `_`.

### Key Fields

| Field | Type | Description |
|---|---|---|
| `order_id` | `string` | Gateway-prefixed Midtrans order ID. Format: `{envPrefix}_{yourOrderId}`. |
| `transaction_id` | `string` | Midtrans unique transaction identifier. |
| `transaction_status` | `string` | Payment status. See [status values](./check-payment-status.md#status-values). |
| `transaction_time` | `string` | When the transaction event occurred. Format: `YYYY-MM-DD HH:mm:ss` in **WIB (UTC+7)**. |
| `payment_type` | `string` | Payment method used (e.g. `bank_transfer`, `credit_card`, `gopay`). |
| `gross_amount` | `string` | Total transaction amount in IDR. |
| `fraud_status` | `string` | `accept`, `challenge`, or `deny`. Only present for credit card payments. |
| `status_code` | `string` | Midtrans HTTP-style status code (e.g. `"200"` for success). |
| `signature_key` | `string` | HMAC-SHA512 signature (verified by gateway before forwarding). |

---

## Responding to a Webhook

Your endpoint should respond promptly. The gateway does **not** wait on your response to acknowledge Midtrans — forwarding failure is logged and silently discarded.

Keep your handler fast — do any heavy processing asynchronously:

```ts
// Express example
app.post("/webhook/payment", express.json(), (req, res) => {
  // Acknowledge immediately
  res.status(200).send("OK");

  // Process asynchronously
  processPaymentNotification(req.body).catch(console.error);
});
```

---

## Extracting Your Order ID

The `order_id` in the Midtrans payload contains a gateway-added prefix. To recover your original `orderId`:

```ts
function extractCallerOrderId(midtransOrderId: string): string {
  // Format: "{8-char-prefix}_{yourOrderId}"
  const underscoreIndex = midtransOrderId.indexOf("_");
  if (underscoreIndex === -1) return midtransOrderId;
  return midtransOrderId.substring(underscoreIndex + 1);
}

// Example
extractCallerOrderId("a1b2c3d4_ORDER-2026-0001"); // → "ORDER-2026-0001"
```

---

## TypeScript Implementation

Full webhook handler with status-based logic:

```ts
import express from "express";

const app = express();
app.use(express.json());

type MidtransTransactionStatus =
  | "pending"
  | "settlement"
  | "capture"
  | "authorize"
  | "deny"
  | "cancel"
  | "expire"
  | "error"
  | "failure"
  | "refund"
  | "partial_refund";

interface MidtransWebhookPayload {
  order_id: string;
  transaction_id: string;
  transaction_status: MidtransTransactionStatus;
  transaction_time: string;
  payment_type: string;
  gross_amount: string;
  fraud_status?: string;
  status_code: string;
  status_message: string;
  signature_key: string;
  merchant_id: string;
  currency: string;
}

function extractCallerOrderId(midtransOrderId: string): string {
  const idx = midtransOrderId.indexOf("_");
  return idx === -1 ? midtransOrderId : midtransOrderId.substring(idx + 1);
}

app.post("/webhook/payment", (req, res) => {
  // Always acknowledge immediately
  res.status(200).send("OK");

  const payload = req.body as MidtransWebhookPayload;
  const orderId = extractCallerOrderId(payload.order_id);

  handlePaymentUpdate(orderId, payload).catch(console.error);
});

async function handlePaymentUpdate(
  orderId: string,
  payload: MidtransWebhookPayload
): Promise<void> {
  const { transaction_status, fraud_status } = payload;

  switch (transaction_status) {
    case "settlement":
    case "capture":
      if (fraud_status === undefined || fraud_status === "accept") {
        // Payment successful — fulfill the order
        await fulfillOrder(orderId);
      } else if (fraud_status === "challenge") {
        // Flag for manual review
        await flagOrderForReview(orderId);
      }
      break;

    case "deny":
    case "cancel":
    case "expire":
    case "failure":
    case "error":
      // Payment failed — release any held inventory or notify the customer
      await handleFailedOrder(orderId, transaction_status);
      break;

    case "pending":
      // Still awaiting payment — no action needed, just log
      console.log(`Order ${orderId} is pending.`);
      break;

    case "refund":
    case "partial_refund":
      await handleRefund(orderId, transaction_status);
      break;
  }
}

// Stub implementations
async function fulfillOrder(orderId: string) { /* ... */ }
async function flagOrderForReview(orderId: string) { /* ... */ }
async function handleFailedOrder(orderId: string, status: string) { /* ... */ }
async function handleRefund(orderId: string, status: string) { /* ... */ }

app.listen(3000);
```

---

## Security Notes

- **Signature verification is done by the gateway** before forwarding. You do not need to re-verify the `signature_key` field.
- **Deduplication is handled by the gateway.** Duplicate Midtrans notifications will not be forwarded twice within a 60-minute window.
- **Your `WebhookUrl` must be HTTPS.** HTTP URLs are rejected and the notification is silently dropped.
- **Private/internal IPs are blocked.** The gateway performs SSRF protection — it will not forward to any of the following:
  - Loopback: `127.x.x.x`, `::1`
  - Private: `10.x.x.x`, `172.16–31.x.x`, `192.168.x.x`
  - Link-local: `169.254.x.x`, IPv6 link-local (`fe80::/10`)
  - Carrier-grade NAT: `100.64–127.x.x`
  - Reserved/multicast: `0.x.x.x`, `224.x.x.x` and above
  - IPv6 unique-local: `fc00::/7`
  - Unresolvable hostnames
