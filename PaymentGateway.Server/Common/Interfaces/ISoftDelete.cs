namespace PaymentGateway.Server.Common.Interfaces
{
    public interface ISoftDelete
    {
        public DateTime? DeletedAt { get; set; }
    }
}
