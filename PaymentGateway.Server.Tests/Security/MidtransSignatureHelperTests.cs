using PaymentGateway.Server.Midtrans.Utils;
using System.Security.Cryptography;
using System.Text;

namespace PaymentGateway.Server.Tests.Security
{
    public class MidtransSignatureHelperTests
    {
        [Fact]
        public void Verify_ReturnsTrue_ForValidSignature()
        {
            const string orderId = "order-123";
            const string statusCode = "200";
            const string grossAmount = "10000.00";
            const string serverKey = "server-key-sample";

            var raw = orderId + statusCode + grossAmount + serverKey;
            var signature = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

            var isValid = MidtransSignatureHelper.Verify(orderId, statusCode, grossAmount, signature, serverKey);

            Assert.True(isValid);
        }

        [Fact]
        public void Verify_ReturnsFalse_ForInvalidSignature()
        {
            var isValid = MidtransSignatureHelper.Verify(
                "order-123",
                "200",
                "10000.00",
                "invalid-signature",
                "server-key-sample");

            Assert.False(isValid);
        }
    }
}
