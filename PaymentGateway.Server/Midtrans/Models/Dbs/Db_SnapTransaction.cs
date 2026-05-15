using PaymentGateway.Server.Applications.Models.Dbs;

namespace PaymentGateway.Server.Midtrans.Models.Dbs
{
    public class Db_SnapTransaction
    {
        public Guid Id { get; set; }
        public Guid EnvironmentId { get; set; }

        /// <summary>Full order_id sent to Midtrans: "{envId[0..7]}_{callerOrderId}"</summary>
        public string MidtransOrderId { get; set; } = string.Empty;

        /// <summary>The raw OrderId provided by the child app.</summary>
        public string CallerOrderId { get; set; } = string.Empty;

        public int GrossAmount { get; set; }

        /// <summary>"sandbox" or "production"</summary>
        public string MidtransEnv { get; set; } = string.Empty;

        public string? TransactionStatus { get; set; }
        public string? MidtransTransactionId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public Db_Environment? Environment { get; set; }
    }
}
