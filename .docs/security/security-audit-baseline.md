# Security Audit Baseline (Phase 0)

Tanggal audit: 2026-03-17  
Status: Completed  
Scope: Seluruh endpoint publik/anonymous backend + endpoint `X-Api-Key`

## 1) Ringkasan Temuan Baseline

- Permukaan serangan utama berada di endpoint anonymous `Auth`, `Snap`, dan `Webhook`.
- Saat ini belum ada rate limiting global/policy per endpoint.
- Belum ada verifikasi Turnstile/Captcha di endpoint sensitif anonymous.
- Validasi signature webhook Midtrans sudah ada, tetapi belum ada dedupe/idempotency dan anti-replay window.
- Forwarding webhook sudah memiliki guard dasar (`https` + non-loopback), namun belum ada kontrol private-network yang lebih ketat dan retry policy terukur.

## 2) Endpoint Inventory (Public/Anonymous)

### A. Auth Public Endpoints

Sumber: `PaymentGateway.Server/Authorization/Controllers/AuthController.cs`

- `POST /api/auth/register` (`[AllowAnonymous]`, saat ini nonaktif/return 404)
- `POST /api/auth/login` (`[AllowAnonymous]`)
- `POST /api/auth/refresh` (`[AllowAnonymous]`)
- `POST /api/auth/logout` (`[AllowAnonymous]`)

### B. Midtrans Public Endpoints

Sumber: `PaymentGateway.Server/Midtrans/Controllers/SnapController.cs`

- `POST /api/snap/token` (`[AllowAnonymous]`, auth via `X-Api-Key`)
- `POST /api/snap/sandbox/token` (`[AllowAnonymous]`, deprecated, auth via `X-Api-Key`)
- `POST /api/snap/production/token` (`[AllowAnonymous]`, deprecated, auth via `X-Api-Key`)
- `GET /api/snap/status/{orderId}` (`[AllowAnonymous]`, auth via `X-Api-Key`)
- `POST /api/snap/cancel/{orderId}` (`[AllowAnonymous]`, auth via `X-Api-Key`)
- `GET /api/midtrans/snap/callback` (`[AllowAnonymous]`)
- `GET /api/midtrans/sandbox/snap/callback` (`[AllowAnonymous]`)
- `GET /api/midtrans/snap/callback/error` (`[AllowAnonymous]`)
- `GET /api/midtrans/sandbox/snap/callback/error` (`[AllowAnonymous]`)

### C. Webhook Public Endpoints

Sumber: `PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs`

- `POST /api/midtrans/payment` (`[AllowAnonymous]`)
- `POST /api/midtrans/sandbox/payment` (`[AllowAnonymous]`)

## 3) Threat Mapping (Baseline)

### T1 — Brute-force login
- Target: `POST /api/auth/login`
- Dampak: account compromise, credential stuffing
- Kontrol saat ini: validasi model + autentikasi normal
- Gap: belum ada captcha dan rate limit ketat

### T2 — Refresh token abuse
- Target: `POST /api/auth/refresh`
- Dampak: session abuse / token endpoint flood
- Kontrol saat ini: refresh token via HttpOnly cookie
- Gap: belum ada rate limit/captcha untuk endpoint anonymous

### T3 — API key endpoint abuse (Snap)
- Target: `/api/snap/*` anonymous dengan `X-Api-Key`
- Dampak: resource exhaustion, brute-force API key probing
- Kontrol saat ini: validasi `X-Api-Key` + DB scope
- Gap: belum ada rate limiter dan bot challenge

### T4 — Forged webhook notification
- Target: `/api/midtrans/payment` dan `/api/midtrans/sandbox/payment`
- Dampak: perubahan status transaksi tidak sah
- Kontrol saat ini: signature verification (`MidtransSignatureHelper`)
- Gap: belum ada payload minimal enforcement yang eksplisit dan alarming terstruktur

### T5 — Replay & duplicate webhook
- Target: webhook endpoint Midtrans
- Dampak: status update berulang, side effect forwarding berulang
- Kontrol saat ini: update state langsung
- Gap: belum ada idempotency key dan replay window

### T6 — Webhook forwarding SSRF pivot (lanjutan)
- Target: forwarding ke `Environment.WebhookUrl`
- Dampak: akses ke network internal/target tak diinginkan
- Kontrol saat ini: `https` + bukan loopback
- Gap: belum ada blok private CIDR/host resolusi dan policy retry/timeout terukur

## 4) Matriks Proteksi Endpoint (Target Phase Selanjutnya)

| Endpoint Group | Rate Limit Policy | Captcha | Validation Tambahan | Logging/Audit |
|---|---|---|---|---|
| `POST /api/auth/login` | `auth_login_strict` | Ya | Email hash keying + anomaly counter | Success/fail + reason |
| `POST /api/auth/refresh` | `auth_refresh_moderate` | Ya | Cookie presence + challenge verify | Fail reason + burst events |
| `POST /api/auth/logout` | `auth_logout_moderate` | Tidak (awal) | Token cookie hygiene | Optional |
| `POST /api/snap/token` (+deprecated token endpoint) | `snap_public_moderate` | Ya | API key scope + body validation | API key hash + reject reason |
| `GET /api/snap/status/{orderId}` | `snap_status_moderate` | Ya | API key scope + order ownership | Query abuse pattern |
| `POST /api/snap/cancel/{orderId}` | `snap_cancel_moderate` | Ya | API key scope + state validation | Cancel fail reason |
| `POST /api/midtrans/*payment` | `webhook_tolerant` | Tidak | Signature + idempotency + anti-replay + payload minimal | Invalid signature/replay/duplicate metrics |
| callback redirect GET endpoints | `callback_lenient` | Tidak | Query sanity check | Low-priority tracking |

## 5) KPI Security Baseline (Disepakati untuk Monitoring)

- `rate_limit_reject_total` per route (`429` count)
- `captcha_validation_fail_total` per route
- `webhook_invalid_signature_total`
- `webhook_duplicate_total`
- `webhook_replay_suspected_total`
- `x_api_key_invalid_total` untuk endpoint `/api/snap/*`

## 6) Improvement Backlog Prioritas

### P0 (langsung dieksekusi)
- Implementasi rate limiting policy-based untuk seluruh endpoint publik/anonymous.
- Implementasi Turnstile untuk login + endpoint anonymous sensitif (`login`, `refresh`, `snap token/status/cancel`).
- Hardening webhook: idempotency, anti-replay window, payload minimal validation.

### P1
- Hardening lanjutan webhook forwarder: private network block, DNS/IP safety, retry policy terbatas.
- Tambah regression test keamanan otomatis backend/frontend.

### P2
- Migrasi rate limiter ke distributed store (Redis) saat traffic scale.
- Dashboard observability + alert threshold security.

## 7) Exit Criteria Phase 0 (Checklist)

- [x] Daftar endpoint public/anonymous terinventaris.
- [x] Threat map utama terdokumentasi.
- [x] Matriks proteksi endpoint disepakati.
- [x] KPI baseline untuk phase implementasi ditetapkan.
- [x] Backlog prioritas P0/P1/P2 disusun.
