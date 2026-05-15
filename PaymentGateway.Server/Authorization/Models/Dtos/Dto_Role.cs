using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    /// <summary>
    /// DTO for creating a new role
    /// </summary>
    public class Dto_CreateRoleRequest
    {
        [Required(ErrorMessage = "Role name is required")]
        [StringLength(256, ErrorMessage = "Role name cannot exceed 256 characters")]
        public string Name { get; set; }
    }

    /// <summary>
    /// DTO for role response
    /// </summary>
    public class Dto_RoleResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int UserCount { get; set; }
    }

    /// <summary>
    /// DTO for adding user to role
    /// </summary>
    public class Dto_AddUserToRoleRequest
    {
        [Required(ErrorMessage = "User ID is required")]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "Role name is required")]
        [StringLength(256, ErrorMessage = "Role name cannot exceed 256 characters")]
        public string RoleName { get; set; }
    }

    /// <summary>
    /// DTO for removing user from role
    /// </summary>
    public class Dto_RemoveUserFromRoleRequest
    {
        [Required(ErrorMessage = "User ID is required")]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "Role name is required")]
        [StringLength(256, ErrorMessage = "Role name cannot exceed 256 characters")]
        public string RoleName { get; set; }
    }

    // ============================================
    // Role List Item DTO (for management)
    // ============================================
    public class Dto_RoleListItem
    {
        public string RoleId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UserCount { get; set; }
    }

    // ============================================
    // Role Detail DTO (for management)
    // ============================================
    public class Dto_RoleDetail
    {
        public string RoleId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Claims { get; set; } = new List<string>();
        public int UserCount { get; set; }
    }

    // ============================================
    // Role Update Request DTO
    // ============================================
    public class Dto_RoleUpdateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // ============================================
    // Assign Claims Request DTO
    // ============================================
    public class Dto_AssignClaimsRequest
    {
        public List<string> Claims { get; set; } = new List<string>();
    }

    /// <summary>
    /// DTO for user roles response
    /// </summary>
    public class Dto_UserRolesResponse
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    /// <summary>
    /// DTO for listing all roles
    /// </summary>
    public class Dto_RolesListResponse
    {
        public List<Dto_RoleResponse> Roles { get; set; } = new List<Dto_RoleResponse>();
        public int TotalCount { get; set; }
    }

    // ============================================
    // Role Create Request DTO (Technical Plan)
    // ============================================
    public class Dto_RoleCreateRequest
    {
        [Required(ErrorMessage = "Role name is required")]
        [StringLength(256, ErrorMessage = "Role name cannot exceed 256 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }
    }
}
