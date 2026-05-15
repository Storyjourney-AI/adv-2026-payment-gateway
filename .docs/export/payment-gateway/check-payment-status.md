# Check Payment Status

Retrieve the current payment status for an order, combining live data from Midtrans with the gateway's stored record.

---

## Endpoint

```
GET /api/snap/status/{orderId}
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

## Response

### 200 OK — Status retrieved

```json
{
  "success": true,
  "code": 200,
  "message": "Payment status retrieved successfully.",
  "errors": null,
  "data": {
    "callerOrderId": "ORDER-2026-0001",
    "midtransOrderId": "a1b2c3d4_ORDER-2026-0001",
    "gatewayStatus": "settlement",
    "midtransStatus": "settlement",
    "fraudStatus": "accept",
    "grossAmount": "150000.00",
    "midtransTransactionId": "abc123def456",
    "paymentType": "bank_transfer",
    "createdAt": "2026-03-31T08:00:00Z",
    "updatedAt": "2026-03-31T08:05:00Z"
  }
}
```

### Response Fields

| Field | Type | Description |
|---|---|---|
| `callerOrderId` | `string` | The `orderId` you originally sent. |
| `midtransOrderId` | `string` | The internal order ID used at Midtrans (prefixed by gateway). Informational only. |
| `gatewayStatus` | `string` | Last status recorded by the gateway (from webhook or prior sync). |
| `midtransStatus` | `string` | Live status returned directly from Midtrans API at request time. |
| `fraudStatus` | `string` | Midtrans fraud detection result: `accept`, `challenge`, or `deny`. |
| `grossAmount` | `string` | Transaction amount as returned by Midtrans (e.g. `"150000.00"`). |
| `midtransTransactionId` | `string` | Midtrans internal transaction identifier. |
| `paymentType` | `string` | Payment method used (e.g. `bank_transfer`, `credit_card`, `gopay`). |
| `createdAt` | `string` | ISO 8601 UTC timestamp when the transaction was created in the gateway. |
| `updatedAt` | `string` | ISO 8601 UTC timestamp of the last status update. |

---

### Status Values

| Status | Meaning |
|---|---|
| `pending` | Payment initiated but not yet completed by the customer. |
| `settlement` | Payment successfully settled. Funds confirmed. |
| `capture` | Credit card payment captured (awaiting settlement). |
| `authorize` | Payment authorized but not yet captured. |
| `deny` | Payment denied by bank or Midtrans fraud detection. |
| `cancel` | Payment was cancelled (by customer or via API). |
| `expire` | Payment window expired before the customer completed payment. |
| `error` | Error occurred during payment processing. |
| `failure` | Payment failed. |
| `refund` | Payment fully refunded. |
| `partial_refund` | Payment partially refunded. |

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

### 502 Bad Gateway — Midtrans API error

Returned when Midtrans responds with a non-2xx status. `message` may contain the `status_message` from Midtrans.

```json
{
  "success": false,
  "code": 502,
  "message": "Midtrans status API returned an error.",
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

async function getPaymentStatus(
  apiKey: string,
  orderId: string
): Promise<PaymentStatusResponse> {
  const res = await fetch(`${BASE_URL}/api/snap/status/${encodeURIComponent(orderId)}`, {
    method: "GET",
    headers: {
      "X-Api-Key": apiKey,
    },
  });

  const json: GatewayResponse<PaymentStatusResponse> = await res.json();

  if (!json.success || !json.data) {
    throw new Error(json.message ?? "Failed to retrieve payment status");
  }

  return json.data;
}

// Usage
const status = await getPaymentStatus("your-api-key", "ORDER-2026-0001");

if (status.midtransStatus === "settlement") {
  console.log("Payment confirmed!");
} else if (status.midtransStatus === "pending") {
  console.log("Awaiting payment...");
}
```

---

## Notes

- `midtransStatus` reflects the live status fetched from Midtrans at the time of the request.
- `gatewayStatus` is synced to match `midtransStatus` before the response is built. Both fields will always be equal in a successful response from this endpoint.
- The gateway persists the updated status to the database if it changed.
