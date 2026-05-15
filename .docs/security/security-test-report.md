# Security Regression Test Report (Phase 4)

Tanggal: 2026-03-17  
Scope: Verifikasi regresi untuk komponen security Phase 1-3.

## 1) Backend Automated Tests

Project test baru:

- `PaymentGateway.Server.Tests` (xUnit, net8.0)

Test cases yang ditambahkan:

- `MidtransSignatureHelperTests`
  - valid signature => pass
  - invalid signature => pass
- `RateLimitKeyBuilderTests`
  - key builder tanpa API key => pass
  - key builder dengan API key hash (tanpa expose raw key) => pass
- `WebhookReplayGuardTests`
  - first acquire true, duplicate false => pass
- `TurnstileValidationServiceTests`
  - feature disabled => pass
  - header missing => pass
  - dev bypass token valid => pass
  - provider reject token => pass

Perintah eksekusi:

```bash
dotnet test PaymentGateway.Server.Tests/PaymentGateway.Server.Tests.csproj
```

Hasil:

- Passed: 9
- Failed: 0
- Skipped: 0

Setelah Phase 5 (penambahan metrics service), suite diulang:

- Passed: 10
- Failed: 0
- Skipped: 0

## 2) Frontend Regression Verification

Perintah:

```bash
npm run typecheck --prefix paymentgateway.client
```

Hasil:

- Typecheck berhasil tanpa error.

## 3) Security Smoke Tests (Runtime)

Script smoke test ditambahkan:

- `scripts/security/security-smoke-tests.ps1`

Perintah:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/security/security-smoke-tests.ps1 -BaseUrl http://localhost:5562
```

Hasil eksekusi:

- `login_no_captcha_status=401` (sesuai ekspektasi)
- `login_with_bypass_status=401` (flow auth lanjut, kredensial invalid)
- `webhook_missing_field_status=400` (sesuai ekspektasi)

## 4) Ringkasan Verdict

- Suite regresi security Phase 4 berhasil diimplementasikan.
- Test otomatis backend berjalan hijau.
- Verifikasi frontend dan smoke test runtime menunjukkan proteksi utama tetap aktif.
- Setelah perubahan operasional Phase 5, regresi test tetap hijau.
