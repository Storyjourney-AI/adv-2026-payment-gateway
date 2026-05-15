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
    public class UsersController : ControllerBase
    {
        private readonly UsersService m_usersService;
        private readonly ActivityLogService m_activityLog;
        private readonly ILogger<UsersController> m_logger;

        public UsersController(
            UsersService usersService,
            ActivityLogService activityLog,
            ILogger<UsersController> logger)
        {
            m_usersService = usersService;
            m_activityLog = activityLog;
            m_logger = logger;
        }

        /// <summary>
        /// Get paginated list of users with optional search
        /// </summary>
        [HttpGet]
        [Authorize(Policy = AuthorizationConstants.Policies.UserView)]
        public async Task<ActionResult<DataWrapper<PaginationWrapper<Dto_UserListItem>>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                m_logger.LogInformation("GetUsers endpoint called - Page: {page}, PageSize: {pageSize}, Search: {search}",
                    page, pageSize, search);

                var result = await m_usersService.GetUsersAsync(page, pageSize, search);
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetUsers endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<PaginationWrapper<Dto_UserListItem>>.Fail_InternalError(
                    message: "An unexpected error occurred."));
            }
        }

        /// <summary>
        /// Get user details by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Policy = AuthorizationConstants.Policies.UserView)]
        public async Task<ActionResult<DataWrapper<Dto_UserDetail>>> GetUser(string id)
        {
            try
            {
                m_logger.LogInformation("GetUser endpoint called - UserId: {userId}", id);

                var result = await m_usersService.GetByIdAsync(id);
                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "GetUser endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<Dto_UserDetail>.Fail_InternalError(
                    message: "An unexpected error occurred."));
            }
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        [HttpPost]
        [Authorize(Policy = AuthorizationConstants.Policies.UserCreate)]
        public async Task<ActionResult<DataWrapper<Dto_UserDetail>>> CreateUser(
            [FromBody] Dto_UserCreateRequest request)
        {
            try
            {
                m_logger.LogInformation("CreateUser endpoint called - Email: {email}", request.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_UserDetail>.BadRequest(
                        message: "Validation failed.", errors: errors));
                }

                var result = await m_usersService.CreateAsync(request);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Creation,
                        $"Created user: {request.Email}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "CreateUser endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<Dto_UserDetail>.Fail_InternalError(
                    message: "An unexpected error occurred."));
            }
        }

        /// <summary>
        /// Update an existing user
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = AuthorizationConstants.Policies.UserEdit)]
        public async Task<ActionResult<DataWrapper<Dto_UserDetail>>> UpdateUser(
            string id,
            [FromBody] Dto_UserUpdateRequest request)
        {
            try
            {
                m_logger.LogInformation("UpdateUser endpoint called - UserId: {userId}", id);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_UserDetail>.BadRequest(
                        message: "Validation failed.", errors: errors));
                }

                var result = await m_usersService.UpdateAsync(id, request);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Updated user: {id}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "UpdateUser endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<Dto_UserDetail>.Fail_InternalError(
                    message: "An unexpected error occurred."));
            }
        }

        /// <summary>
        /// Toggle user active status
        /// </summary>
        [HttpPatch("{id}/toggle-active")]
        [Authorize(Policy = AuthorizationConstants.Policies.UserEdit)]
        public async Task<ActionResult<DataWrapper<bool>>> ToggleActive(string id)
        {
            try
            {
                m_logger.LogInformation("ToggleActive endpoint called - UserId: {userId}", id);

                var result = await m_usersService.ToggleActiveAsync(id);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Modification,
                        $"Toggled active status for user: {id}");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "ToggleActive endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An unexpected error occurred."));
            }
        }

        /// <summary>
        /// Batch create users from CSV data
        /// </summary>
        [HttpPost("batch")]
        [Authorize(Policy = AuthorizationConstants.Policies.UserCreate)]
        public async Task<ActionResult<DataWrapper<Dto_BatchCreateUsersResponse>>> BatchCreateUsers(
            [FromBody] Dto_BatchCreateUsersRequest request)
        {
            try
            {
                m_logger.LogInformation("BatchCreateUsers endpoint called - UserCount: {count}", 
                    request.Users.Count);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_BatchCreateUsersResponse>.BadRequest(
                        message: "Validation failed.", errors: errors));
                }

                var result = await m_usersService.BatchCreateAsync(request);

                if (result.Success)
                {
                    await m_activityLog.LogAsync(
                        ActivityLogCategory.Creation,
                        $"Batch created {request.Users.Count} users");
                }

                return StatusCode((int)result.Code, result);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "BatchCreateUsers endpoint - Unexpected error");
                return StatusCode(500, DataWrapper<Dto_BatchCreateUsersResponse>.Fail_InternalError(
                    message: "An unexpected error occurred."));
            }
        }
    }
}
