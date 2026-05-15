namespace PaymentGateway.Server.Midtrans.Models.Dtos
{
    public class Dto_SnapStatusResponse
    {
        public string CallerOrderId { get; set; } = string.Empty;
        public string MidtransOrderId { get; set; } = string.Empty;
        public string? GatewayStatus { get; set; }
        public string? MidtransStatus { get; set; }
        public string? FraudStatus { get; set; }
        public string GrossAmount { get; set; } = string.Empty;
        public string? MidtransTransactionId { get; set; }
        public string? PaymentType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
