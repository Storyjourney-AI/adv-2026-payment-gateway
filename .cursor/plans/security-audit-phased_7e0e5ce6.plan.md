---
name: security-audit-phased
overview: Menyusun dan mengeksekusi hardening security bertahap untuk semua endpoint public/anonymous, mencakup audit baseline, rate limiting, Turnstile, validasi webhook, dan pengujian per phase dengan status tracking yang bisa diperbarui sampai seluruh phase selesai.
todos:
  - id: phase-0-baseline-audit
    content: Susun baseline audit endpoint publik + threat map + prioritas P0/P1/P2
    status: completed
  - id: phase-1-rate-limit
    content: Implementasi rate limiter foundation di backend dengan policy per endpoint
    status: completed
  - id: phase-2-turnstile
    content: Implementasi Turnstile server-side + integrasi frontend login + endpoint anonymous sensitif
    status: completed
  - id: phase-3-webhook-hardening
    content: Tambah idempotency, anti-replay, payload validation, dan hardening forwarding webhook
    status: completed
  - id: phase-4-test-suite
    content: Bangun regression test backend/frontend dan jalankan test per phase
    status: completed
  - id: phase-5-operations
    content: Siapkan observability, alerting, runbook, dan rencana migrasi limiter ke Redis
    status: completed
isProject: false
---

# Security Audit & Improvement Plan (Phase-by-Phase)

## Keputusan Arsitektur Utama

- **Rate limit store**: mulai dari **in-memory** untuk time-to-protect tercepat, namun dibangun dengan interface agar siap upgrade ke Redis.
- **Captcha scope**: diterapkan pada **login + endpoint anonymous sensitif** (`/api/auth/login`, `/api/auth/refresh`, `/api/snap/token`, `/api/snap/status/{orderId}`, `/api/snap/cancel/{orderId}`).
- **Audit scope**: **semua endpoint publik/anonymous** pada backend.

## Area Kode Prioritas

- Backend bootstrap/middleware: [D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Program.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Program.cs)
- Auth endpoint: [D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Authorization/Controllers/AuthController.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Authorization/Controllers/AuthController.cs)
- Midtrans public endpoints: [D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Controllers/SnapController.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Controllers/SnapController.cs)
- Webhook handling: [D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs)
- Signature utility: [D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Utils/MidtransSignatureHelper.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Utils/MidtransSignatureHelper.cs)
- Frontend login flow: [D:/Work/Payment-gateaway-asp-react/paymentgateway.client/app/routes/auth/Page_Login.tsx](D:/Work/Payment-gateaway-asp-react/paymentgateway.client/app/routes/auth/Page_Login.tsx), [D:/Work/Payment-gateaway-asp-react/paymentgateway.client/app/services/auth/utils/auth.api.ts](D:/Work/Payment-gateaway-asp-react/paymentgateway.client/app/services/auth/utils/auth.api.ts)

## Tracking Status (Wajib per Phase)

- Buat dokumen status tunggal: [D:/Work/Payment-gateaway-asp-react/.docs/security/security-hardening-status.md](D:/Work/Payment-gateaway-asp-react/.docs/security/security-hardening-status.md)
- Setiap phase berisi:
  - `Status`: Not Started / In Progress / Blocked / Done
  - `Owner`
  - `Tanggal mulai/selesai`
  - `Checklist implementasi`
  - `Checklist test + evidence`
  - `Risk/rollback notes`

## Fase Implementasi

### Phase 0 — Baseline Security Audit & Threat Mapping

**Tujuan**: memotret risiko aktual sebelum perubahan.

**Aktivitas**

- Inventaris endpoint `[AllowAnonymous]` dan endpoint yang bergantung `X-Api-Key`.
- Klasifikasi ancaman: brute-force login, abuse token endpoint, replay webhook, forged webhook, volumetric abuse.
- Definisikan matriks proteksi endpoint (rate limit bucket + captcha required + logging policy).
- Tetapkan KPI keamanan (contoh: % request ditolak oleh limiter, % captcha fail, webhook invalid signature rate).

**Output**

- Dokumen audit baseline: [D:/Work/Payment-gateaway-asp-react/.docs/security/security-audit-baseline.md](D:/Work/Payment-gateaway-asp-react/.docs/security/security-audit-baseline.md)
- Improvement backlog prioritas P0/P1/P2.

**Gate test phase**

- Validasi daftar endpoint publik vs implementasi aktual.
- Review checklist risiko disetujui.

---

### Phase 1 — Global Rate Limiting Foundation (In-Memory, Redis-Ready)

**Tujuan**: menahan abuse cepat pada endpoint publik.

**Aktivitas**

- Tambahkan `AddRateLimiter` + `UseRateLimiter` di [Program.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Program.cs).
- Definisikan beberapa policy terpisah:
  - `auth_login_strict` untuk `/api/auth/login`.
  - `auth_refresh_moderate` untuk `/api/auth/refresh`.
  - `snap_public_moderate` untuk endpoint `/api/snap/`* anonymous.
  - `webhook_tolerant` untuk `/api/midtrans/`* dengan batas tinggi tapi tetap terkendali.
- Buat key selector yang mempertimbangkan IP + endpoint + identity hint (mis. hash email untuk login bila tersedia).
- Tambahkan response standar saat 429 dan header observability (`Retry-After` bila relevan).
- Siapkan abstraction config (opsi future Redis) di file options baru.

**Output**

- Konfigurasi limiter aktif di runtime.
- Dokumen tuning limit awal: [D:/Work/Payment-gateaway-asp-react/.docs/security/rate-limit-tuning.md](D:/Work/Payment-gateaway-asp-react/.docs/security/rate-limit-tuning.md)

**Gate test phase**

- Positif: request normal tetap lolos.
- Negatif: burst traffic ke endpoint target menghasilkan 429 sesuai policy.
- Smoke test endpoint auth/snap/webhook tidak regression.

---

### Phase 2 — Turnstile/Captcha Enforcement untuk Endpoint Sensitif

**Tujuan**: menekan bot abuse pada endpoint anonymous bernilai tinggi.

**Aktivitas**

- Integrasikan verifikasi Cloudflare Turnstile server-side (service + typed options).
- Tambah header/body token captcha pada endpoint:
  - `/api/auth/login`
  - `/api/auth/refresh`
  - `/api/snap/token`
  - `/api/snap/status/{orderId}`
  - `/api/snap/cancel/{orderId}`
- Buat mekanisme bypass terkontrol untuk environment dev/test (feature flag + strict default off di prod).
- Frontend:
  - Tambahkan widget/token handling di [Page_Login.tsx](D:/Work/Payment-gateaway-asp-react/paymentgateway.client/app/routes/auth/Page_Login.tsx).
  - Extend request DTO di [auth.types.ts](D:/Work/Payment-gateaway-asp-react/paymentgateway.client/app/services/auth/types/auth.types.ts).
  - Kirim token ke API di [auth.api.ts](D:/Work/Payment-gateaway-asp-react/paymentgateway.client/app/services/auth/utils/auth.api.ts).
- Dokumentasikan variabel env baru frontend/backend.

**Output**

- Captcha enforcement aktif pada endpoint target.
- Dokumentasi setup Turnstile: [D:/Work/Payment-gateaway-asp-react/.docs/security/turnstile-setup.md](D:/Work/Payment-gateaway-asp-react/.docs/security/turnstile-setup.md)

**Gate test phase**

- Token valid -> request diproses.
- Token invalid/missing/expired -> request ditolak (401/400 sesuai kontrak).
- UI login tetap usable dan menampilkan error yang jelas.

---

### Phase 3 — Webhook Validation Hardening (Beyond Signature)

**Tujuan**: memperkuat keandalan dan anti-abuse alur webhook Midtrans.

**Aktivitas**

- Pertahankan signature verification yang sudah ada di [WebhookController.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Controllers/WebhookController.cs) dan [MidtransSignatureHelper.cs](D:/Work/Payment-gateaway-asp-react/PaymentGateway.Server/Midtrans/Utils/MidtransSignatureHelper.cs).
- Tambahkan **idempotency guard** webhook (dedupe key: `transaction_id + transaction_status + order_id`).
- Tambahkan **anti-replay window** berbasis `transaction_time` (toleransi configurable, fallback aman jika field tidak tersedia).
- Validasi payload minimum wajib (order_id/status_code/gross_amount/signature_key/transaction_status).
- Perketat forwarding safety:
  - URL allowlist/deny private network check lebih kuat.
  - timeout + retry policy terbatas untuk forwarder.
- Tambah structured audit logging untuk event webhook invalid, duplicate, replay-suspect.

**Output**

- Webhook processor idempotent + replay-aware.
- Dokumen hardening webhook: [D:/Work/Payment-gateaway-asp-react/.docs/security/webhook-hardening.md](D:/Work/Payment-gateaway-asp-react/.docs/security/webhook-hardening.md)

**Gate test phase**

- Signature valid diproses sekali (duplicate tidak mengubah state berulang).
- Signature invalid ditolak.
- Replay payload lama ditandai/ditolak sesuai policy.
- Forwarding gagal tidak mengganggu ack ke Midtrans.

---

### Phase 4 — Security Regression Test Suite & Verification

**Tujuan**: memastikan perubahan aman dipelihara dan tidak regress.

**Aktivitas**

- Tambah project test backend (jika belum ada) untuk:
  - rate-limit behavior per route,
  - captcha validator service,
  - webhook signature/idempotency/replay cases.
- Tambah minimal test frontend login terkait captcha submit/error states.
- Buat skrip smoke test endpoint publik (manual + otomatis ringan).
- Tambah checklist verifikasi release security.

**Output**

- Dokumen test plan + hasil: [D:/Work/Payment-gateaway-asp-react/.docs/security/security-test-report.md](D:/Work/Payment-gateaway-asp-react/.docs/security/security-test-report.md)

**Gate test phase**

- Seluruh test baru lulus.
- Tidak ada linter error baru pada file yang diubah.
- Endpoint kritikal tetap backward-compatible sesuai kontrak yang disepakati.

---

### Phase 5 — Operasionalisasi & Continuous Improvement

**Tujuan**: mengubah hardening menjadi proses berkelanjutan.

**Aktivitas**

- Observability dashboard metrik security (429 rate, captcha fail rate, webhook invalid rate).
- Threshold alerting awal untuk anomali.
- Jalur migrasi ke Redis untuk limiter saat traffic bertumbuh.
- Review kebijakan tiap 2 minggu berdasarkan data produksi.

**Output**

- Runbook operasional: [D:/Work/Payment-gateaway-asp-react/.docs/security/security-operations-runbook.md](D:/Work/Payment-gateaway-asp-react/.docs/security/security-operations-runbook.md)

## Daftar Improvement (Prioritas)

- **P0**: Rate limiting endpoint publik/anonymous.
- **P0**: Turnstile pada login + endpoint anonymous sensitif.
- **P0**: Webhook idempotency + replay protection + payload validation.
- **P1**: Forwarder webhook hardening (URL/network validation, timeout/retry terkendali).
- **P1**: Security regression tests terotomasi.
- **P2**: Migrasi limiter ke Redis/distributed store.
- **P2**: Monitoring & alerting security metrics.

## Rencana Eksekusi Bertahap

- Kita kerjakan **1 phase per cycle**.
- Setelah tiap phase selesai: update `status.md` + lampirkan hasil test phase.
- Baru lanjut ke phase berikutnya setelah gate test phase saat ini lulus.

## Definisi Selesai (Done Criteria)

- Semua phase status `Done` di dokumen status.
- Semua improvement P0/P1 terimplementasi dan teruji.
- Laporan test dan runbook operasional tersedia di `.docs/security`.
- Tidak ada regression fungsional pada alur auth, snap token, status/cancel, dan webhook.

