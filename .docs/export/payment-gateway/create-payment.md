# Create Payment

Generate a Midtrans Snap payment token. The gateway automatically routes the request to **Sandbox** or **Production** based on the `IsSandbox` flag of the environment that matches your API key.

---

## Endpoint

```
POST /api/snap/token
```

---

## Headers

| Header | Required | Description |
|---|---|---|
| `X-Api-Key` | Yes | Your environment's API key |
| `Content-Type` | Yes | `application/json` |

---

## Request Body

```json
{
  "orderId": "ORDER-2026-0001",
  "grossAmount": 150000,
  "customerDetails": {
    "firstName": "Budi",
    "lastName": "Santoso",
    "email": "budi@example.com",
    "phone": "081234567890"
  },
  "itemDetails": [
    {
      "id": "ITEM-001",
      "price": 150000,
      "quantity": 1,
      "name": "Premium Course"
    }
  ]
}
```

### Fields

| Field | Type | Required | Rules |
|---|---|---|---|
| `orderId` | `string` | Yes | Max 42 characters. Must be unique per environment. |
| `grossAmount` | `integer` | Yes | Must be ≥ 1. Unit: IDR (Rupiah). Must equal the sum of all `itemDetails[].price × quantity` when `itemDetails` is provided. |
| `customerDetails` | `object` | No | Customer info passed to Midtrans. |
| `customerDetails.firstName` | `string` | No | |
| `customerDetails.lastName` | `string` | No | |
| `customerDetails.email` | `string` | No | |
| `customerDetails.phone` | `string` | No | |
| `itemDetails` | `array` | No | Line items for the transaction. |
| `itemDetails[].id` | `string` | No | Your internal item identifier. |
| `itemDetails[].price` | `integer` | No | Unit price in IDR. |
| `itemDetails[].quantity` | `integer` | No | Quantity of this item. |
| `itemDetails[].name` | `string` | No | Display name shown in Midtrans UI. |

> **Note:** The `orderId` you send becomes the `callerOrderId` inside the gateway. The actual Midtrans `order_id` is prefixed internally as `{envPrefix}_{orderId}` to prevent collisions. You never need to know the Midtrans order ID — always use your own `orderId` when calling status or cancel.

---

## Response

### 200 OK — Token created

```json
{
  "success": true,
  "code": 200,
  "message": "Snap token created successfully.",
  "errors": null,
  "data": {
    "token": "66e4fa55-fdac-4ef9-91b5-733b97d1b862",
    "redirectUrl": "https://app.midtrans.com/snap/v2/vtweb/66e4fa55-fdac-4ef9-91b5-733b97d1b862"
  }
}
```

| Field | Type | Description |
|---|---|---|
| `data.token` | `string` | Snap token. Pass to `window.snap.pay(token)` on your frontend. |
| `data.redirectUrl` | `string` | Full-page payment URL. Redirect the customer here for a non-popup experience. |

### 400 Bad Request — Validation failure

```json
{
  "success": false,
  "code": 400,
  "message": "One or more validation errors occurred.",
  "errors": [
    "OrderId: The OrderId field is required.",
    "GrossAmount: GrossAmount must be greater than 0."
  ],
  "data": null
}
```

### 401 Unauthorized — Missing or invalid API key

```json
{
  "success": false,
  "code": 401,
  "message": "Invalid API key.",
  "errors": null,
  "data": null
}
```

### 409 Conflict — Duplicate orderId

```json
{
  "success": false,
  "code": 409,
  "message": "A transaction with OrderId 'ORDER-2026-0001' already exists for this environment.",
  "errors": null,
  "data": null
}
```

### 502 Bad Gateway — Midtrans unreachable

Returned when the Midtrans Snap API call fails. Note: the HTTP status is `502` but the `code` in the JSON body is `500` (internal error code from the gateway).

```json
{
  "success": false,
  "code": 500,
  "message": "Midtrans API returned an error. Please try again.",
  "errors": null,
  "data": null
}
```

### 503 Service Unavailable — Payment environment disabled

```json
{
  "success": false,
  "code": 503,
  "message": "The production payment environment is currently disabled.",
  "errors": null,
  "data": null
}
```

---

## TypeScript Implementation

```ts
const BASE_URL = "https://your-gateway-domain.com";

interface SnapTokenRequest {
  orderId: string;
  grossAmount: number;
  customerDetails?: {
    firstName?: string;
    lastName?: string;
    email?: string;
    phone?: string;
  };
  itemDetails?: Array<{
    id?: string;
    price: number;
    quantity: number;
    name?: string;
  }>;
}

interface SnapTokenResponse {
  token: string;
  redirectUrl: string;
}

interface GatewayResponse<T> {
  success: boolean;
  code: number;
  message: string;
  errors: string[] | null;
  data: T | null;
}

async function createPayment(
  apiKey: string,
  payload: SnapTokenRequest
): Promise<SnapTokenResponse> {
  const res = await fetch(`${BASE_URL}/api/snap/token`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Api-Key": apiKey,
    },
    body: JSON.stringify(payload),
  });

  const json: GatewayResponse<SnapTokenResponse> = await res.json();

  if (!json.success || !json.data) {
    throw new Error(json.message ?? "Failed to create payment token");
  }

  return json.data;
}

// Usage
const { token, redirectUrl } = await createPayment("your-api-key", {
  orderId: "ORDER-2026-0001",
  grossAmount: 150000,
  customerDetails: {
    firstName: "Budi",
    lastName: "Santoso",
    email: "budi@example.com",
    phone: "081234567890",
  },
  itemDetails: [
    { id: "ITEM-001", price: 150000, quantity: 1, name: "Premium Course" },
  ],
});

// Option A: Snap popup
window.snap.pay(token);

// Option B: Full-page redirect
window.location.href = redirectUrl;
```

---

## Notes

- Call this endpoint **from your backend server**, not from the browser. Your `X-Api-Key` must never be exposed to the client.
- Each `orderId` must be unique within an environment. Reusing an orderId for a different transaction will return `409`.
- The gateway does not expire unused tokens — token expiry is controlled by Midtrans (typically 24 hours).
