using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Server.ActivityLog.Services;
using PaymentGateway.Server.Authorization.Models.Dtos;
using PaymentGateway.Server.Authorization.Services;
using PaymentGateway.Server.Authorization.Utils;
using PaymentGateway.Server.Common.Models;

namespace PaymentGateway.Server.Authorization.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly RoleService m_roleService;
        private readonly ClaimsService m_claimsService;
        private readonly ActivityLogService m_activityLog;
        private readonly ILogger<RolesController> m_logger;

        public RolesController(
            RoleService roleService,
            ClaimsService claimsService,
            ActivityLogService activityLog,
            ILogger<RolesController> logger)
        {
            m_roleService = roleService;
            m_claimsService = claimsService;
            m_activityLog = activityLog;
            m_logger = logger;
        }

        /// <summary>
        /// Create a new role (requires role_create permission)
        /// </summary>
        /// <param name="request">Role creation request</param>
        /// <returns>Created role details</returns>
        [HttpPost("create")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleCreate)]
        public async Task<ActionResult<DataWrapper<Dto_RoleResponse>>> CreateRole([FromBody] Dto_CreateRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("CreateRole endpoint called for role: {roleName}", request.Name);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    m_logger.LogWarning("CreateRole - Validation failed for role: {roleName}. Errors: {errors}",
                        request.Name, string.Join(", ", errors));
                    return BadRequest(DataWrapper<Dto_RoleResponse>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var result = await m_roleService.CreateRoleAsync(request);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Creation,
                        $"Created role: {request.Name}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "CreateRole endpoint - Unexpected error for role: {roleName}",
                    request?.Name ?? "unknown");
                return StatusCode(500, DataWrapper<Dto_RoleResponse>.Fail_InternalError(
                    message: "An unexpected error occurred while creating the role."));
            }
        }

        /// <summary>
        /// Get all roles (requires role_view permission)
        /// </summary>
        /// <returns>List of all roles with user counts</returns>
        [HttpGet("list")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public async Task<ActionResult<DataWrapper<Dto_RolesListResponse>>> GetAllRoles()
        {
            try
            {
                m_logger.LogInformation("GetAllRoles endpoint called");

                var result = await m_roleService.GetAllRolesAsync();
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAllRoles endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<Dto_RolesListResponse>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching roles."));
            }
        }

        /// <summary>
        /// Add user to role (requires role_edit permission)
        /// </summary>
        /// <param name="request">Add user to role request</param>
        /// <returns>Operation result</returns>
        [HttpPost("add-user")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleEdit)]
        public async Task<ActionResult<DataWrapper<bool>>> AddUserToRole([FromBody] Dto_AddUserToRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("AddUserToRole endpoint called for user: {userId}, role: {roleName}",
                    request.UserId, request.RoleName);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    m_logger.LogWarning("AddUserToRole - Validation failed. Errors: {errors}",
                        string.Join(", ", errors));
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                // Get current user's roles for Super Admin protection
                var currentUserRoles = User.Claims
                    .Where(c => c.Type == global::System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                var result = await m_roleService.AddUserToRoleAsync(request, currentUserRoles);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Added user {request.UserId} to role: {request.RoleName}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "AddUserToRole endpoint - Unexpected error for user: {userId}, role: {roleName}",
                    request?.UserId.ToString() ?? "unknown", request?.RoleName ?? "unknown");
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while adding user to role."));
            }
        }

        /// <summary>
        /// Remove user from role (requires role_edit permission)
        /// </summary>
        /// <param name="request">Remove user from role request</param>
        /// <returns>Operation result</returns>
        [HttpPost("remove-user")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleEdit)]
        public async Task<ActionResult<DataWrapper<bool>>> RemoveUserFromRole([FromBody] Dto_RemoveUserFromRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("RemoveUserFromRole endpoint called for user: {userId}, role: {roleName}",
                    request.UserId, request.RoleName);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    m_logger.LogWarning("RemoveUserFromRole - Validation failed. Errors: {errors}",
                        string.Join(", ", errors));
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                // Get current user's roles for Super Admin protection
                var currentUserRoles = User.Claims
                    .Where(c => c.Type == global::System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                var result = await m_roleService.RemoveUserFromRoleAsync(request, currentUserRoles);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Removed user {request.UserId} from role: {request.RoleName}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "RemoveUserFromRole endpoint - Unexpected error for user: {userId}, role: {roleName}",
                    request?.UserId.ToString() ?? "unknown", request?.RoleName ?? "unknown");
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while removing user from role."));
            }
        }

        /// <summary>
        /// Get all roles for a specific user (requires role_view permission)
        /// </summary>
        /// <param name="userId">ID of the user</param>
        /// <returns>User roles</returns>
        [HttpGet("user/{userId}")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public async Task<ActionResult<DataWrapper<Dto_UserRolesResponse>>> GetUserRoles(string userId)
        {
            try
            {
                m_logger.LogInformation("GetUserRoles endpoint called for user: {userId}", userId);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    m_logger.LogWarning("GetUserRoles - User ID is empty");
                    return BadRequest(DataWrapper<Dto_UserRolesResponse>.BadRequest(
                        message: "User ID is required."));
                }

                var result = await m_roleService.GetUserRolesAsync(userId);
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetUserRoles endpoint - Unexpected error for user: {userId}", userId);
                return StatusCode(500, DataWrapper<Dto_UserRolesResponse>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching user roles."));
            }
        }

        /// <summary>
        /// Get paginated list of users in a specific role (requires role_view permission)
        /// </summary>
        /// <param name="request">Role name and pagination parameters</param>
        /// <returns>Paginated list of users (UserId and Email only)</returns>
        [HttpPost("users-by-role")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public async Task<ActionResult<DataWrapper<Dto_GetUsersByRoleResponse>>> GetUsersByRole([FromBody] Dto_GetUsersByRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("GetUsersByRole endpoint called for role: {roleName}, page: {pageNumber}",
                    request.RoleName, request.PageNumber);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    m_logger.LogWarning("GetUsersByRole - Validation failed. Errors: {errors}",
                        string.Join(", ", errors));
                    return BadRequest(DataWrapper<Dto_GetUsersByRoleResponse>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var result = await m_roleService.GetUsersByRoleAsync(request);
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetUsersByRole endpoint - Unexpected error for role: {roleName}",
                    request?.RoleName ?? "unknown");
                return StatusCode(500, DataWrapper<Dto_GetUsersByRoleResponse>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching users."));
            }
        }

        /// <summary>
        /// Get all permissions for a role (requires role_view permission)
        /// </summary>
        /// <param name="roleName">Name of the role</param>
        /// <returns>List of permissions assigned to the role</returns>
        [HttpGet("{roleName}/permissions")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public async Task<ActionResult<DataWrapper<Dto_RolePermissionsResponse>>> GetRolePermissions(string roleName)
        {
            try
            {
                m_logger.LogInformation("GetRolePermissions endpoint called for role: {roleName}", roleName);

                if (string.IsNullOrWhiteSpace(roleName))
                {
                    return BadRequest(DataWrapper<Dto_RolePermissionsResponse>.BadRequest(
                        message: "Role name is required."));
                }

                var roleManager = HttpContext.RequestServices.GetService(
                    typeof(Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>))
                    as Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>;

                var role = await roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    return NotFound(DataWrapper<Dto_RolePermissionsResponse>.NotFound(
                        message: "Role not found."));
                }

                var result = await m_claimsService.GetRolePermissionsAsync(role);
                
                var response = new Dto_RolePermissionsResponse
                {
                    RoleName = roleName,
                    Permissions = result.Data
                };

                return Ok(DataWrapper<Dto_RolePermissionsResponse>.Succeed(response, 
                    message: "Role permissions retrieved successfully."));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetRolePermissions endpoint - Unexpected error for role: {roleName}", roleName);
                return StatusCode(500, DataWrapper<Dto_RolePermissionsResponse>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching role permissions."));
            }
        }

        /// <summary>
        /// Get all available permissions (requires role_view permission)
        /// </summary>
        /// <returns>List of all available permissions organized by sections</returns>
        [HttpGet("permissions/available")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public ActionResult<DataWrapper<List<AuthorizationConstants.PermissionSection>>> GetAvailablePermissions()
        {
            try
            {
                m_logger.LogInformation("GetAvailablePermissions endpoint called");

                var result = m_claimsService.GetAllPermissions();
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAvailablePermissions endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<List<AuthorizationConstants.PermissionSection>>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching available permissions."));
            }
        }

        /// <summary>
        /// Get all available permissions organized by sections (requires role_view permission)
        /// </summary>
        /// <returns>List of permission sections with metadata</returns>
        [HttpGet("permissions/sections")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public ActionResult<DataWrapper<List<AuthorizationConstants.PermissionSection>>> GetPermissionSections()
        {
            try
            {
                m_logger.LogInformation("GetPermissionSections endpoint called");

                var sections = AuthorizationConstants.Permissions.GetAllSections();
                return Ok(DataWrapper<List<AuthorizationConstants.PermissionSection>>.Succeed(
                    sections, message: "Permission sections retrieved successfully."));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetPermissionSections endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<List<AuthorizationConstants.PermissionSection>>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching permission sections."));
            }
        }

        /// <summary>
        /// Assign a permission to a role (requires role_edit permission)
        /// </summary>
        /// <param name="request">Permission assignment request</param>
        /// <returns>Operation result</returns>
        [HttpPost("assign-permission")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleEdit)]
        public async Task<ActionResult<DataWrapper<bool>>> AssignPermissionToRole([FromBody] Dto_AssignPermissionToRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("AssignPermissionToRole endpoint called for role: {roleName}, permission: {permission}",
                    request.RoleName, request.Permission);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var roleManager = HttpContext.RequestServices.GetService(
                    typeof(Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>))
                    as Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>;

                var role = await roleManager.FindByNameAsync(request.RoleName);
                if (role == null)
                {
                    return NotFound(DataWrapper<bool>.NotFound(message: "Role not found."));
                }

                var result = await m_claimsService.AssignPermissionToRoleAsync(role, request.Permission);
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "AssignPermissionToRole endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while assigning permission."));
            }
        }

        /// <summary>
        /// Remove a permission from a role (requires role_edit permission)
        /// </summary>
        /// <param name="request">Permission removal request</param>
        /// <returns>Operation result</returns>
        [HttpPost("remove-permission")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleEdit)]
        public async Task<ActionResult<DataWrapper<bool>>> RemovePermissionFromRole([FromBody] Dto_RemovePermissionFromRoleRequest request)
        {
            try
            {
                m_logger.LogInformation("RemovePermissionFromRole endpoint called for role: {roleName}, permission: {permission}",
                    request.RoleName, request.Permission);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var roleManager = HttpContext.RequestServices.GetService(
                    typeof(Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>))
                    as Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>;

                var role = await roleManager.FindByNameAsync(request.RoleName);
                if (role == null)
                {
                    return NotFound(DataWrapper<bool>.NotFound(message: "Role not found."));
                }

                var result = await m_claimsService.RemovePermissionFromRoleAsync(role, request.Permission);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Removed permission '{request.Permission}' from role: {request.RoleName}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "RemovePermissionFromRole endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while removing permission."));
            }
        }

        /// <summary>
        /// Update all permissions for a role (requires role_edit permission) - replaces existing permissions
        /// </summary>
        /// <param name="roleName">Name of the role</param>
        /// <param name="request">List of permissions to assign</param>
        /// <returns>Operation result</returns>
        [HttpPut("{roleName}/permissions")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleEdit)]
        public async Task<ActionResult<DataWrapper<bool>>> UpdateRolePermissions(string roleName, [FromBody] Dto_UpdateRolePermissionsRequest request)
        {
            try
            {
                m_logger.LogInformation("UpdateRolePermissions endpoint called for role: {roleName}", roleName);

                if (string.IsNullOrWhiteSpace(roleName))
                {
                    return BadRequest(DataWrapper<bool>.BadRequest(message: "Role name is required."));
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Validation failed. Please check your input.",
                        errors: errors));
                }

                var roleManager = HttpContext.RequestServices.GetService(
                    typeof(Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>))
                    as Microsoft.AspNetCore.Identity.RoleManager<Models.Dbs.Db_ApplicationRole>;

                var role = await roleManager.FindByNameAsync(roleName);
                if (role == null)
                {
                    return NotFound(DataWrapper<bool>.NotFound(message: "Role not found."));
                }

                // Get current permissions
                var currentPermissions = await m_claimsService.GetRolePermissionsAsync(role);
                
                // Remove all current permissions
                if (currentPermissions.Success && currentPermissions.Data.Count > 0)
                {
                    await m_claimsService.RemovePermissionsFromRoleAsync(role, currentPermissions.Data);
                }

                // Assign new permissions
                var result = await m_claimsService.AssignPermissionsToRoleAsync(role, request.Permissions);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Updated permissions for role: {roleName}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "UpdateRolePermissions endpoint - Unexpected error for role: {roleName}", roleName);
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while updating role permissions."));
            }
        }

        // ===== TECHNICAL PLAN ENDPOINTS =====

        /// <summary>
        /// GET /api/Roles - List all roles
        /// Technical Plan: Returns DataWrapper<List<Dto_RoleListItem>>
        /// </summary>
        [HttpGet]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public async Task<ActionResult<DataWrapper<List<Dto_RoleListItem>>>> GetRoles()
        {
            try
            {
                m_logger.LogInformation("GetRoles endpoint called");

                var result = await m_roleService.GetRolesAsync();
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetRoles endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<List<Dto_RoleListItem>>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching roles."));
            }
        }

        /// <summary>
        /// GET /api/Roles/{id} - Get role detail with claims
        /// Technical Plan: Returns DataWrapper<Dto_RoleDetail>
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public async Task<ActionResult<DataWrapper<Dto_RoleDetail>>> GetRoleById(string id)
        {
            try
            {
                m_logger.LogInformation("GetRoleById endpoint called for: {roleId}", id);

                var result = await m_roleService.GetByIdAsync(id);
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetRoleById endpoint - Unexpected error for: {roleId}", id);
                return StatusCode(500, DataWrapper<Dto_RoleDetail>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching role."));
            }
        }

        /// <summary>
        /// POST /api/Roles - Create new role
        /// Technical Plan: Returns DataWrapper<Dto_RoleDetail>, sets IsSystemRole = false
        /// </summary>
        [HttpPost]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleCreate)]
        public async Task<ActionResult<DataWrapper<Dto_RoleDetail>>> CreateRole([FromBody] Dto_RoleCreateRequest request)
        {
            try
            {
                m_logger.LogInformation("CreateRole endpoint called for: {roleName}", request.Name);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_RoleDetail>.BadRequest(
                        message: "Validation failed.",
                        errors: errors));
                }

                var result = await m_roleService.CreateAsync(request);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Creation,
                        $"Created role: {request.Name}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "CreateRole endpoint - Unexpected error for: {roleName}", request.Name);
                return StatusCode(500, DataWrapper<Dto_RoleDetail>.Fail_InternalError(
                    message: "An unexpected error occurred while creating role."));
            }
        }

        /// <summary>
        /// PUT /api/Roles/{id} - Update role
        /// Technical Plan: Returns DataWrapper<Dto_RoleDetail>, cannot modify IsSystemRole
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleEdit)]
        public async Task<ActionResult<DataWrapper<Dto_RoleDetail>>> UpdateRole(string id, [FromBody] Dto_RoleUpdateRequest request)
        {
            try
            {
                m_logger.LogInformation("UpdateRole endpoint called for: {roleId}", id);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_RoleDetail>.BadRequest(
                        message: "Validation failed.",
                        errors: errors));
                }

                var result = await m_roleService.UpdateAsync(id, request);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Updated role: {id}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "UpdateRole endpoint - Unexpected error for: {roleId}", id);
                return StatusCode(500, DataWrapper<Dto_RoleDetail>.Fail_InternalError(
                    message: "An unexpected error occurred while updating role."));
            }
        }

        /// <summary>
        /// DELETE /api/Roles/{id} - Delete role (system roles protected)
        /// Technical Plan: Returns DataWrapper<bool>, checks IsSystemRole and returns Forbidden
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleDelete)]
        public async Task<ActionResult<DataWrapper<bool>>> DeleteRole(string id)
        {
            try
            {
                m_logger.LogInformation("DeleteRole endpoint called for: {roleId}", id);

                var result = await m_roleService.DeleteAsync(id);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Deletion,
                        $"Deleted role: {id}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "DeleteRole endpoint - Unexpected error for: {roleId}", id);
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while deleting role."));
            }
        }

        /// <summary>
        /// PUT /api/Roles/{id}/claims - Assign claims to role
        /// Technical Plan: Returns DataWrapper<bool>, uses RoleManager claims methods
        /// </summary>
        [HttpPut("{id}/claims")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleEdit)]
        public async Task<ActionResult<DataWrapper<bool>>> AssignClaims(string id, [FromBody] Dto_AssignClaimsRequest request)
        {
            try
            {
                m_logger.LogInformation("AssignClaims endpoint called for: {roleId} with {count} claims",
                    id, request.Claims.Count);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Validation failed.",
                        errors: errors));
                }

                var result = await m_roleService.AssignClaimsAsync(id, request);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Assigned {request.Claims.Count} claims to role: {id}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "AssignClaims endpoint - Unexpected error for: {roleId}", id);
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while assigning claims."));
            }
        }

        /// <summary>
        /// GET /api/Roles/available-claims - Get all available claims
        /// Technical Plan: Returns DataWrapper<List<string>> from AuthorizationConstants
        /// </summary>
        [HttpGet("available-claims")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public async Task<ActionResult<DataWrapper<List<string>>>> GetAvailableClaims()
        {
            try
            {
                m_logger.LogInformation("GetAvailableClaims endpoint called");

                var result = await m_roleService.GetAvailableClaimsAsync();
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAvailableClaims endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<List<string>>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching available claims."));
            }
        }
    }
}
