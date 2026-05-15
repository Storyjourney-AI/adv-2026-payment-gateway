# Payment Gateway — API Reference

Complete reference for integrating your application with the Payment Gateway.

---

## Table of Contents

1. [Authentication](#1-authentication)
2. [Standard Response Envelope](#2-standard-response-envelope)
3. [Endpoint Reference](#3-endpoint-reference)
   - [3.1 Create Payment Token](#31-create-payment-token)
   - [3.2 Check Payment Status](#32-check-payment-status)
   - [3.3 Cancel Payment](#33-cancel-payment)
4. [Webhook Handling](#4-webhook-handling)
5. [Order ID Rules](#5-order-id-rules)

---

## 1. Authentication

All child-app endpoints require an `X-Api-Key` HTTP header for authentication.

You can find your API key on the **Application Detail page** → select your environment card → copy the API key.

### Example Header

```
X-Api-Key: your_api_key_here
```

Include this header in every request to the payment gateway API.

---

## 2. Standard Response Envelope

All API responses are wrapped in a `DataWrapper<T>` envelope:

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": { ... },
  "errors": null
}
```

| Field     | Type            | Description                                |
| --------- | --------------- | ------------------------------------------ |
| `success` | boolean         | Whether the request was successful         |
| `message` | string          | Human-readable status message              |
| `data`    | T \| null       | Response payload (null on failure)         |
| `errors`  | string[] \| null | Validation error details (null on success) |

---

## 3. Endpoint Reference

| Endpoint                      | Method | Description                  |
| ----------------------------- | ------ | ---------------------------- |
| `/api/snap/token`             | POST   | Create a Snap payment token  |
| `/api/snap/status/{orderId}`  | GET    | Check payment status         |
| `/api/snap/cancel/{orderId}`  | POST   | Cancel a pending payment     |

---

### 3.1 Create Payment Token

```
POST /api/snap/token
```

Creates a Midtrans Snap payment token and returns a redirect URL for the payment page.

#### Required Headers

```
X-Api-Key: your_api_key_here
Content-Type: application/json
```

#### Request Body

| Field             | Type   | Required | Constraints                            |
| ----------------- | ------ | -------- | -------------------------------------- |
| `orderId`         | string | Yes      | Unique per environment, max 42 chars   |
| `grossAmount`     | number | Yes      | Positive integer (in IDR)              |
| `customerDetails` | object | No       | Customer information (see below)       |
| `itemDetails`     | array  | No       | List of purchased items (see below)    |

#### `customerDetails` Object

| Field       | Type   | Description            |
| ----------- | ------ | ---------------------- |
| `firstName` | string | Customer first name    |
| `lastName`  | string | Customer last name     |
| `email`     | string | Customer email address |
| `phone`     | string | Customer phone number  |

#### `itemDetails` Array Items

| Field      | Type   | Description          |
| ---------- | ------ | -------------------- |
| `id`       | string | Item identifier      |
| `price`    | number | Price per unit (IDR) |
| `quantity` | number | Number of items      |
| `name`     | string | Name of the item     |

#### Example Request Body

```json
{
  "orderId": "order-001",
  "grossAmount": 50000,
  "customerDetails": {
    "firstName": "John",
    "lastName": "Doe",
    "email": "john@example.com",
    "phone": "08123456789"
  },
  "itemDetails": [
    {
      "id": "item-1",
      "price": 50000,
      "quantity": 1,
      "name": "Premium Subscription"
    }
  ]
}
```

#### Success Response — `200 OK`

```json
{
  "success": true,
  "message": "Snap token created successfully",
  "data": {
    "token": "snap-token-string",
    "redirectUrl": "https://app.midtrans.com/snap/v4/redirection/..."
  },
  "errors": null
}
```

#### Error Responses

| Code | Meaning                                        |
| ---- | ---------------------------------------------- |
| 400  | Invalid request body or missing required fields |
| 401  | Missing or invalid API key                     |
| 409  | Duplicate order ID for this environment        |
| 502  | Midtrans API error                             |

**400 — Validation Error**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "GrossAmount": ["GrossAmount must be greater than 0."]
  }
}
```

**401 — Unauthorized**

```json
{
  "success": false,
  "message": "Missing or invalid API key.",
  "data": null,
  "errors": null
}
```

**409 — Conflict**

```json
{
  "success": false,
  "message": "A transaction with OrderId 'order-001' already exists for this environment.",
  "data": null,
  "errors": null
}
```

**502 — Bad Gateway**

```json
{
  "success": false,
  "message": "Midtrans API returned an error. Please try again.",
  "data": null,
  "errors": null
}
```

#### cURL Example

```bash
curl -X POST https://your-gateway-url/api/snap/token \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your_api_key_here" \
  -d '{
    "orderId": "order-001",
    "grossAmount": 50000,
    "customerDetails": {
      "firstName": "John",
      "lastName": "Doe",
      "email": "john@example.com",
      "phone": "08123456789"
    },
    "itemDetails": [
      {
        "id": "item-1",
        "price": 50000,
        "quantity": 1,
        "name": "Premium Subscription"
      }
    ]
  }'
```

---

### 3.2 Check Payment Status

```
GET /api/snap/status/{orderId}
```

Retrieves the current status of a payment by order ID.

#### Required Headers

```
X-Api-Key: your_api_key_here
```

#### Path Parameters

| Parameter | Type   | Description                                       |
| --------- | ------ | ------------------------------------------------- |
| `orderId` | string | The order ID used when creating the payment token |

#### Success Response — `200 OK`

```json
{
  "success": true,
  "message": "Transaction status retrieved",
  "data": {
    "callerOrderId": "order-001",
    "midtransOrderId": "a1b2c3d4_order-001",
    "transactionStatus": "settlement",
    "fraudStatus": "accept",
    "grossAmount": "50000.00",
    "transactionId": "midtrans-txn-id",
    "paymentType": "bank_transfer",
    "transactionTime": "2026-01-15 10:30:00"
  },
  "errors": null
}
```

#### Error Responses

| Code | Meaning                          |
| ---- | -------------------------------- |
| 401  | Missing or invalid API key       |
| 404  | Order ID not found               |
| 502  | Midtrans API error               |

#### cURL Example

```bash
curl -X GET https://your-gateway-url/api/snap/status/order-001 \
  -H "X-Api-Key: your_api_key_here"
```

---

### 3.3 Cancel Payment

```
POST /api/snap/cancel/{orderId}
```

Cancels a pending payment. Only payments with status `pending` can be cancelled.

#### Required Headers

```
X-Api-Key: your_api_key_here
```

#### Path Parameters

| Parameter | Type   | Description                                  |
| --------- | ------ | -------------------------------------------- |
| `orderId` | string | The order ID of the pending payment to cancel |

#### Success Response — `200 OK`

```json
{
  "success": true,
  "message": "Transaction cancelled successfully",
  "data": {
    "callerOrderId": "order-001",
    "midtransOrderId": "a1b2c3d4_order-001",
    "transactionStatus": "cancel",
    "grossAmount": "50000.00",
    "transactionId": "midtrans-txn-id"
  },
  "errors": null
}
```

#### Error Responses

| Code | Meaning                                  |
| ---- | ---------------------------------------- |
| 401  | Missing or invalid API key               |
| 404  | Order ID not found                       |
| 409  | Transaction is not in a cancellable state |
| 502  | Midtrans API error                       |

#### cURL Example

```bash
curl -X POST https://your-gateway-url/api/snap/cancel/order-001 \
  -H "X-Api-Key: your_api_key_here"
```

---

## 4. Webhook Handling

When a payment status changes, Midtrans sends a notification to the gateway. The gateway stores the updated status and forwards the raw Midtrans notification payload to the `WebhookUrl` configured on your environment.

### Notification Payload

The forwarded payload contains the raw Midtrans notification. Key fields include:

```json
{
  "order_id": "a1b2c3d4_order-001",
  "transaction_status": "settlement",
  "fraud_status": "accept",
  "gross_amount": "50000.00",
  "transaction_id": "midtrans-txn-id"
}
```

### Important Notes

- The `order_id` in the forwarded payload is the Midtrans-prefixed ID (format: `{envId[0..8]}_{callerOrderId}`), not your raw caller order ID.
- Your webhook endpoint **must return a 2xx status code** to acknowledge receipt of the notification.
- **SSRF Guard:** The `WebhookUrl` must be a valid HTTPS URL pointing to a non-loopback, non-private IP address.

### Transaction Statuses

| Status       | Description                                     |
| ------------ | ----------------------------------------------- |
| `pending`    | Payment initiated, waiting for customer action  |
| `settlement` | Payment completed successfully                  |
| `cancel`     | Payment was cancelled                           |
| `deny`       | Payment was denied                              |
| `expire`     | Payment expired without completion              |

---

## 5. Order ID Rules

- **Uniqueness:** The `orderId` must be unique per environment. Reusing an order ID for the same API key returns `409 Conflict`.
- **Max Length:** The `orderId` can be at most **42 characters** long.
