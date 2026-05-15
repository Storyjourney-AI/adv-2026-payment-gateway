namespace PaymentGateway.Server.Common.Models
{
    public class PaginationWrapper<T>
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public List<T>? Items { get; set; }
    }
}
