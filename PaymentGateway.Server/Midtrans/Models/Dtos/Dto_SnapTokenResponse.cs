namespace PaymentGateway.Server.Midtrans.Models.Dtos
{
    public class Dto_SnapTokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
    }
}
