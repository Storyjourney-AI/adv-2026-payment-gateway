# Rate Limit Tuning Guide (Phase 1)

Dokumen ini menjelaskan konfigurasi awal rate limiter yang diaktifkan pada backend.

## Implementasi Saat Ini

- Middleware: `AddRateLimiter` + `UseRateLimiter`
- Lokasi konfigurasi: `PaymentGateway.Server/Program.cs`
- Definisi policy + key builder: `PaymentGateway.Server/Security/RateLimiting/RateLimitingDefinitions.cs`

## Daftar Policy Aktif

- `auth_login_strict`
  - permit: 5 / 60 detik
  - queue: 1
  - route target: `POST /api/auth/login`, `POST /api/auth/register`

- `auth_refresh_moderate`
  - permit: 12 / 60 detik
  - queue: 2
  - route target: `POST /api/auth/refresh`

- `auth_logout_moderate`
  - permit: 20 / 60 detik
  - queue: 4
  - route target: `POST /api/auth/logout`

- `snap_public_moderate`
  - permit: 20 / 60 detik
  - queue: 4
  - route target: `POST /api/snap/token` + endpoint deprecated token

- `snap_status_moderate`
  - permit: 30 / 60 detik
  - queue: 6
  - route target: `GET /api/snap/status/{orderId}`

- `snap_cancel_moderate`
  - permit: 10 / 60 detik
  - queue: 2
  - route target: `POST /api/snap/cancel/{orderId}`

- `webhook_tolerant`
  - permit: 120 / 60 detik
  - queue: 20
  - route target: `POST /api/midtrans/payment`, `POST /api/midtrans/sandbox/payment`

- `callback_lenient`
  - permit: 120 / 60 detik
  - queue: 20
  - route target: callback redirect endpoints (`/api/midtrans/snap/callback*`)

## Partition Key Strategy

- Base key: `METHOD + PATH + CLIENT_IP`
- Endpoint `X-Api-Key` menambahkan hash pendek API key (SHA-256 12 char pertama).
- Tujuan: mengurangi dampak satu IP yang membawa banyak tenant/API key.

## Response 429

Semua rejection rate limit mengembalikan:

```json
{
  "success": false,
  "message": "Too many requests. Please retry later.",
  "data": null,
  "errors": ["Rate limit exceeded."],
  "code": 429
}
```

## Catatan Tuning Lanjutan

- Jika traffic webhook valid sering kena 429, naikkan `webhook_tolerant` secara bertahap.
- Jika login brute-force tinggi, turunkan `auth_login_strict` (contoh dari 5 ke 3 per menit).
- Saat scale multi-instance, migrasikan limiter ke distributed store (Redis) dengan pola key yang sama.
