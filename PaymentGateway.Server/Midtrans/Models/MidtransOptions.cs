namespace PaymentGateway.Server.Midtrans.Models
{
    public class MidtransOptions
    {
        /// <summary>Public base URL of this gateway (e.g. https://payment-gateway.example.com). Used to build X-Override-Notification URLs sent to Midtrans.</summary>
        public string BaseUrl { get; set; } = string.Empty;
        public MidtransEnvironmentOptions Sandbox { get; set; } = new();
        public MidtransEnvironmentOptions Production { get; set; } = new();
    }

    public class MidtransEnvironmentOptions
    {
        public string ServerKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}
