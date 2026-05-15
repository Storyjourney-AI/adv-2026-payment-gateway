using Microsoft.AspNetCore.Identity;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Authorization.Utils;
using PaymentGateway.Server.Common.Models;
using System.Security.Claims;

namespace PaymentGateway.Server.Authorization.Services
{
    /// <summary>
    /// Service for managing role permissions (claims)
    /// </summary>
    public class ClaimsService
    {
        private readonly RoleManager<Db_ApplicationRole> m_roleManager;
        private readonly ILogger<ClaimsService> m_logger;

        // Protected role name - cannot be modified
        private const string PROTECTED_ROLE_NAME = "Super Admin";

        public ClaimsService(
            RoleManager<Db_ApplicationRole> roleManager,
            ILogger<ClaimsService> logger)
        {
            m_roleManager = roleManager;
            m_logger = logger;
        }

        /// <summary>
        /// Check if role is protected (Super Admin)
        /// </summary>
        private bool IsProtectedRole(Db_ApplicationRole role)
        {
            return role.Name.Equals(PROTECTED_ROLE_NAME, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Assign a permission to a role
        /// </summary>
        public async Task<DataWrapper<bool>> AssignPermissionToRoleAsync(Db_ApplicationRole role, string permission)
        {
            try
            {
                // Protect Super Admin role
                if (IsProtectedRole(role))
                {
                    m_logger.LogWarning("AssignPermission - Attempted to modify protected role: {roleName}", role.Name);
                    return DataWrapper<bool>.BadRequest(message: $"Cannot modify permissions for '{role.Name}' role. This role is protected.");
                }

                m_logger.LogInformation("AssignPermission - Assigning permission {permission} to role {roleName}",
                    permission, role.Name);

                // Validate permission exists
                if (!AuthorizationConstants.Permissions.GetAll().Contains(permission))
                {
                    m_logger.LogWarning("AssignPermission - Permission not found: {permission}", permission);
                    return DataWrapper<bool>.BadRequest(message: $"Permission '{permission}' not found.");
                }

                // Check if permission already assigned
                var existingClaim = (await m_roleManager.GetClaimsAsync(role))
                    .FirstOrDefault(c => c.Type == AuthorizationConstants.PermissionClaimType && c.Value == permission);

                if (existingClaim != null)
                {
                    m_logger.LogInformation("AssignPermission - Role already has permission {permission}", permission);
                    return DataWrapper<bool>.BadRequest(message: $"Role already has permission '{permission}'.");
                }

                // Add permission claim
                var claim = new Claim(AuthorizationConstants.PermissionClaimType, permission);
                var result = await m_roleManager.AddClaimAsync(role, claim);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("AssignPermission - Failed to add permission claim: {errors}",
                        string.Join(", ", errors));
                    return DataWrapper<bool>.BadRequest(message: "Failed to assign permission.", errors: errors);
                }

                m_logger.LogInformation("AssignPermission - Successfully assigned permission {permission} to role {roleName}",
                    permission, role.Name);
                return DataWrapper<bool>.Succeed(true, message: $"Permission '{permission}' assigned to role successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "AssignPermission - Unexpected error assigning permission {permission} to role {roleName}",
                    permission, role.Name);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while assigning permission.");
            }
        }

        /// <summary>
        /// Remove a permission from a role
        /// </summary>
        public async Task<DataWrapper<bool>> RemovePermissionFromRoleAsync(Db_ApplicationRole role, string permission)
        {
            try
            {
                // Protect Super Admin role
                if (IsProtectedRole(role))
                {
                    m_logger.LogWarning("RemovePermission - Attempted to modify protected role: {roleName}", role.Name);
                    return DataWrapper<bool>.BadRequest(message: $"Cannot modify permissions for '{role.Name}' role. This role is protected.");
                }

                m_logger.LogInformation("RemovePermission - Removing permission {permission} from role {roleName}",
                    permission, role.Name);

                // Validate permission exists
                if (!AuthorizationConstants.Permissions.GetAll().Contains(permission))
                {
                    m_logger.LogWarning("RemovePermission - Permission not found: {permission}", permission);
                    return DataWrapper<bool>.BadRequest(message: $"Permission '{permission}' not found.");
                }

                // Check if role has this permission
                var allClaims = await m_roleManager.GetClaimsAsync(role);
                var permissionClaim = allClaims.FirstOrDefault(c => 
                    c.Type == AuthorizationConstants.PermissionClaimType && c.Value == permission);

                if (permissionClaim == null)
                {
                    m_logger.LogWarning("RemovePermission - Role does not have permission {permission}", permission);
                    return DataWrapper<bool>.BadRequest(message: $"Role does not have permission '{permission}'.");
                }

                // Remove permission claim
                var result = await m_roleManager.RemoveClaimAsync(role, permissionClaim);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("RemovePermission - Failed to remove permission claim: {errors}",
                        string.Join(", ", errors));
                    return DataWrapper<bool>.BadRequest(message: "Failed to remove permission.", errors: errors);
                }

                m_logger.LogInformation("RemovePermission - Successfully removed permission {permission} from role {roleName}",
                    permission, role.Name);
                return DataWrapper<bool>.Succeed(true, message: $"Permission '{permission}' removed from role successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "RemovePermission - Unexpected error removing permission {permission} from role {roleName}",
                    permission, role.Name);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while removing permission.");
            }
        }

        /// <summary>
        /// Get all permissions assigned to a specific role
        /// </summary>
        public async Task<DataWrapper<List<string>>> GetRolePermissionsAsync(Db_ApplicationRole role)
        {
            try
            {
                m_logger.LogInformation("GetRolePermissions - Fetching permissions for role {roleName}", role.Name);

                var claims = await m_roleManager.GetClaimsAsync(role);
                var permissions = claims
                    .Where(c => c.Type == AuthorizationConstants.PermissionClaimType)
                    .Select(c => c.Value)
                    .ToList();

                m_logger.LogInformation("GetRolePermissions - Retrieved {count} permissions for role {roleName}",
                    permissions.Count, role.Name);

                return DataWrapper<List<string>>.Succeed(permissions, message: "Permissions retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetRolePermissions - Unexpected error fetching permissions for role {roleName}",
                    role.Name);
                return DataWrapper<List<string>>.Fail_InternalError(message: "An error occurred while fetching permissions.");
            }
        }

        /// <summary>
        /// Get all available permissions organized by sections
        /// </summary>
        public DataWrapper<List<AuthorizationConstants.PermissionSection>> GetAllPermissions()
        {
            try
            {
                var sections = AuthorizationConstants.Permissions.GetAllSections();
                return DataWrapper<List<AuthorizationConstants.PermissionSection>>.Succeed(sections, 
                    message: "Permissions retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAllPermissions - Unexpected error");
                return DataWrapper<List<AuthorizationConstants.PermissionSection>>.Fail_InternalError(
                    message: "An error occurred while fetching permissions.");
            }
        }

        /// <summary>
        /// Get all available permissions as a flat list
        /// </summary>
        public DataWrapper<List<string>> GetAllPermissionsFlat()
        {
            try
            {
                var permissions = AuthorizationConstants.Permissions.GetAll();
                return DataWrapper<List<string>>.Succeed(permissions, 
                    message: "Permissions retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAllPermissionsFlat - Unexpected error");
                return DataWrapper<List<string>>.Fail_InternalError(
                    message: "An error occurred while fetching permissions.");
            }
        }

        /// <summary>
        /// Check if role has a specific permission
        /// </summary>
        public async Task<bool> RoleHasPermissionAsync(Db_ApplicationRole role, string permission)
        {
            try
            {
                var claims = await m_roleManager.GetClaimsAsync(role);
                return claims.Any(c => c.Type == AuthorizationConstants.PermissionClaimType && c.Value == permission);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "RoleHasPermission - Error checking permission {permission} for role {roleName}",
                    permission, role.Name);
                return false;
            }
        }

        /// <summary>
        /// Assign multiple permissions to a role
        /// </summary>
        public async Task<DataWrapper<bool>> AssignPermissionsToRoleAsync(Db_ApplicationRole role, List<string> permissions)
        {
            try
            {
                // Protect Super Admin role
                if (IsProtectedRole(role))
                {
                    m_logger.LogWarning("AssignPermissions - Attempted to modify protected role: {roleName}", role.Name);
                    return DataWrapper<bool>.BadRequest(message: $"Cannot modify permissions for '{role.Name}' role. This role is protected.");
                }

                m_logger.LogInformation("AssignPermissions - Assigning {count} permissions to role {roleName}",
                    permissions.Count, role.Name);

                var addedCount = 0;
                var skippedCount = 0;
                var errors = new List<string>();

                foreach (var permission in permissions)
                {
                    var result = await AssignPermissionToRoleAsync(role, permission);
                    if (result.Success)
                    {
                        addedCount++;
                    }
                    else
                    {
                        skippedCount++;
                        if (result.Errors != null && result.Errors.Count > 0)
                        {
                            errors.AddRange(result.Errors);
                        }
                    }
                }

                m_logger.LogInformation("AssignPermissions - Added {added} permissions, skipped {skipped} for role {roleName}",
                    addedCount, skippedCount, role.Name);

                return DataWrapper<bool>.Succeed(true, 
                    message: $"Assigned {addedCount} permissions to role. {skippedCount} skipped.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "AssignPermissions - Unexpected error assigning permissions to role {roleName}",
                    role.Name);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while assigning permissions.");
            }
        }

        /// <summary>
        /// Remove multiple permissions from a role
        /// </summary>
        public async Task<DataWrapper<bool>> RemovePermissionsFromRoleAsync(Db_ApplicationRole role, List<string> permissions)
        {
            try
            {
                // Protect Super Admin role
                if (IsProtectedRole(role))
                {
                    m_logger.LogWarning("RemovePermissions - Attempted to modify protected role: {roleName}", role.Name);
                    return DataWrapper<bool>.BadRequest(message: $"Cannot modify permissions for '{role.Name}' role. This role is protected.");
                }

                m_logger.LogInformation("RemovePermissions - Removing {count} permissions from role {roleName}",
                    permissions.Count, role.Name);

                var removedCount = 0;
                var skippedCount = 0;
                var errors = new List<string>();

                foreach (var permission in permissions)
                {
                    var result = await RemovePermissionFromRoleAsync(role, permission);
                    if (result.Success)
                    {
                        removedCount++;
                    }
                    else
                    {
                        skippedCount++;
                        if (result.Errors != null && result.Errors.Count > 0)
                        {
                            errors.AddRange(result.Errors);
                        }
                    }
                }

                m_logger.LogInformation("RemovePermissions - Removed {removed} permissions, skipped {skipped} from role {roleName}",
                    removedCount, skippedCount, role.Name);

                return DataWrapper<bool>.Succeed(true, 
                    message: $"Removed {removedCount} permissions from role. {skippedCount} skipped.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "RemovePermissions - Unexpected error removing permissions from role {roleName}",
                    role.Name);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while removing permissions.");
            }
        }
    }
}
