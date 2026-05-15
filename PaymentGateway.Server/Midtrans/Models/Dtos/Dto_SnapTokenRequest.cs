using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Midtrans.Models.Dtos
{
    public class Dto_SnapTokenRequest
    {
        [Required]
        [MaxLength(42, ErrorMessage = "OrderId must not exceed 42 characters.")]
        public string OrderId { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "GrossAmount must be greater than 0.")]
        public int GrossAmount { get; set; }

        public SnapCustomerDetails? CustomerDetails { get; set; }

        public List<SnapItemDetail>? ItemDetails { get; set; }
    }

    public class SnapCustomerDetails
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    public class SnapItemDetail
    {
        public string? Id { get; set; }
        public int Price { get; set; }
        public int Quantity { get; set; }
        public string? Name { get; set; }
    }
}
