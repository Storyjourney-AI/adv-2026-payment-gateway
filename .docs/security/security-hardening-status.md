# Security Hardening Status Tracker

Last update: 2026-03-17

## Phase Status Overview

| Phase | Nama | Status | Mulai | Selesai | Catatan |
|---|---|---|---|---|---|
| Phase 0 | Baseline Security Audit & Threat Mapping | Done | 2026-03-17 | 2026-03-17 | Baseline doc selesai dan disetujui untuk lanjut Phase 1 |
| Phase 1 | Global Rate Limiting Foundation | Done | 2026-03-17 | 2026-03-17 | Policy limiter aktif + build sukses + burst test menunjukkan 429 |
| Phase 2 | Turnstile/Captcha Enforcement | Done | 2026-03-17 | 2026-03-17 | Verifikasi Turnstile backend+frontend selesai, typecheck lulus, build backend output terpisah sukses |
| Phase 3 | Webhook Validation Hardening | Done | 2026-03-17 | 2026-03-17 | Payload validation + anti-replay + idempotency + forwarding safety hardening aktif |
| Phase 4 | Security Regression Test Suite | Done | 2026-03-17 | 2026-03-17 | Backend automated tests + frontend typecheck + smoke tests selesai |
| Phase 5 | Operasionalisasi & Continuous Improvement | Done | 2026-03-17 | 2026-03-17 | Metrics service + endpoint observability + runbook + script operasional selesai |

## Detail Per Phase

### Phase 0 — Baseline Security Audit & Threat Mapping

- Status: Done
- Owner: AI Pair (Cursor)
- Tanggal mulai: 2026-03-17
- Tanggal selesai: 2026-03-17

Checklist implementasi:
- [x] Inventaris endpoint publik/anonymous.
- [x] Threat mapping baseline.
- [x] Endpoint protection matrix.
- [x] Prioritas improvement P0/P1/P2.

Checklist test + evidence:
- [x] Validasi endpoint list dari controller auth/snap/webhook.
- [x] Verifikasi gap kontrol: rate limit, captcha, idempotency/replay webhook.
- [x] Evidence dokumen: `.docs/security/security-audit-baseline.md`.

Risk/rollback notes:
- Tidak ada perubahan runtime/code path pada aplikasi (dokumentasi saja), sehingga tidak memerlukan rollback teknis.

### Phase 1 — Global Rate Limiting Foundation

- Status: Done
- Owner: AI Pair (Cursor)
- Tanggal mulai: 2026-03-17
- Tanggal selesai: 2026-03-17

Checklist implementasi:
- [x] AddRateLimiter + UseRateLimiter.
- [x] Policy per endpoint group.
- [x] 429 response handling.

Checklist test + evidence:
- [x] Backend compile check: `dotnet build PaymentGateway.Server/PaymentGateway.Server.csproj` (success).
- [x] Uji burst login parallel: hasil mengandung banyak status `429` (throttling aktif).
- [x] Uji request normal endpoint login tetap diproses (status `401` untuk kredensial invalid, bukan error middleware).

Risk/rollback notes:
- Potensi false positive throttling masih ada; threshold dapat dituning di `RateLimitSettings`.

### Phase 2 — Turnstile/Captcha Enforcement

- Status: Done
- Owner: AI Pair (Cursor)
- Tanggal mulai: 2026-03-17
- Tanggal selesai: 2026-03-17

Checklist implementasi:
- [x] Integrasi Turnstile validator service di backend (`TurnstileOptions`, service, DI, http client).
- [x] Enforcement captcha pada endpoint target: `auth/login`, `auth/refresh`, `snap/token`, `snap/status`, `snap/cancel` (+ endpoint token deprecated).
- [x] Integrasi frontend login/refresh untuk mengirim header captcha token.
- [x] Dokumentasi setup Turnstile ditambahkan (`.docs/security/turnstile-setup.md`).

Checklist test + evidence:
- [x] Frontend typecheck: `npm run typecheck --prefix paymentgateway.client` (success).
- [x] Backend compile check (fallback): `dotnet build PaymentGateway.Server/PaymentGateway.Server.csproj -o artifacts/build-check/phase-2` (success).
- [x] Backend compile check (default output): `dotnet build PaymentGateway.Server/PaymentGateway.Server.csproj` (success) setelah lock process dibersihkan.
- [x] Linter check pada file yang diubah (no lint errors).
- [x] Runtime captcha check:
  - tanpa header `X-Turnstile-Token` pada `POST /api/auth/login` => `401` dengan pesan header required
  - dengan bypass token dev pada `POST /api/auth/login` => lanjut ke auth flow (`401 Invalid email or password`)
  - tanpa header pada `POST /api/auth/refresh` => `401` header required
  - dengan bypass token dev pada `POST /api/auth/refresh` => lanjut ke auth flow (`401 Refresh token is missing or invalid`)

Risk/rollback notes:
- Build default sebelumnya sempat terhambat lock file oleh proses `PaymentGateway.Server.exe`; sudah diselesaikan dengan menghentikan proses lock holder dan re-run build default berhasil.

### Phase 3 — Webhook Validation Hardening

- Status: Done
- Owner: AI Pair (Cursor)
- Tanggal mulai: 2026-03-17
- Tanggal selesai: 2026-03-17

Checklist implementasi:
- [x] Payload minimum validation pada webhook (`order_id`, `status_code`, `gross_amount`, `signature_key`, `transaction_status`, `transaction_id`).
- [x] Anti-replay window berbasis `transaction_time`.
- [x] Idempotency guard dedupe (`order_id + transaction_id + transaction_status`) via in-memory cache.
- [x] Forwarding safety hardening: validasi host public-routable (no loopback/private/reserved IP) termasuk DNS resolution.
- [x] Retry terbatas untuk forwarding webhook dengan delay configurable.
- [x] Dokumentasi hardening webhook ditambahkan (`.docs/security/webhook-hardening.md`).

Checklist test + evidence:
- [x] Backend compile check default output: `dotnet build PaymentGateway.Server/PaymentGateway.Server.csproj` (success).
- [x] Linter check file phase 3 (no lint errors).
- [x] Runtime webhook check:
  - payload missing field => `400`
  - invalid signature => `400`
  - valid signature (order unknown) => `200` acknowledged
  - duplicate valid payload => acknowledged sebagai duplicate (tercatat di log sebagai duplicate webhook)

Risk/rollback notes:
- Dedupe saat ini masih in-memory per instance; untuk multi-instance production, disarankan migrasi ke distributed cache (Redis) pada phase lanjut.

### Phase 4 — Security Regression Test Suite

- Status: Done
- Owner: AI Pair (Cursor)
- Tanggal mulai: 2026-03-17
- Tanggal selesai: 2026-03-17

Checklist implementasi:
- [x] Menambahkan project test backend: `PaymentGateway.Server.Tests`.
- [x] Menambahkan unit tests untuk komponen security utama (signature, rate-limit key builder, replay guard, turnstile validator).
- [x] Menambahkan smoke test script endpoint security: `scripts/security/security-smoke-tests.ps1`.
- [x] Menambahkan laporan test: `.docs/security/security-test-report.md`.

Checklist test + evidence:
- [x] `dotnet test PaymentGateway.Server.Tests/PaymentGateway.Server.Tests.csproj` => Passed 9, Failed 0.
- [x] `npm run typecheck --prefix paymentgateway.client` => success.
- [x] Smoke test runtime (`security-smoke-tests.ps1`) => hasil sesuai ekspektasi (`401/400`).
- [x] Linter check file phase 4 => no lint errors.

Risk/rollback notes:
- Suite frontend saat ini masih fokus pada type-level regression + smoke script; UI automation end-to-end dapat ditambahkan pada phase berikutnya bila dibutuhkan coverage lebih dalam.

### Phase 5 — Operasionalisasi & Continuous Improvement

- Status: Done
- Owner: AI Pair (Cursor)
- Tanggal mulai: 2026-03-17
- Tanggal selesai: 2026-03-17

Checklist implementasi:
- [x] Menambahkan security metrics service in-memory (`ISecurityMetricsService`, `SecurityMetricsService`).
- [x] Menambahkan endpoint monitoring `GET /api/security/metrics` (khusus `RequireSuperAdmin`).
- [x] Menambahkan pencatatan metrik di jalur kritikal:
  - rate limit reject
  - captcha validation fail
  - invalid API key snap
  - webhook invalid signature
  - webhook duplicate
  - webhook replay suspected
- [x] Menambahkan runbook operasional: `.docs/security/security-operations-runbook.md`.
- [x] Menambahkan script utilitas operasional: `scripts/security/fetch-security-metrics.ps1`.

Checklist test + evidence:
- [x] Backend build: `dotnet build PaymentGateway.Server/PaymentGateway.Server.csproj` (success).
- [x] Backend tests: `dotnet test PaymentGateway.Server.Tests/PaymentGateway.Server.Tests.csproj` (Passed 10, Failed 0).
- [x] Linter check file phase 5 => no lint errors.
- [x] Laporan test diperbarui: `.docs/security/security-test-report.md`.

Risk/rollback notes:
- Metrics service saat ini in-memory per instance; untuk deployment horizontal perlu migrasi ke backend metrics/distributed store agar agregasi lintas instance akurat.
