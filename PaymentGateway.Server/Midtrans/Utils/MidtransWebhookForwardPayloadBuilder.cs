using PaymentGateway.Server.Midtrans.Models.Dtos;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PaymentGateway.Server.Midtrans.Utils
{
    internal static class MidtransWebhookForwardPayloadBuilder
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        public static string Build(string rawBody, Dto_SnapFeeBreakdown? gatewayFeeBreakdown)
        {
            var payloadNode = JsonNode.Parse(rawBody)
                ?? throw new JsonException("Webhook payload is null.");

            if (payloadNode is not JsonObject payloadObject)
            {
                throw new JsonException("Webhook payload must be a JSON object.");
            }

            payloadObject["gateway_fee_breakdown"] = JsonSerializer.SerializeToNode(
                gatewayFeeBreakdown,
                s_jsonOptions);

            return payloadObject.ToJsonString(s_jsonOptions);
        }
    }
}