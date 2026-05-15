# Cancel Payment

Cancel a pending Midtrans payment by order ID. Only transactions in a cancellable state can be cancelled.

---

## Endpoint

```
POST /api/snap/cancel/{orderId}
```

| Parameter | In | Required | Description |
|---|---|---|---|
| `orderId` | Path | Yes | The `orderId` you supplied when creating the payment. |

---

## Headers

| Header | Required | Description |
|---|---|---|
| `X-Api-Key` | Yes | Your environment's API key |

---

## Request Body

None. This endpoint requires no request body.

---

## Response

### 200 OK — Cancelled successfully

```json
{
  "success": true,
  "code": 200,
  "message": "Payment cancelled successfully.",
  "errors": null,
  "data": {
    "callerOrderId": "ORDER-2026-0001",
    "midtransOrderId": "a1b2c3d4_ORDER-2026-0001",
    "gatewayStatus": "cancel",
    "midtransStatus": "cancel",
    "fraudStatus": null,
    "grossAmount": "150000.00",
    "midtransTransactionId": "abc123def456",
    "paymentType": null,
    "createdAt": "2026-03-31T08:00:00Z",
    "updatedAt": "2026-03-31T08:10:00Z"
  }
}
```

The response shape is the same as [Check Payment Status](./check-payment-status.md#response-fields) with `gatewayStatus` and `midtransStatus` both set to `"cancel"`.

---

### 401 Unauthorized — Missing header

```json
{
  "success": false,
  "code": 401,
  "message": "X-Api-Key header is required.",
  "errors": null,
  "data": null
}
```

### 401 Unauthorized — Invalid API key

```json
{
  "success": false,
  "code": 401,
  "message": "Invalid API key.",
  "errors": null,
  "data": null
}
```

### 404 Not Found

```json
{
  "success": false,
  "code": 404,
  "message": "Transaction with orderId 'ORDER-2026-0001' not found.",
  "errors": null,
  "data": null
}
```

### 422 Unprocessable — Cannot cancel in current state

```json
{
  "success": false,
  "code": 422,
  "message": "Transaction cannot be cancelled in its current state.",
  "errors": null,
  "data": null
}
```

> Triggered when Midtrans returns HTTP `412 Precondition Failed`, meaning the transaction has already reached a terminal state. The `message` may contain a more specific reason returned by Midtrans instead of the fallback shown above.

### 502 Bad Gateway — Midtrans API error

Returned when Midtrans responds with a non-2xx status (other than `412`). The `message` may reflect a specific reason from Midtrans.

```json
{
  "success": false,
  "code": 502,
  "message": "Midtrans cancel API returned an error.",
  "errors": null,
  "data": null
}
```

### 502 Bad Gateway — Network/exception failure

```json
{
  "success": false,
  "code": 502,
  "message": "Failed to communicate with Midtrans. Please try again.",
  "errors": null,
  "data": null
}
```

---

## Cancellable States

Only transactions in **non-terminal** states can be cancelled:

| Status | Cancellable |
|---|---|
| `pending` | Yes |
| `authorize` | Yes |
| `settlement` | No |
| `capture` | No |
| `deny` | No |
| `cancel` | No (already cancelled) |
| `expire` | No |
| `error` / `failure` | No |

---

## TypeScript Implementation

```ts
const BASE_URL = "https://your-gateway-domain.com";

interface PaymentStatusResponse {
  callerOrderId: string;
  midtransOrderId: string;
  gatewayStatus: string | null;
  midtransStatus: string | null;
  fraudStatus: string | null;
  grossAmount: string;
  midtransTransactionId: string | null;
  paymentType: string | null;
  createdAt: string;
  updatedAt: string;
}

interface GatewayResponse<T> {
  success: boolean;
  code: number;
  message: string;
  errors: string[] | null;
  data: T | null;
}

async function cancelPayment(
  apiKey: string,
  orderId: string
): Promise<PaymentStatusResponse> {
  const res = await fetch(`${BASE_URL}/api/snap/cancel/${encodeURIComponent(orderId)}`, {
    method: "POST",
    headers: {
      "X-Api-Key": apiKey,
    },
  });

  const json: GatewayResponse<PaymentStatusResponse> = await res.json();

  if (res.status === 422) {
    throw new Error(`Cannot cancel: ${json.message}`);
  }

  if (!json.success || !json.data) {
    throw new Error(json.message ?? "Failed to cancel payment");
  }

  return json.data;
}

// Usage
try {
  const result = await cancelPayment("your-api-key", "ORDER-2026-0001");
  console.log("Cancelled. Status:", result.midtransStatus);
} catch (err) {
  console.error("Cancel failed:", err.message);
}
```

---

## Notes

- Cancellation forwards directly to Midtrans. The gateway updates its stored status upon successful cancellation.
- Attempting to cancel an already-terminal transaction returns `422`, not `400`. Check for `422` explicitly in your error handling.
- After cancellation, you can issue a new payment for the same customer by creating a new token with a **different `orderId`**.
