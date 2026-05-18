namespace PaymentGateway.Server.Midtrans.Models.Dtos
{
    public class Dto_SnapFeeBreakdown
    {
        public decimal? FinalGrossAmount { get; set; }
        public decimal? OriginalAmount { get; set; }
        public decimal? CustomerPaymentFee { get; set; }
        public decimal? FeePercentage { get; set; }
    }
}