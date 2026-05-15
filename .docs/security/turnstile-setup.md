# Turnstile Setup (Phase 2)

Dokumen ini menjelaskan konfigurasi dan perilaku implementasi Turnstile/Captcha pada Phase 2.

## Endpoint yang Diproteksi

Backend memverifikasi token Turnstile untuk endpoint berikut:

- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/snap/token`
- `POST /api/snap/sandbox/token` (deprecated)
- `POST /api/snap/production/token` (deprecated)
- `GET /api/snap/status/{orderId}`
- `POST /api/snap/cancel/{orderId}`

## Header Captcha

- Nama header default: `X-Turnstile-Token`
- Semua endpoint di atas akan menolak request (`401`) bila token tidak ada/tidak valid.

## Konfigurasi Backend

Section konfigurasi: `Turnstile`

Contoh:

```json
{
  "Turnstile": {
    "IsEnabled": true,
    "SecretKey": "your-turnstile-secret",
    "VerificationUrl": "https://challenges.cloudflare.com/turnstile/v0/siteverify",
    "HeaderName": "X-Turnstile-Token",
    "AllowBypassInDevelopment": true,
    "DevelopmentBypassToken": "dev-turnstile-bypass"
  }
}
```

### Catatan bypass development

- Bila `AllowBypassInDevelopment = true`, backend menerima token bypass pada environment Development.
- Token bypass harus sama dengan `DevelopmentBypassToken`.

## Konfigurasi Frontend

File: `paymentgateway.client/.env`

```env
VITE_API_BASE_URL=http://localhost:5550
VITE_TURNSTILE_SITE_KEY=your-turnstile-site-key
VITE_TURNSTILE_DEV_BYPASS_TOKEN=dev-turnstile-bypass
```

Perilaku frontend:

- Jika `VITE_TURNSTILE_SITE_KEY` terisi, frontend mengambil token Turnstile invisible sebelum memanggil:
  - login
  - refresh
- Jika site key kosong dan `VITE_TURNSTILE_DEV_BYPASS_TOKEN` terisi, frontend mengirim token bypass.

## Operasional

- Production: aktifkan `IsEnabled=true`, isi `SecretKey`, isi frontend `VITE_TURNSTILE_SITE_KEY`, dan nonaktifkan bypass development.
- Development/Test: boleh gunakan bypass token untuk mempercepat debugging.
