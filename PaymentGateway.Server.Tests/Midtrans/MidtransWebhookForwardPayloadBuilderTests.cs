using PaymentGateway.Server.Midtrans.Models.Dtos;
using PaymentGateway.Server.Midtrans.Utils;
using System.Text.Json;

namespace PaymentGateway.Server.Tests.Midtrans
{
    public class MidtransWebhookForwardPayloadBuilderTests
    {
        [Fact]
        public void Build_PreservesOriginalMidtransFields_AndAppendsGatewayFeeBreakdown()
        {
            const string rawBody = """
            {
              "order_id": "order-123",
              "gross_amount": "10000.00",
              "transaction_status": "pending",
              "metadata": {
                "source": "midtrans"
              }
            }
            """;

            var feeBreakdown = new Dto_SnapFeeBreakdown
            {
                FinalGrossAmount = 10300.00m,
                OriginalAmount = 10000.00m,
                CustomerPaymentFee = 300.00m,
                FeePercentage = 3.00m
            };

            var payload = MidtransWebhookForwardPayloadBuilder.Build(rawBody, feeBreakdown);

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            Assert.Equal("order-123", root.GetProperty("order_id").GetString());
            Assert.Equal("10000.00", root.GetProperty("gross_amount").GetString());
            Assert.Equal("pending", root.GetProperty("transaction_status").GetString());
            Assert.Equal("midtrans", root.GetProperty("metadata").GetProperty("source").GetString());

            var gatewayFeeBreakdown = root.GetProperty("gateway_fee_breakdown");
            Assert.Equal(10300.00m, gatewayFeeBreakdown.GetProperty("final_gross_amount").GetDecimal());
            Assert.Equal(10000.00m, gatewayFeeBreakdown.GetProperty("original_amount").GetDecimal());
            Assert.Equal(300.00m, gatewayFeeBreakdown.GetProperty("customer_payment_fee").GetDecimal());
            Assert.Equal(3.00m, gatewayFeeBreakdown.GetProperty("fee_percentage").GetDecimal());
        }

        [Fact]
        public void Build_WritesNullGatewayFeeBreakdown_WhenFeeBreakdownIsUnavailable()
        {
            const string rawBody = """
            {
              "order_id": "order-123",
              "gross_amount": "10000.00"
            }
            """;

            var payload = MidtransWebhookForwardPayloadBuilder.Build(rawBody, gatewayFeeBreakdown: null);

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("gateway_fee_breakdown", out var gatewayFeeBreakdown));
            Assert.Equal(JsonValueKind.Null, gatewayFeeBreakdown.ValueKind);
            Assert.Equal("10000.00", root.GetProperty("gross_amount").GetString());
        }
    }
}