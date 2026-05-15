# Redirect URLs

After a customer completes (or fails) a payment on the Midtrans Snap page, they are redirected back to your application via the gateway's callback endpoints. The gateway reads your environment's configured URLs and bounces the customer there.

---

## How It Works

```
Customer pays on Midtrans Snap
         │
         ▼
Gateway callback endpoint
  /api/midtrans/snap/callback?order_id=...          ← success / pending
  /api/midtrans/snap/callback/error?order_id=...    ← error / denial
         │
         ▼
HTTP 302 redirect to your configured URL
  SuccessResponseUrl?order_id={midtransOrderId}&status_code=...&transaction_status=...
  FailureResponseUrl?order_id={midtransOrderId}&status_code=...&transaction_status=...
```

Your application receives the customer as a browser redirect with `order_id` appended as a query parameter. Use this to look up the order and show the appropriate page.

---

## Configuring Your URLs

Set these fields on your **Environment** via the gateway dashboard:

| Field | Description |
|---|---|
| `SuccessResponseUrl` | Where to send the customer after a successful or pending payment. |
| `FailureResponseUrl` | Where to send the customer after a denied or errored payment. |

**Examples:**

```
SuccessResponseUrl: https://yourapp.com/payment/success
FailureResponseUrl: https://yourapp.com/payment/failed
```

---

## Callback Endpoints (Gateway → Your Browser)

These endpoints are called by Midtrans Snap after the payment flow completes. You do not call them directly.

| Route | Condition | Redirects To |
|---|---|---|
| `GET /api/midtrans/snap/callback` | Production — success / pending | `SuccessResponseUrl` |
| `GET /api/midtrans/sandbox/snap/callback` | Sandbox — success / pending | `SuccessResponseUrl` |
| `GET /api/midtrans/snap/callback/error` | Production — error / deny | `FailureResponseUrl` |
| `GET /api/midtrans/sandbox/snap/callback/error` | Sandbox — error / deny | `FailureResponseUrl` |

---

## Query Parameters on Redirect

The gateway forwards **all** query parameters received from Midtrans directly to your URL. Midtrans typically sends:

```
https://yourapp.com/payment/success?order_id=a1b2c3d4_ORDER-2026-0001&status_code=200&transaction_status=settlement
https://yourapp.com/payment/failed?order_id=a1b2c3d4_ORDER-2026-0001&status_code=202&transaction_status=deny
```

> **Important:** `order_id` in the redirect URL is the **gateway-prefixed Midtrans order ID** (e.g. `a1b2c3d4_ORDER-2026-0001`), not your original `orderId`. Strip the prefix to recover your order ID:

```ts
function extractCallerOrderId(midtransOrderId: string): string {
  const idx = midtransOrderId.indexOf("_");
  return idx === -1 ? midtransOrderId : midtransOrderId.substring(idx + 1);
}

// e.g. on your success page:
const raw = new URLSearchParams(window.location.search).get("order_id") ?? "";
const orderId = extractCallerOrderId(raw); // → "ORDER-2026-0001"
```

---

## Handling the Redirect

**Do not use the redirect alone to confirm payment.** The redirect only signals that the customer returned from Midtrans — it does not guarantee the payment succeeded. Always verify payment status via the webhook or by calling [Check Payment Status](./check-payment-status.md).

Recommended flow on your success page:

1. Read `order_id` from the URL query string.
2. Call your backend to look up the order status.
3. If already marked as paid (from webhook), show the confirmed success UI.
4. If still `pending`, show a "payment is being processed" message and poll or wait for the webhook.

---

## TypeScript Implementation

### React Router (loader)

```tsx
// routes/payment/success.tsx
import { useSearchParams } from "react-router";

export default function PaymentSuccessPage() {
  const [searchParams] = useSearchParams();
  const orderId = searchParams.get("order_id");

  return (
    <div>
      <h1>Payment Received</h1>
      <p>Order: {orderId}</p>
      <p>We are confirming your payment. You will receive an email shortly.</p>
    </div>
  );
}
```

### Next.js (App Router)

```tsx
// app/payment/success/page.tsx
export default function PaymentSuccessPage({
  searchParams,
}: {
  searchParams: { order_id?: string };
}) {
  const orderId = searchParams.order_id;

  return (
    <div>
      <h1>Payment Received</h1>
      <p>Order: {orderId}</p>
    </div>
  );
}
```

### Verifying status after redirect (fetch from client)

```ts
async function verifyOrderAfterRedirect(orderId: string): Promise<string> {
  // Call your own backend — never expose your API key to the browser
  const res = await fetch(`/api/orders/${encodeURIComponent(orderId)}/status`);
  const data = await res.json();
  return data.paymentStatus; // "paid" | "pending" | "failed"
}
```

Your backend then calls the gateway's [Check Payment Status](./check-payment-status.md) endpoint using the API key server-side.

---

## Notes

- The redirect happens immediately when the customer closes the Midtrans Snap iframe or completes the payment flow. The webhook notification may arrive slightly before or after the redirect.
- If `SuccessResponseUrl` or `FailureResponseUrl` is not set on the environment, the gateway will return a plain `200 OK` instead of redirecting.
- Both sandbox and production redirects follow the same logic; only the callback path differs.
