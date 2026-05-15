# User Flow — Order ID Scoping: Enforce API Key Ownership & Composite Uniqueness

## Use Case
**Tenant-Isolated Order ID Handling**

Child application developers interact with the payment gateway's Snap endpoints using their environment-specific API key. The system must ensure that (1) each `orderId` is unique within a single environment, (2) no tenant can access, query, or cancel another tenant's orders, and (3) concurrent duplicate submissions are safely rejected at the database level.

---

## User Levels (Action × Role)

| Action / Capability | Child App Developer (via X-Api-Key) | Gateway Operator (system-level) |
|--------------------|:-----------------------------------:|:-------------------------------:|
| Create Snap token (`POST /api/snap/token`) | ✅ | ✅ (via test purchase) |
| Reuse same orderId in same environment | ❌ (409 Conflict) | ❌ (409 Conflict) |
| Reuse same orderId in different environment | ✅ | ✅ |
| Check payment status (`GET /api/snap/status/{orderId}`) | ✅ (own env only) | — |
| Cancel payment (`POST /api/snap/cancel/{orderId}`) | ✅ (own env only) | — |
| Access another tenant's order by orderId | ❌ (404 Not Found) | ❌ |

---

## User Flows

### Flow #1 – Create Snap Token (Happy Path)
**As a child app developer**

1. Send `POST /api/snap/token` with `X-Api-Key` header and request body containing `orderId`
2. System resolves environment from API key
3. System checks if `(EnvironmentId, CallerOrderId)` already exists in `SnapTransactions`
4. No duplicate found → system inserts the transaction and calls Midtrans Snap API
5. Receive `200 OK` with Snap token and redirect URL

---

### Flow #2 – Create Snap Token with Duplicate orderId (Same Environment)
**As a child app developer**

1. Send `POST /api/snap/token` with the same `orderId` used in a previous request for the same environment
2. System resolves environment from API key
3. System finds existing `(EnvironmentId, CallerOrderId)` match
4. Receive `409 Conflict` with message: "A transaction with OrderId '{orderId}' already exists for this environment."

---

### Flow #3 – Create Snap Token with Same orderId (Different Environment)
**As a child app developer**

1. Send `POST /api/snap/token` with `X-Api-Key` belonging to Environment B, using an `orderId` that already exists in Environment A
2. System resolves Environment B from API key
3. System checks `(EnvironmentB.Id, CallerOrderId)` — no match found
4. Proceed to create token normally → `200 OK`
5. Cross-tenant isolation confirmed: same orderId string is valid across different environments

---

### Flow #4 – Concurrent Duplicate Token Requests (Race Condition)
**As a child app developer (two simultaneous requests)**

1. Two simultaneous `POST /api/snap/token` requests arrive with the same `orderId` for the same environment
2. Both pass the application-layer duplicate check (narrow race window)
3. First request inserts successfully
4. Second request fails on the DB composite unique index `(EnvironmentId, CallerOrderId)`
5. System catches the `DbUpdateException` and returns `409 Conflict` (not `500`)
6. System logs a warning for audit purposes

---

### Flow #5 – Check Payment Status (Own Environment)
**As a child app developer**

1. Send `GET /api/snap/status/{orderId}` with `X-Api-Key`
2. System resolves environment from API key
3. System queries `SnapTransactions` by `(EnvironmentId, CallerOrderId)`
4. Transaction found → calls Midtrans status API → returns `200 OK` with merged status

---

### Flow #6 – Check Payment Status (Another Tenant's Order)
**As a child app developer**

1. Send `GET /api/snap/status/{orderId}` with own `X-Api-Key`, but using an orderId that belongs to a different tenant
2. System resolves caller's environment from API key
3. System queries `SnapTransactions` by `(caller's EnvironmentId, orderId)` — no match
4. Receive `404 Not Found` — no cross-tenant data leaked

---

### Flow #7 – Cancel Payment (Own Environment)
**As a child app developer**

1. Send `POST /api/snap/cancel/{orderId}` with `X-Api-Key`
2. System resolves environment from API key
3. System queries `SnapTransactions` by `(EnvironmentId, CallerOrderId)`
4. Transaction found → calls Midtrans cancel API → updates DB → returns `200 OK`

---

## Key Rules / Constraints

- `CallerOrderId` must be unique per environment, enforced by a DB composite unique index on `(EnvironmentId, CallerOrderId)`
- The existing `MidtransOrderId` UNIQUE constraint remains as a secondary safety net — not removed
- Status and Cancel endpoints scope all queries to the caller's `EnvironmentId` — a miss is always `404`, never `403`
- Race conditions on duplicate inserts are caught at the DB level and surfaced as `409 Conflict`
- No frontend changes required — this is a backend-only security hardening task

---

## Page Mapping

| Endpoint | Status | Changes |
|----------|--------|---------|
| `POST /api/snap/token` (CreateTokenAsync) | **EXISTING** | Update duplicate check to `(EnvironmentId, CallerOrderId)`, add race condition handling |
| `GET /api/snap/status/{orderId}` | **EXISTING** | Already correctly scoped — no changes needed |
| `POST /api/snap/cancel/{orderId}` | **EXISTING** | Already correctly scoped — no changes needed |
| `AppDbContext.ConfigureMidtransSchema` | **EXISTING** | Add composite unique index declaration |
| `migrations.md` | **EXISTING** | Append migration instructions |

---

**End of Userflow**
