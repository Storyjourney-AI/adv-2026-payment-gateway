using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Server.Authorization.Models.Dtos;
using PaymentGateway.Server.Authorization.Services;
using PaymentGateway.Server.Authorization.Utils;
using PaymentGateway.Server.Common.Models;

namespace PaymentGateway.Server.Authorization.Controllers
{
    /// <summary>
    /// Controller for managing role permissions/claims
    /// Uses policy-based authorization for fine-grained access control
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClaimsController : ControllerBase
    {
        private readonly ClaimsService m_claimsService;
        private readonly ILogger<ClaimsController> m_logger;

        public ClaimsController(
            ClaimsService claimsService,
            ILogger<ClaimsController> logger)
        {
            m_claimsService = claimsService;
            m_logger = logger;
        }

        /// <summary>
        /// Get all available permissions organized by sections (Read operation)
        /// Requires role_view permission
        /// </summary>
        /// <returns>List of permission sections with metadata</returns>
        [HttpGet("permissions")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public ActionResult<DataWrapper<List<AuthorizationConstants.PermissionSection>>> GetAllPermissions()
        {
            try
            {
                m_logger.LogInformation("GetAllPermissions endpoint called");

                var result = m_claimsService.GetAllPermissions();
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAllPermissions endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<List<AuthorizationConstants.PermissionSection>>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching permissions."));
            }
        }

        /// <summary>
        /// Get all available permissions as a flat list (Read operation)
        /// Requires role_view permission
        /// </summary>
        /// <returns>List of all permission names</returns>
        [HttpGet("permissions/flat")]
        [Authorize(Policy = AuthorizationConstants.Policies.RoleView)]
        public ActionResult<DataWrapper<List<string>>> GetAllPermissionsFlat()
        {
            try
            {
                m_logger.LogInformation("GetAllPermissionsFlat endpoint called");

                var result = m_claimsService.GetAllPermissionsFlat();
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetAllPermissionsFlat endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<List<string>>.Fail_InternalError(
                    message: "An unexpected error occurred while fetching permissions."));
            }
        }

        /// <summary>
        /// Get permissions for a specific role (Read operation)
        /// Requires role_view permission
        /// </summary>
        /// <param name="roleName">Name of the role</param>
        /// <returns>List of permissions assigned to the role</returns>
        [HttpGet("role/{roleName}/permissions")]
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
        /// Assign a permission to a role (Create operation)
        /// Requires role_edit permission
        /// </summary>
        /// <param name="request">Permission assignment request</param>
        /// <returns>Operation result</returns>
        [HttpPost("assign")]
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
        /// Remove a permission from a role (Delete operation)
        /// Requires role_edit permission
        /// </summary>
        /// <param name="request">Permission removal request</param>
        /// <returns>Operation result</returns>
        [HttpDelete("remove")]
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
        /// Update all permissions for a role (Update operation)
        /// Requires role_edit permission - replaces existing permissions
        /// </summary>
        /// <param name="roleName">Name of the role</param>
        /// <param name="request">List of permissions to assign</param>
        /// <returns>Operation result</returns>
        [HttpPut("role/{roleName}/permissions")]
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
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "UpdateRolePermissions endpoint - Unexpected error for role: {roleName}", roleName);
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred while updating role permissions."));
            }
        }
    }
}
