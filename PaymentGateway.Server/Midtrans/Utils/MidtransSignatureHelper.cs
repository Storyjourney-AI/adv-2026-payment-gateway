using System.Security.Cryptography;
using System.Text;

namespace PaymentGateway.Server.Midtrans.Utils
{
    public static class MidtransSignatureHelper
    {
        /// <summary>
        /// Verifies the Midtrans webhook signature_key.
        /// SHA512(orderId + statusCode + grossAmount + serverKey) must equal receivedSignature.
        /// </summary>
        public static bool Verify(
            string orderId,
            string statusCode,
            string grossAmount,
            string receivedSignature,
            string serverKey)
        {
            var raw = orderId + statusCode + grossAmount + serverKey;
            var bytes = SHA512.HashData(Encoding.UTF8.GetBytes(raw));
            var computed = Convert.ToHexString(bytes).ToLowerInvariant();
            return string.Equals(computed, receivedSignature, StringComparison.OrdinalIgnoreCase);
        }
    }
}
