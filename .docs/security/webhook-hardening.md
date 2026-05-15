# Webhook Hardening (Phase 3)

Phase ini memperkuat keamanan endpoint webhook Midtrans di `WebhookController`.

## Proteksi yang Ditambahkan

- **Minimum payload validation**
  - Field wajib: `order_id`, `status_code`, `gross_amount`, `signature_key`, `transaction_status`, `transaction_id`.
  - Jika ada yang kosong/missing, request ditolak (`400`).

- **Signature verification (tetap)**
  - Tetap menggunakan `MidtransSignatureHelper.Verify(...)`.
  - Signature invalid ditolak (`400`).

- **Anti-replay window**
  - Memvalidasi `transaction_time` (format `yyyy-MM-dd HH:mm:ss`, diasumsikan WIB UTC+7).
  - Ditolak jika terlalu lama atau terlalu jauh ke masa depan dibanding window konfigurasi.
  - Opsional reject saat `transaction_time` tidak ada (`RejectWhenTransactionTimeMissing`).

- **Idempotency / duplicate guard**
  - Dedupe key: `order_id + transaction_id + transaction_status`.
  - Duplicate request diakui (`200`) tapi tidak diproses ulang.
  - Penyimpanan guard saat ini memakai in-memory cache (TTL configurable).

- **Forwarding safety hardening**
  - URL wajib `https`.
  - Host tidak boleh loopback/private/reserved IP.
  - Untuk hostname, dilakukan DNS resolution dan semua resolved IP harus public-routable.
  - Tambahan retry terbatas untuk forwarding ke child webhook saat error transient.

## Konfigurasi Baru (Backend)

Tambahkan section `WebhookHardening`:

```json
{
  "WebhookHardening": {
    "ReplayWindowMinutes": 15,
    "DeduplicationWindowMinutes": 60,
    "RejectWhenTransactionTimeMissing": false,
    "ForwardRetryCount": 1,
    "ForwardRetryDelayMs": 300
  }
}
```

## Catatan Operasional

- Dedupe in-memory berlaku per instance aplikasi. Untuk multi-instance, pertimbangkan distributed cache (Redis).
- Mode default tetap mengakui webhook duplicate (`200`) agar Midtrans tidak terus retry.
