using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Authorization.Models.Dtos;
using PaymentGateway.Server.Authorization.Utils;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;
using System.Security.Claims;

namespace PaymentGateway.Server.Authorization.Services
{
    public class RoleService
    {
        private readonly RoleManager<Db_ApplicationRole> m_roleManager;
        private readonly UserManager<Db_ApplicationUser> m_userManager;
        private readonly AppDbContext m_dbContext;
        private readonly ILogger<RoleService> m_logger;

        // Protected role names - cannot be deleted
        private static readonly string[] PROTECTED_ROLES = { "Super Admin", "User" };
        private const string SUPER_ADMIN_ROLE = "Super Admin";

        public RoleService(
            RoleManager<Db_ApplicationRole> roleManager,
            UserManager<Db_ApplicationUser> userManager,
            AppDbContext dbContext,
            ILogger<RoleService> logger)
        {
            m_roleManager = roleManager;
            m_userManager = userManager;
            m_dbContext = dbContext;
            m_logger = logger;
        }

        /// <summary>
        /// Check if role is protected
        /// </summary>
        private bool IsProtectedRole(string roleName)
        {
            return PROTECTED_ROLES.Any(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if user is Super Admin
        /// </summary>
        private bool IsSuperAdmin(IList<string> userRoles)
        {
            return userRoles.Any(r => r.Equals(SUPER_ADMIN_ROLE, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Create a new role
        /// </summary>
        public async Task<DataWrapper<Dto_RoleResponse>> CreateRoleAsync(Dto_CreateRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("CreateRole - Attempting to create role: {roleName}", request.Name);

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    m_logger.LogWarning("CreateRole - Role name is empty");
                    return DataWrapper<Dto_RoleResponse>.BadRequest(message: "Role name is required.");
                }

                // Check if role already exists
                var existingRole = await m_roleManager.FindByNameAsync(request.Name);
                if (existingRole != null)
                {
                    m_logger.LogWarning("CreateRole - Role already exists: {roleName}", request.Name);
                    return DataWrapper<Dto_RoleResponse>.Conflict(message: "Role already exists.");
                }

                // Create new role
                var newRole = new Db_ApplicationRole
                {
                    Name = request.Name
                };

                var result = await m_roleManager.CreateAsync(newRole);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("CreateRole - Failed to create role: {roleName}. Errors: {errors}",
                        request.Name, string.Join(", ", errors));
                    return DataWrapper<Dto_RoleResponse>.BadRequest(message: "Failed to create role.", errors: errors);
                }

                m_logger.LogInformation("CreateRole - Role created successfully: {roleName}", request.Name);

                return DataWrapper<Dto_RoleResponse>.Succeed(
                    new Dto_RoleResponse
                    {
                        Id = newRole.Id.ToString(),
                        Name = newRole.Name,
                        UserCount = 0
                    },
                    message: "Role created successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "CreateRole - Unexpected error while creating role: {roleName}", request.Name);
                return DataWrapper<Dto_RoleResponse>.Fail_InternalError(message: "An error occurred while creating the role.");
            }
        }

        /// <summary>
        /// Get all roles
        /// </summary>
        public async Task<DataWrapper<Dto_RolesListResponse>> GetAllRolesAsync()
        {
            try
            {
                m_logger.LogInformation("GetAllRoles - Fetching all roles");

                var roles = await m_roleManager.Roles.ToListAsync();

                var rolesResponse = new List<Dto_RoleResponse>();

                foreach (var role in roles)
                {
                    var usersInRole = await m_userManager.GetUsersInRoleAsync(role.Name);
                    rolesResponse.Add(new Dto_RoleResponse
                    {
                        Id = role.Id.ToString(),
                        Name = role.Name,
                        UserCount = usersInRole.Count
                    });
                }

                m_logger.LogInformation("GetAllRoles - Retrieved {count} roles", rolesResponse.Count);

                return DataWrapper<Dto_RolesListResponse>.Succeed(
                    new Dto_RolesListResponse
                    {
                        Roles = rolesResponse,
                        TotalCount = rolesResponse.Count
                    },
                    message: "Roles retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAllRoles - Unexpected error while fetching roles");
                return DataWrapper<Dto_RolesListResponse>.Fail_InternalError(message: "An error occurred while fetching roles.");
            }
        }

        /// <summary>
        /// Get role by name with user count
        /// </summary>
        public async Task<DataWrapper<Dto_RoleResponse>> GetRoleByNameAsync(string roleName)
        {
            try
            {
                m_logger.LogInformation("GetRoleByName - Fetching role: {roleName}", roleName);

                var role = await m_roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    m_logger.LogWarning("GetRoleByName - Role not found: {roleName}", roleName);
                    return DataWrapper<Dto_RoleResponse>.NotFound(message: "Role not found.");
                }

                var usersInRole = await m_userManager.GetUsersInRoleAsync(role.Name);

                m_logger.LogInformation("GetRoleByName - Role found: {roleName}", roleName);

                return DataWrapper<Dto_RoleResponse>.Succeed(
                    new Dto_RoleResponse
                    {
                        Id = role.Id.ToString(),
                        Name = role.Name,
                        UserCount = usersInRole.Count
                    },
                    message: "Role retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetRoleByName - Unexpected error while fetching role: {roleName}", roleName);
                return DataWrapper<Dto_RoleResponse>.Fail_InternalError(message: "An error occurred while fetching the role.");
            }
        }

        /// <summary>
        /// Delete a role
        /// </summary>
        public async Task<DataWrapper<bool>> DeleteRoleAsync(string roleName)
        {
            try
            {
                m_logger.LogInformation("DeleteRole - Attempting to delete role: {roleName}", roleName);

                // Protect Super Admin and Admin roles
                if (IsProtectedRole(roleName))
                {
                    m_logger.LogWarning("DeleteRole - Attempted to delete protected role: {roleName}", roleName);
                    return DataWrapper<bool>.BadRequest(message: $"Cannot delete '{roleName}' role. This role is protected.");
                }

                var role = await m_roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    m_logger.LogWarning("DeleteRole - Role not found: {roleName}", roleName);
                    return DataWrapper<bool>.NotFound(message: "Role not found.");
                }

                // Check if any users have this role
                var usersInRole = await m_userManager.GetUsersInRoleAsync(role.Name);
                if (usersInRole.Count > 0)
                {
                    m_logger.LogWarning("DeleteRole - Cannot delete role with {count} users: {roleName}",
                        usersInRole.Count, roleName);
                    return DataWrapper<bool>.BadRequest(
                        message: $"Cannot delete role. {usersInRole.Count} user(s) are still assigned to this role.");
                }

                var result = await m_roleManager.DeleteAsync(role);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("DeleteRole - Failed to delete role: {roleName}. Errors: {errors}",
                        roleName, string.Join(", ", errors));
                    return DataWrapper<bool>.BadRequest(message: "Failed to delete role.", errors: errors);
                }

                m_logger.LogInformation("DeleteRole - Role deleted successfully: {roleName}", roleName);
                return DataWrapper<bool>.Succeed(true, message: "Role deleted successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "DeleteRole - Unexpected error while deleting role: {roleName}", roleName);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while deleting the role.");
            }
        }

        /// <summary>
        /// Add user to role
        /// </summary>
        public async Task<DataWrapper<bool>> AddUserToRoleAsync(Dto_AddUserToRoleRequest request, IList<string> currentUserRoles)
        {
            try
            {
                m_logger.LogInformation("AddUserToRole - Adding user {userId} to role: {roleName}",
                    request.UserId, request.RoleName);

                // Protect Super Admin role assignment - only Super Admin can assign this role
                if (request.RoleName.Equals(SUPER_ADMIN_ROLE, StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsSuperAdmin(currentUserRoles))
                    {
                        m_logger.LogWarning("AddUserToRole - Non-Super Admin user attempted to assign Super Admin role. User roles: {roles}",
                            string.Join(", ", currentUserRoles));
                        return DataWrapper<bool>.BadRequest(
                            message: $"Only Super Admin users can assign the '{SUPER_ADMIN_ROLE}' role to other users.");
                    }
                }

                // Check if user exists
                var user = await m_userManager.FindByIdAsync(request.UserId.ToString());
                if (user == null)
                {
                    m_logger.LogWarning("AddUserToRole - User not found: {userId}", request.UserId);
                    return DataWrapper<bool>.NotFound(message: "User not found.");
                }

                // Check if role exists
                var roleExists = await m_roleManager.RoleExistsAsync(request.RoleName);
                if (!roleExists)
                {
                    m_logger.LogWarning("AddUserToRole - Role not found: {roleName}", request.RoleName);
                    return DataWrapper<bool>.NotFound(message: "Role not found.");
                }

                // Check if user already has this role
                var userHasRole = await m_userManager.IsInRoleAsync(user, request.RoleName);
                if (userHasRole)
                {
                    m_logger.LogWarning("AddUserToRole - User already has role: {userId}, {roleName}",
                        request.UserId, request.RoleName);
                    return DataWrapper<bool>.BadRequest(message: "User already has this role.");
                }

                // Add user to role
                var result = await m_userManager.AddToRoleAsync(user, request.RoleName);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("AddUserToRole - Failed to add user to role: {userId}, {roleName}. Errors: {errors}",
                        request.UserId, request.RoleName, string.Join(", ", errors));
                    return DataWrapper<bool>.BadRequest(message: "Failed to add user to role.", errors: errors);
                }

                m_logger.LogInformation("AddUserToRole - User added to role successfully: {userId}, {roleName}",
                    request.UserId, request.RoleName);
                return DataWrapper<bool>.Succeed(true, message: "User added to role successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "AddUserToRole - Unexpected error: {userId}, {roleName}",
                    request.UserId, request.RoleName);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while adding user to role.");
            }
        }

        /// <summary>
        /// Remove user from role
        /// </summary>
        public async Task<DataWrapper<bool>> RemoveUserFromRoleAsync(Dto_RemoveUserFromRoleRequest request, IList<string> currentUserRoles)
        {
            try
            {
                m_logger.LogInformation("RemoveUserFromRole - Removing user {userId} from role: {roleName}",
                    request.UserId, request.RoleName);

                // Protect Super Admin role removal - only Super Admin can remove this role
                if (request.RoleName.Equals(SUPER_ADMIN_ROLE, StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsSuperAdmin(currentUserRoles))
                    {
                        m_logger.LogWarning("RemoveUserFromRole - Non-Super Admin user attempted to remove Super Admin role. User roles: {roles}",
                            string.Join(", ", currentUserRoles));
                        return DataWrapper<bool>.BadRequest(
                            message: $"Only Super Admin users can remove the '{SUPER_ADMIN_ROLE}' role from other users.");
                    }
                }

                // Check if user exists
                var user = await m_userManager.FindByIdAsync(request.UserId.ToString());
                if (user == null)
                {
                    m_logger.LogWarning("RemoveUserFromRole - User not found: {userId}", request.UserId);
                    return DataWrapper<bool>.NotFound(message: "User not found.");
                }

                // Check if user has this role
                var userHasRole = await m_userManager.IsInRoleAsync(user, request.RoleName);
                if (!userHasRole)
                {
                    m_logger.LogWarning("RemoveUserFromRole - User does not have role: {userId}, {roleName}",
                        request.UserId, request.RoleName);
                    return DataWrapper<bool>.BadRequest(message: "User does not have this role.");
                }

                // Remove user from role
                var result = await m_userManager.RemoveFromRoleAsync(user, request.RoleName);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("RemoveUserFromRole - Failed to remove user from role: {userId}, {roleName}. Errors: {errors}",
                        request.UserId, request.RoleName, string.Join(", ", errors));
                    return DataWrapper<bool>.BadRequest(message: "Failed to remove user from role.", errors: errors);
                }

                m_logger.LogInformation("RemoveUserFromRole - User removed from role successfully: {userId}, {roleName}",
                    request.UserId, request.RoleName);
                return DataWrapper<bool>.Succeed(true, message: "User removed from role successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "RemoveUserFromRole - Unexpected error: {userId}, {roleName}",
                    request.UserId, request.RoleName);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while removing user from role.");
            }
        }

        /// <summary>
        /// Get all roles for a specific user
        /// </summary>
        public async Task<DataWrapper<Dto_UserRolesResponse>> GetUserRolesAsync(string userId)
        {
            try
            {
                m_logger.LogInformation("GetUserRoles - Fetching roles for user: {userId}", userId);

                var user = await m_userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    m_logger.LogWarning("GetUserRoles - User not found: {userId}", userId);
                    return DataWrapper<Dto_UserRolesResponse>.NotFound(message: "User not found.");
                }

                var userRoles = await m_userManager.GetRolesAsync(user);

                m_logger.LogInformation("GetUserRoles - Retrieved {count} roles for user: {userId}",
                    userRoles.Count, userId);

                return DataWrapper<Dto_UserRolesResponse>.Succeed(
                    new Dto_UserRolesResponse
                    {
                        UserId = user.Id.ToString(),
                        Email = user.Email,
                        Roles = userRoles.ToList()
                    },
                    message: "User roles retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetUserRoles - Unexpected error: {userId}", userId);
                return DataWrapper<Dto_UserRolesResponse>.Fail_InternalError(message: "An error occurred while fetching user roles.");
            }
        }

        /// <summary>
        /// Get paginated list of users in a specific role
        /// </summary>
        public async Task<DataWrapper<Dto_GetUsersByRoleResponse>> GetUsersByRoleAsync(Dto_GetUsersByRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("GetUsersByRole - Fetching users for role: {roleName}, page: {pageNumber}, pageSize: {pageSize}",
                    request.RoleName, request.PageNumber, request.PageSize);

                // Check if role exists
                var roleExists = await m_roleManager.RoleExistsAsync(request.RoleName);
                if (!roleExists)
                {
                    m_logger.LogWarning("GetUsersByRole - Role not found: {roleName}", request.RoleName);
                    return DataWrapper<Dto_GetUsersByRoleResponse>.NotFound(message: "Role not found.");
                }

                // Get all users in the role
                var usersInRole = await m_userManager.GetUsersInRoleAsync(request.RoleName);
                var totalCount = usersInRole.Count;

                // Apply pagination
                var paginatedUsers = usersInRole
                    .OrderBy(u => u.Email)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var userDtos = paginatedUsers.Select(u => new Dto_UserInRoleResponse
                {
                    UserId = u.Id.ToString(),
                    Email = u.Email
                }).ToList();

                m_logger.LogInformation("GetUsersByRole - Retrieved {count} users for role: {roleName}",
                    userDtos.Count, request.RoleName);

                return DataWrapper<Dto_GetUsersByRoleResponse>.Succeed(
                    new Dto_GetUsersByRoleResponse
                    {
                        RoleName = request.RoleName,
                        Users = userDtos,
                        TotalCount = totalCount,
                        PageNumber = request.PageNumber,
                        PageSize = request.PageSize
                    },
                    message: "Users retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetUsersByRole - Unexpected error for role: {roleName}", request.RoleName);
                return DataWrapper<Dto_GetUsersByRoleResponse>.Fail_InternalError(message: "An error occurred while fetching users.");
            }
        }

        // ===== TECHNICAL PLAN METHODS =====

        /// <summary>
        /// Get role by ID with claims
        /// Technical Plan: GetByIdAsync → Returns DataWrapper<Dto_RoleDetail> with assigned claims
        /// </summary>
        public async Task<DataWrapper<Dto_RoleDetail>> GetByIdAsync(string roleId)
        {
            try
            {
                m_logger.LogInformation("GetByIdAsync - Fetching role: {roleId}", roleId);

                var role = await m_roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    m_logger.LogWarning("GetByIdAsync - Role not found: {roleId}", roleId);
                    return DataWrapper<Dto_RoleDetail>.NotFound(message: "Role not found.");
                }

                // Get users in role
                var usersInRole = await m_userManager.GetUsersInRoleAsync(role.Name!);

                // Get claims for role
                var roleClaims = await m_roleManager.GetClaimsAsync(role);
                var claimValues = roleClaims
                    .Where(c => c.Type == AuthorizationConstants.PermissionClaimType)
                    .Select(c => c.Value)
                    .ToList();

                m_logger.LogInformation("GetByIdAsync - Role found: {roleName} with {claimCount} claims",
                    role.Name, claimValues.Count);

                return DataWrapper<Dto_RoleDetail>.Succeed(
                    new Dto_RoleDetail
                    {
                        RoleId = role.Id.ToString(),
                        Name = role.Name!,
                        Description = role.Description,
                        IsSystemRole = role.IsSystemRole,
                        CreatedAt = role.CreatedAt,
                        Claims = claimValues,
                        UserCount = usersInRole.Count
                    },
                    message: "Role retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetByIdAsync - Unexpected error: {roleId}", roleId);
                return DataWrapper<Dto_RoleDetail>.Fail_InternalError(message: "An error occurred while fetching role.");
            }
        }

        /// <summary>
        /// Create role
        /// Technical Plan: CreateAsync → Returns DataWrapper<Dto_RoleDetail>, sets IsSystemRole = false by default
        /// </summary>
        public async Task<DataWrapper<Dto_RoleDetail>> CreateAsync(Dto_RoleCreateRequest request)
        {
            try
            {
                m_logger.LogInformation("CreateAsync - Creating role: {roleName}", request.Name);

                // Check if role exists
                var existing = await m_roleManager.FindByNameAsync(request.Name);
                if (existing != null)
                {
                    m_logger.LogWarning("CreateAsync - Role already exists: {roleName}", request.Name);
                    return DataWrapper<Dto_RoleDetail>.Conflict(message: "Role already exists.");
                }

                var newRole = new Db_ApplicationRole
                {
                    Name = request.Name,
                    Description = request.Description,
                    IsSystemRole = false, // Technical plan: false by default
                    CreatedAt = DateTime.UtcNow
                };

                var result = await m_roleManager.CreateAsync(newRole);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("CreateAsync - Failed: {errors}", string.Join(", ", errors));
                    return DataWrapper<Dto_RoleDetail>.BadRequest(message: "Failed to create role.", errors: errors);
                }

                m_logger.LogInformation("CreateAsync - Role created: {roleName}", newRole.Name);

                return DataWrapper<Dto_RoleDetail>.Succeed(
                    new Dto_RoleDetail
                    {
                        RoleId = newRole.Id.ToString(),
                        Name = newRole.Name!,
                        Description = newRole.Description,
                        IsSystemRole = newRole.IsSystemRole,
                        CreatedAt = newRole.CreatedAt,
                        Claims = new List<string>(),
                        UserCount = 0
                    },
                    message: "Role created successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "CreateAsync - Unexpected error: {roleName}", request.Name);
                return DataWrapper<Dto_RoleDetail>.Fail_InternalError(message: "An error occurred while creating role.");
            }
        }

        /// <summary>
        /// Update role
        /// Technical Plan: UpdateAsync → Returns DataWrapper<Dto_RoleDetail>, cannot modify IsSystemRole
        /// </summary>
        public async Task<DataWrapper<Dto_RoleDetail>> UpdateAsync(string roleId, Dto_RoleUpdateRequest request)
        {
            try
            {
                m_logger.LogInformation("UpdateAsync - Updating role: {roleId}", roleId);

                var role = await m_roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    m_logger.LogWarning("UpdateAsync - Role not found: {roleId}", roleId);
                    return DataWrapper<Dto_RoleDetail>.NotFound(message: "Role not found.");
                }

                // Update name and description only (IsSystemRole cannot be modified)
                role.Name = request.Name;
                role.Description = request.Description;

                var result = await m_roleManager.UpdateAsync(role);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("UpdateAsync - Failed: {errors}", string.Join(", ", errors));
                    return DataWrapper<Dto_RoleDetail>.BadRequest(message: "Failed to update role.", errors: errors);
                }

                // Get updated data
                var usersInRole = await m_userManager.GetUsersInRoleAsync(role.Name!);
                var roleClaims = await m_roleManager.GetClaimsAsync(role);
                var claimValues = roleClaims
                    .Where(c => c.Type == AuthorizationConstants.PermissionClaimType)
                    .Select(c => c.Value)
                    .ToList();

                m_logger.LogInformation("UpdateAsync - Role updated: {roleName}", role.Name);

                return DataWrapper<Dto_RoleDetail>.Succeed(
                    new Dto_RoleDetail
                    {
                        RoleId = role.Id.ToString(),
                        Name = role.Name!,
                        Description = role.Description,
                        IsSystemRole = role.IsSystemRole,
                        CreatedAt = role.CreatedAt,
                        Claims = claimValues,
                        UserCount = usersInRole.Count
                    },
                    message: "Role updated successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "UpdateAsync - Unexpected error: {roleId}", roleId);
                return DataWrapper<Dto_RoleDetail>.Fail_InternalError(message: "An error occurred while updating role.");
            }
        }

        /// <summary>
        /// Delete role
        /// Technical Plan: DeleteAsync → Returns DataWrapper<bool>, checks role.IsSystemRole and returns Forbidden if true
        /// </summary>
        public async Task<DataWrapper<bool>> DeleteAsync(string roleId)
        {
            try
            {
                m_logger.LogInformation("DeleteAsync - Deleting role: {roleId}", roleId);

                var role = await m_roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    m_logger.LogWarning("DeleteAsync - Role not found: {roleId}", roleId);
                    return DataWrapper<bool>.NotFound(message: "Role not found.");
                }

                // Technical plan: Check IsSystemRole and return Forbidden
                if (role.IsSystemRole)
                {
                    m_logger.LogWarning("DeleteAsync - Attempted to delete system role: {roleName}", role.Name);
                    return DataWrapper<bool>.Forbidden(message: "Cannot delete system roles.");
                }

                // Check if users have this role
                var usersInRole = await m_userManager.GetUsersInRoleAsync(role.Name!);
                if (usersInRole.Count > 0)
                {
                    m_logger.LogWarning("DeleteAsync - Role has {count} users: {roleName}", usersInRole.Count, role.Name);
                    return DataWrapper<bool>.BadRequest(
                        message: $"Cannot delete role. {usersInRole.Count} user(s) are still assigned to this role.");
                }

                var result = await m_roleManager.DeleteAsync(role);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                    m_logger.LogWarning("DeleteAsync - Failed: {errors}", string.Join(", ", errors));
                    return DataWrapper<bool>.BadRequest(message: "Failed to delete role.", errors: errors);
                }

                m_logger.LogInformation("DeleteAsync - Role deleted: {roleName}", role.Name);
                return DataWrapper<bool>.Succeed(true, message: "Role deleted successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "DeleteAsync - Unexpected error: {roleId}", roleId);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while deleting role.");
            }
        }

        /// <summary>
        /// Assign claims to role
        /// Technical Plan: AssignClaimsAsync → Returns DataWrapper<bool>, uses RoleManager claims methods
        /// </summary>
        public async Task<DataWrapper<bool>> AssignClaimsAsync(string roleId, Dto_AssignClaimsRequest request)
        {
            try
            {
                m_logger.LogInformation("AssignClaimsAsync - Assigning {count} claims to role: {roleId}",
                    request.Claims.Count, roleId);

                var role = await m_roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    m_logger.LogWarning("AssignClaimsAsync - Role not found: {roleId}", roleId);
                    return DataWrapper<bool>.NotFound(message: "Role not found.");
                }

                // PROTECTION: SuperAdmin role claims cannot be edited
                if (role.Name.Equals(SUPER_ADMIN_ROLE, StringComparison.OrdinalIgnoreCase))
                {
                    m_logger.LogWarning("AssignClaimsAsync - Attempted to modify SuperAdmin role claims. Role: {roleName}", role.Name);
                    return DataWrapper<bool>.Forbidden(
                        message: $"Cannot modify claims for '{SUPER_ADMIN_ROLE}' role. This role's permissions are managed by the system.");
                }

                // Remove all existing permission claims
                var existingClaims = await m_roleManager.GetClaimsAsync(role);
                var permissionClaims = existingClaims.Where(c => c.Type == AuthorizationConstants.PermissionClaimType).ToList();

                foreach (var claim in permissionClaims)
                {
                    await m_roleManager.RemoveClaimAsync(role, claim);
                }

                // Add new claims
                foreach (var claimValue in request.Claims)
                {
                    var claim = new Claim(AuthorizationConstants.PermissionClaimType, claimValue);
                    var result = await m_roleManager.AddClaimAsync(role, claim);

                    if (!result.Succeeded)
                    {
                        var errors = result.Errors.Select(e => $"[{e.Code}] {e.Description}").ToList();
                        m_logger.LogWarning("AssignClaimsAsync - Failed to add claim {claim}: {errors}",
                            claimValue, string.Join(", ", errors));
                    }
                }

                m_logger.LogInformation("AssignClaimsAsync - Claims assigned to role: {roleName}", role.Name);
                return DataWrapper<bool>.Succeed(true, message: "Claims assigned successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "AssignClaimsAsync - Unexpected error: {roleId}", roleId);
                return DataWrapper<bool>.Fail_InternalError(message: "An error occurred while assigning claims.");
            }
        }

        /// <summary>
        /// Get available claims
        /// Technical Plan: GetAvailableClaimsAsync → Returns DataWrapper<List<string>> from AuthorizationConstants
        /// </summary>
        public async Task<DataWrapper<List<string>>> GetAvailableClaimsAsync()
        {
            try
            {
                m_logger.LogInformation("GetAvailableClaimsAsync - Fetching available claims");

                var claims = AuthorizationConstants.Permissions.GetAll();

                m_logger.LogInformation("GetAvailableClaimsAsync - Retrieved {count} claims", claims.Count);

                return await Task.FromResult(DataWrapper<List<string>>.Succeed(
                    claims,
                    message: "Available claims retrieved successfully."));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAvailableClaimsAsync - Unexpected error");
                return DataWrapper<List<string>>.Fail_InternalError(message: "An error occurred while fetching available claims.");
            }
        }

        /// <summary>
        /// Get all roles with details
        /// Technical Plan: GetRolesAsync → Returns DataWrapper<List<Dto_RoleListItem>>
        /// </summary>
        public async Task<DataWrapper<List<Dto_RoleListItem>>> GetRolesAsync()
        {
            try
            {
                m_logger.LogInformation("GetRolesAsync - Fetching all roles");

                var roles = await m_roleManager.Roles.ToListAsync();
                var roleListItems = new List<Dto_RoleListItem>();

                foreach (var role in roles)
                {
                    var usersInRole = await m_userManager.GetUsersInRoleAsync(role.Name!);
                    roleListItems.Add(new Dto_RoleListItem
                    {
                        RoleId = role.Id.ToString(),
                        Name = role.Name!,
                        Description = role.Description,
                        IsSystemRole = role.IsSystemRole,
                        CreatedAt = role.CreatedAt,
                        UserCount = usersInRole.Count
                    });
                }

                m_logger.LogInformation("GetRolesAsync - Retrieved {count} roles", roleListItems.Count);

                return DataWrapper<List<Dto_RoleListItem>>.Succeed(
                    roleListItems,
                    message: "Roles retrieved successfully.");
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetRolesAsync - Unexpected error");
                return DataWrapper<List<Dto_RoleListItem>>.Fail_InternalError(message: "An error occurred while fetching roles.");
            }
        }
    }
}
