# Midtrans Snap Integration — Reference Documentation

**Source:** https://docs.midtrans.com/docs/snap-snap-integration-guide  
**Scope:** Backend-only integration for this gateway. Frontend Snap.js display is handled by child apps, not this gateway.

---

## What is Midtrans Snap?

Snap is Midtrans's pre-built payment UI product. Instead of building a custom checkout page, Snap provides a ready-made payment modal that supports all Midtrans payment methods (credit card, bank transfer, e-wallets, QRIS, etc.).

**The key concept:** Snap is two things working together:
1. **A backend token request** — your server calls Midtrans with order details + Server Key and gets back a short-lived transaction token.
2. **A frontend display** — the child app loads `snap.js` with a Client Key and uses the token to show the payment modal to the end user.

**This gateway only handles step 1.** It acts as the secure intermediary that holds the Server Keys and creates tokens on behalf of child apps.

---

## Integration Flow (from this gateway's perspective)

```
Child App → POST /api/midtrans/{env}/create-token
               ↓
          This Gateway (holds Server Key)
               ↓
          POST https://app.{sandbox|midtrans}.com/snap/v1/transactions
               ↓
          { token, redirect_url } ← Midtrans responds
               ↓
          Return to Child App
               ↓
          Child App frontend uses token + Client Key to show Snap UI
```

The child app never sees the Server Key. It only receives the token, which is short-lived and scoped to one transaction.

---

## Midtrans API Reference

### Endpoints

| Environment | Method | URL |
|---|---|---|
| **Sandbox** | POST | `https://app.sandbox.midtrans.com/snap/v1/transactions` |
| **Production** | POST | `https://app.midtrans.com/snap/v1/transactions` |

### HTTP Headers

| Header | Value |
|---|---|
| `Content-Type` | `application/json` |
| `Accept` | `application/json` |
| `Authorization` | `Basic {Base64("ServerKey:")}` |

**Authorization detail:** Midtrans uses HTTP Basic Auth. The username is the Server Key, the password is empty (note the trailing colon before encoding).

```
AUTH_STRING = Base64Encode(ServerKey + ":")
```

### Minimum Request Body

```json
{
  "transaction_details": {
    "order_id": "ORDER-123",
    "gross_amount": 10000
  }
}
```

| Field | Type | Notes |
|---|---|---|
| `order_id` | string | Unique per transaction. Alphanumeric, `-`, `_`, `~`, `.`. Max 50 chars. |
| `gross_amount` | integer | Total amount in IDR (smallest unit, no decimals). |

### Recommended Additional Fields

```json
{
  "transaction_details": {
    "order_id": "ORDER-123",
    "gross_amount": 10000
  },
  "customer_details": {
    "first_name": "Budi",
    "last_name": "Pratama",
    "email": "budi@example.com",
    "phone": "08111222333"
  },
  "item_details": [
    {
      "id": "ITEM-001",
      "price": 10000,
      "quantity": 1,
      "name": "Product Name"
    }
  ],
  "credit_card": {
    "secure": true
  }
}
```

### Successful Response

```json
{
  "token": "66e4fa55-fdac-4ef9-91b5-733b97d1b862",
  "redirect_url": "https://app.sandbox.midtrans.com/snap/v2/vtweb/66e4fa55-fdac-4ef9-91b5-733b97d1b862"
}
```

The `token` is used by the child app frontend with `snap.js`. The `redirect_url` is an alternative for redirect-based flows.

### Status Codes

| Code | Meaning |
|---|---|
| `201` | Token created successfully |
| `401` | Wrong Server Key — check authorization |
| `4xx` | Bad request — invalid parameters |
| `5xx` | Midtrans internal error — safe to retry |

---

## API Keys — Two Types

Midtrans issues two keys per environment (Sandbox and Production):

| Key | Where Used | Who Holds It |
|---|---|---|
| **Server Key** | Backend only — used to call Snap API | **This gateway only.** Never exposed to clients or child apps. |
| **Client Key** | Frontend only — loaded in `snap.js` | **Each child app** holds its own Client Key and loads it directly in their frontend. |

**Security rule:** The Server Key must never leave the backend. The Client Key is public-safe and is entirely the child app's responsibility.

This gateway only needs the **Server Key** for each Midtrans environment (Sandbox and Production). The Client Key is **not stored or managed here** — each child app registers with their own Midtrans account and uses their own Client Key in their frontend independently.

---

## Multi-App Architecture

This gateway manages many child apps. Each child app is a `Db_Application` record with one or more `Db_Environment` records (e.g. `staging`, `production`). Each environment has an auto-generated `ApiKey` (32-char hex) used by the child app to authenticate requests to this gateway.

```
Db_Application
  └── Db_Environment (staging)   → ApiKey: "abc123..."
  └── Db_Environment (production) → ApiKey: "def456..."
```

When a child app calls this gateway to create a Snap token, it authenticates using its environment's `ApiKey`. The gateway then determines which Midtrans environment to use (Sandbox or Production) and calls Midtrans using the appropriate **Server Key** from appsettings.

---

## Dual-Environment Design Intent

This gateway exposes **two separate endpoint paths** — one for sandbox (staging), one for production. Each environment:
- Uses its own Midtrans Server Key (from appsettings)
- Can be independently enabled or disabled via config flags
- Hits a different Midtrans base URL

### Why two separate endpoints?

Child apps often need to test payment flows without touching real money. By routing to this gateway's sandbox endpoint, they get a fully working Snap token backed by Midtrans's test environment — without this gateway or the child app needing separate Midtrans sandbox accounts per environment.

Meanwhile, production endpoints stay isolated. If a child app accidentally sends to the wrong endpoint, the worst case is a failed sandbox transaction, not a real payment.

### Enable/Disable Flags

The `EnableSandbox` and `EnableProduction` flags control whether the respective endpoints are active at all — not just which keys are used. This allows:

- **Dev servers:** `EnableSandbox: true`, `EnableProduction: false` — production key is never loaded, endpoint doesn't exist.
- **Production servers:** `EnableSandbox: false`, `EnableProduction: true` — no sandbox endpoint, production key loaded in protected environment only.
- **Both true:** Possible for transitional setups, but not the default recommendation.

---

## Proposed AppSettings Schema

```json
{
  "Midtrans": {
    "Sandbox": {
      "ServerKey": "SB-Mid-server-xxxxxxxxxxxxxxxx",
      "IsEnabled": true
    },
    "Production": {
      "ServerKey": "Mid-server-xxxxxxxxxxxxxxxx",
      "IsEnabled": false
    }
  }
}
```

**No Client Key is stored here.** Each child app holds their own Midtrans Client Key and uses it directly in their frontend with `snap.js`. The gateway has no involvement in that.

**In `appsettings.development.json`:** Only the Sandbox block is populated with real keys. Production block has a placeholder `ServerKey` with `IsEnabled: false`.

**In production environment / secrets manager:** Production block is populated with the real key. Sandbox block has `IsEnabled: false`.

---

## After Payment: Webhook Notification

When a transaction status changes (paid, failed, expired), Midtrans sends a POST notification to the gateway's configured **Notification URL** (set in Midtrans Dashboard → Settings → Configuration).

The gateway must:
1. Receive the webhook
2. Verify it (Midtrans provides a signature key for HMAC-SHA512 verification)
3. Forward it to the relevant child app's registered webhook endpoint

This is covered in a subsequent task — **not in scope for task-001**.

---

## Test Credentials (Sandbox)

| Field | Value |
|---|---|
| Card Number | `4811 1111 1111 1114` |
| CVV | `123` |
| Expiry Month | Any (e.g. `02`) |
| Expiry Year | Any future year (e.g. `2025`) |
| OTP/3DS | `112233` |

---

## Snap.js URLs (for child app reference — not this gateway's concern)

The child app independently loads snap.js using **their own Midtrans Client Key**:

| Environment | snap.js URL |
|---|---|
| Sandbox | `https://app.sandbox.midtrans.com/snap/snap.js` |
| Production | `https://app.midtrans.com/snap/snap.js` |

```html
<script src="https://app.sandbox.midtrans.com/snap/snap.js"
        data-client-key="THEIR_OWN_CLIENT_KEY"></script>
```

They then call `window.snap.pay(token)` or `window.snap.embed(token, { embedId: '...' })` using the token returned by this gateway. The Client Key is never passed through this gateway at any point.

---

## Summary: What This Gateway Needs to Implement (task-001 scope)

1. **Read config** — Load `Midtrans:Sandbox` and `Midtrans:Production` blocks from appsettings, including `IsEnabled` flags.
2. **Conditional endpoint registration** — Only register sandbox routes if `IsEnabled: true`, same for production.
3. **Token creation endpoint** — Accept a request from a child app (order_id + gross_amount + optional extras), call Midtrans Snap API with the correct Server Key for the environment, return the `{ token, redirect_url }` response.
4. **Authorization** — Backend to Midtrans: `Basic Base64(ServerKey + ":")`. Child app to this gateway: JWT-authenticated (existing auth system).
