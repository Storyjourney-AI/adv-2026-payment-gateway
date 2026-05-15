using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    /// <summary>
    /// DTO for user in role response (minimal data)
    /// </summary>
    public class Dto_UserInRoleResponse
    {
        public string UserId { get; set; }
        public string Email { get; set; }
    }

    /// <summary>
    /// DTO for paginated users in role response
    /// </summary>
    public class Dto_GetUsersByRoleResponse
    {
        public string RoleName { get; set; }
        public List<Dto_UserInRoleResponse> Users { get; set; } = new List<Dto_UserInRoleResponse>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    }

    /// <summary>
    /// DTO for get users by role request (with pagination)
    /// </summary>
    public class Dto_GetUsersByRoleRequest
    {
        [Required(ErrorMessage = "Role name is required")]
        [StringLength(256, ErrorMessage = "Role name cannot exceed 256 characters")]
        public string RoleName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize { get; set; } = 20;
    }
}
