using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Server.Authorization.Models.Dtos
{
    /// <summary>
    /// DTO for role permissions response
    /// </summary>
    public class Dto_RolePermissionsResponse
    {
        public string RoleName { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
    }

    /// <summary>
    /// DTO for assigning permission to role
    /// </summary>
    public class Dto_AssignPermissionToRoleRequest
    {
        [Required(ErrorMessage = "Role name is required")]
        public string RoleName { get; set; }

        [Required(ErrorMessage = "Permission is required")]
        public string Permission { get; set; }
    }

    /// <summary>
    /// DTO for removing permission from role
    /// </summary>
    public class Dto_RemovePermissionFromRoleRequest
    {
        [Required(ErrorMessage = "Role name is required")]
        public string RoleName { get; set; }

        [Required(ErrorMessage = "Permission is required")]
        public string Permission { get; set; }
    }

    /// <summary>
    /// DTO for updating role permissions (batch operation)
    /// </summary>
    public class Dto_UpdateRolePermissionsRequest
    {
        [Required(ErrorMessage = "Permissions list is required")]
        public List<string> Permissions { get; set; } = new List<string>();
    }
}
