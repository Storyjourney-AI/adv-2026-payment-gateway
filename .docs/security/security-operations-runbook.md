# Security Operations Runbook (Phase 5)

Tanggal: 2026-03-17

Dokumen ini menjadi panduan operasional security setelah hardening phase 1-4.

## 1) Sumber Metrics Security

Metrics dikumpulkan in-memory melalui `ISecurityMetricsService` dan dapat diakses lewat endpoint:

- `GET /api/security/metrics` (policy: `RequireSuperAdmin`)

Format data snapshot:

- `metricName`
- `dimension`
- `count`

## 2) Metrics Inti yang Dipantau

- `rate_limit_reject_total` (dimension: path)
- `captcha_validation_fail_total` (dimension: path)
- `x_api_key_invalid_total` (dimension: snap endpoint)
- `webhook_invalid_signature_total` (dimension: env)
- `webhook_duplicate_total` (dimension: env)
- `webhook_replay_suspected_total` (dimension: env)

## 3) Alert Threshold Awal (Baseline)

Gunakan threshold awal berikut (per 5 menit) sebagai alarm awal:

- `rate_limit_reject_total` > 100 pada endpoint auth/snap => indikasi abuse burst.
- `captcha_validation_fail_total` > 50 pada `/api/auth/login` => indikasi bot login.
- `x_api_key_invalid_total` > 30 => kemungkinan API key probing.
- `webhook_invalid_signature_total` > 10 => kemungkinan spoofed webhook attempts.
- `webhook_replay_suspected_total` > 5 => indikasi replay attack/faulty sender.

## 4) Prosedur Triage Insiden

1. Verifikasi metrik melonjak pada endpoint/dimension tertentu.
2. Cek log aplikasi untuk request pattern (IP, route, timestamp).
3. Jika dominan dari sumber tertentu:
   - perketat rate limit policy terkait,
   - aktifkan blok WAF/IP sementara,
   - audit API key yang terdampak (jika `x_api_key_invalid_total` naik).
4. Untuk webhook:
   - cek validitas signature source,
   - verifikasi `transaction_time` dan dedupe log.
5. Catat postmortem singkat dan tuning threshold jika perlu.

## 5) Tuning Cepat yang Aman

- Naikkan/turunkan permit pada `RateLimitSettings` per policy.
- Ubah replay/dedupe window pada `WebhookHardeningOptions`.
- Perbarui bypass captcha development hanya untuk environment non-production.

## 6) Roadmap Migrasi ke Redis (Next Step)

Saat traffic meningkat/multi-instance:

1. Migrasikan `IWebhookReplayGuard` ke Redis-based key lock dengan TTL.
2. Migrasikan `ISecurityMetricsService` counter ke Redis/instrumentation backend.
3. Hubungkan endpoint metrics ke observability stack (Prometheus/Grafana/ELK sesuai infrastruktur).
4. Pertahankan contract metric name agar dashboard existing tidak rusak.

## 7) Checklist Operasional Release

- [ ] Build backend + test suite lulus.
- [ ] Endpoint `/api/security/metrics` dapat diakses oleh Super Admin.
- [ ] Alert threshold awal dikonfigurasi di tooling monitoring.
- [ ] Rollback plan tersimpan (revert config rate/captcha/webhook options).
