using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Server.ActivityLog.Services;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Applications.Models.Dtos;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;

namespace PaymentGateway.Server.Applications.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "RequireUser")]
    public class EnvironmentController : ControllerBase
    {
        private readonly AppDbContext m_dbContext;
        private readonly UserManager<Db_ApplicationUser> m_userManager;
        private readonly ActivityLogService m_activityLog;
        private readonly ILogger<EnvironmentController> m_logger;

        public EnvironmentController(
            AppDbContext dbContext,
            UserManager<Db_ApplicationUser> userManager,
            ActivityLogService activityLog,
            ILogger<EnvironmentController> logger)
        {
            m_dbContext = dbContext;
            m_userManager = userManager;
            m_activityLog = activityLog;
            m_logger = logger;
        }

        /// <summary>
        /// Get all environments for an application
        /// </summary>
        [HttpGet("by-application/{applicationId}")]
        public async Task<ActionResult<DataWrapper<List<Dto_EnvironmentResponse>>>> GetEnvironmentsByApplication(Guid applicationId)
        {
            try
            {
                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<List<Dto_EnvironmentResponse>>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return BadRequest(DataWrapper<List<Dto_EnvironmentResponse>>.BadRequest(
                        message: "Invalid user ID format"));
                }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                // Check if application exists and user has access
                var application = await m_dbContext.Applications
                    .FirstOrDefaultAsync(a => a.Id == applicationId);

                if (application == null)
                {
                    return NotFound(DataWrapper<List<Dto_EnvironmentResponse>>.NotFound(
                        message: "Application not found"));
                }

                if (!isSuperAdmin && application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<List<Dto_EnvironmentResponse>>.Forbidden(
                        message: "You do not have permission to access this application's environments"));
                }

                var environments = await m_dbContext.Environments
                    .Where(e => e.ApplicationId == applicationId && !e.IsDeleted)
                    .Select(e => new Dto_EnvironmentResponse
                    {
                        Id = e.Id,
                        ApplicationId = e.ApplicationId,
                        Name = e.Name,
                        ApiKey = e.ApiKey,
                        AllowedOrigins = e.AllowedOrigins,
                        WebhookUrl = e.WebhookUrl,
                        SuccessResponseUrl = e.SuccessResponseUrl,
                        PendingResponseUrl = e.PendingResponseUrl ?? string.Empty,
                        FailureResponseUrl = e.FailureResponseUrl,
                        IsSandbox = e.IsSandbox,
                        CreatedAt = e.CreatedAt,
                        UpdatedAt = e.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(DataWrapper<List<Dto_EnvironmentResponse>>.Succeed(
                    environments,
                    message: "Environments retrieved successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error retrieving environments for application {applicationId}", applicationId);
                return StatusCode(500, DataWrapper<List<Dto_EnvironmentResponse>>.Fail_InternalError(
                    message: "An error occurred while retrieving environments"));
            }
        }

        /// <summary>
        /// Get single environment by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<DataWrapper<Dto_EnvironmentResponse>>> GetEnvironment(Guid id)
        {
            try
            {
                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<Dto_EnvironmentResponse>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid)) { return BadRequest(DataWrapper<object>.BadRequest(message: "Invalid user ID format")); }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var environment = await m_dbContext.Environments
                    .Include(e => e.Application)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (environment == null)
                {
                    return NotFound(DataWrapper<Dto_EnvironmentResponse>.NotFound(
                        message: "Environment not found"));
                }

                // Check ownership through application
                if (!isSuperAdmin && environment.Application != null && environment.Application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<Dto_EnvironmentResponse>.Forbidden(
                        message: "You do not have permission to access this environment"));
                }

                var response = new Dto_EnvironmentResponse
                {
                    Id = environment.Id,
                    ApplicationId = environment.ApplicationId,
                    Name = environment.Name,
                    ApiKey = environment.ApiKey,
                    AllowedOrigins = environment.AllowedOrigins,
                    WebhookUrl = environment.WebhookUrl,
                    SuccessResponseUrl = environment.SuccessResponseUrl,
                    PendingResponseUrl = environment.PendingResponseUrl ?? string.Empty,
                    FailureResponseUrl = environment.FailureResponseUrl,
                    IsSandbox = environment.IsSandbox,
                    CreatedAt = environment.CreatedAt,
                    UpdatedAt = environment.UpdatedAt
                };

                return Ok(DataWrapper<Dto_EnvironmentResponse>.Succeed(
                    response,
                    message: "Environment retrieved successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error retrieving environment {id}", id);
                return StatusCode(500, DataWrapper<Dto_EnvironmentResponse>.Fail_InternalError(
                    message: "An error occurred while retrieving the environment"));
            }
        }

        /// <summary>
        /// Create new environment
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DataWrapper<Dto_EnvironmentResponse>>> CreateEnvironment(
            [FromQuery] Guid applicationId,
            [FromBody] Dto_EnvironmentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_EnvironmentResponse>.BadRequest(
                        message: "Validation failed",
                        errors: errors));
                }

                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<Dto_EnvironmentResponse>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid)) { return BadRequest(DataWrapper<object>.BadRequest(message: "Invalid user ID format")); }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                // Check if application exists and user has access
                var application = await m_dbContext.Applications
                    .FirstOrDefaultAsync(a => a.Id == applicationId);

                if (application == null)
                {
                    return NotFound(DataWrapper<Dto_EnvironmentResponse>.NotFound(
                        message: "Application not found"));
                }

                if (!isSuperAdmin && application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<Dto_EnvironmentResponse>.Forbidden(
                        message: "You do not have permission to create environments for this application"));
                }

                // Create environment
                var environment = new Db_Environment
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = applicationId,
                    Name = request.Name,
                    ApiKey = Guid.NewGuid().ToString("N"),
                    AllowedOrigins = request.AllowedOrigins,
                    WebhookUrl = request.WebhookUrl,
                    SuccessResponseUrl = request.SuccessResponseUrl,
                    PendingResponseUrl = NormalizeOptionalUrl(request.PendingResponseUrl),
                    FailureResponseUrl = request.FailureResponseUrl,
                    IsSandbox = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                m_dbContext.Environments.Add(environment);
                await m_dbContext.SaveChangesAsync();

                await m_activityLog.LogAsync(
                    ActivityLogCategory.Creation,
                    $"Created environment: {environment.Name} ({environment.Id}) for application {applicationId}");

                var response = new Dto_EnvironmentResponse
                {
                    Id = environment.Id,
                    ApplicationId = environment.ApplicationId,
                    Name = environment.Name,
                    ApiKey = environment.ApiKey,
                    AllowedOrigins = environment.AllowedOrigins,
                    WebhookUrl = environment.WebhookUrl,
                    SuccessResponseUrl = environment.SuccessResponseUrl,
                    PendingResponseUrl = environment.PendingResponseUrl ?? string.Empty,
                    FailureResponseUrl = environment.FailureResponseUrl,
                    IsSandbox = environment.IsSandbox,
                    CreatedAt = environment.CreatedAt,
                    UpdatedAt = environment.UpdatedAt
                };

                return Ok(DataWrapper<Dto_EnvironmentResponse>.Succeed(
                    response,
                    message: "Environment created successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error creating environment");
                return StatusCode(500, DataWrapper<Dto_EnvironmentResponse>.Fail_InternalError(
                    message: "An error occurred while creating the environment"));
            }
        }

        /// <summary>
        /// Update environment
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<DataWrapper<Dto_EnvironmentResponse>>> UpdateEnvironment(
            Guid id,
            [FromBody] Dto_EnvironmentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_EnvironmentResponse>.BadRequest(
                        message: "Validation failed",
                        errors: errors));
                }

                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<Dto_EnvironmentResponse>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid)) { return BadRequest(DataWrapper<object>.BadRequest(message: "Invalid user ID format")); }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var environment = await m_dbContext.Environments
                    .Include(e => e.Application)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (environment == null)
                {
                    return NotFound(DataWrapper<Dto_EnvironmentResponse>.NotFound(
                        message: "Environment not found"));
                }

                // Check ownership
                if (!isSuperAdmin && environment.Application != null && environment.Application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<Dto_EnvironmentResponse>.Forbidden(
                        message: "You do not have permission to update this environment"));
                }

                // Update environment
                environment.Name = request.Name;
                environment.AllowedOrigins = request.AllowedOrigins;
                environment.WebhookUrl = request.WebhookUrl;
                environment.SuccessResponseUrl = request.SuccessResponseUrl;
                environment.PendingResponseUrl = NormalizeOptionalUrl(request.PendingResponseUrl);
                environment.FailureResponseUrl = request.FailureResponseUrl;
                if (request.IsSandbox.HasValue)
                {
                    environment.IsSandbox = request.IsSandbox.Value;
                }
                environment.UpdatedAt = DateTime.UtcNow;

                await m_dbContext.SaveChangesAsync();

                await m_activityLog.LogAsync(
                    ActivityLogCategory.Modification,
                    $"Updated environment: {environment.Name} ({environment.Id})");

                var response = new Dto_EnvironmentResponse
                {
                    Id = environment.Id,
                    ApplicationId = environment.ApplicationId,
                    Name = environment.Name,
                    ApiKey = environment.ApiKey,
                    AllowedOrigins = environment.AllowedOrigins,
                    WebhookUrl = environment.WebhookUrl,
                    SuccessResponseUrl = environment.SuccessResponseUrl,
                    PendingResponseUrl = environment.PendingResponseUrl ?? string.Empty,
                    FailureResponseUrl = environment.FailureResponseUrl,
                    IsSandbox = environment.IsSandbox,
                    CreatedAt = environment.CreatedAt,
                    UpdatedAt = environment.UpdatedAt
                };

                return Ok(DataWrapper<Dto_EnvironmentResponse>.Succeed(
                    response,
                    message: "Environment updated successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error updating environment {id}", id);
                return StatusCode(500, DataWrapper<Dto_EnvironmentResponse>.Fail_InternalError(
                    message: "An error occurred while updating the environment"));
            }
        }

        /// <summary>
        /// Regenerate API key for environment
        /// </summary>
        [HttpPost("{id}/regenerate-key")]
        public async Task<ActionResult<DataWrapper<Dto_EnvironmentResponse>>> RegenerateApiKey(Guid id)
        {
            try
            {
                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<Dto_EnvironmentResponse>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid)) { return BadRequest(DataWrapper<object>.BadRequest(message: "Invalid user ID format")); }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var environment = await m_dbContext.Environments
                    .Include(e => e.Application)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (environment == null)
                {
                    return NotFound(DataWrapper<Dto_EnvironmentResponse>.NotFound(
                        message: "Environment not found"));
                }

                // Check ownership
                if (!isSuperAdmin && environment.Application != null && environment.Application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<Dto_EnvironmentResponse>.Forbidden(
                        message: "You do not have permission to regenerate API key for this environment"));
                }

                // Regenerate API key
                environment.ApiKey = Guid.NewGuid().ToString("N");
                environment.UpdatedAt = DateTime.UtcNow;

                await m_dbContext.SaveChangesAsync();

                await m_activityLog.LogAsync(
                    ActivityLogCategory.Modification,
                    $"Regenerated API key for environment: {environment.Name} ({environment.Id})");

                var response = new Dto_EnvironmentResponse
                {
                    Id = environment.Id,
                    ApplicationId = environment.ApplicationId,
                    Name = environment.Name,
                    ApiKey = environment.ApiKey,
                    AllowedOrigins = environment.AllowedOrigins,
                    WebhookUrl = environment.WebhookUrl,
                    SuccessResponseUrl = environment.SuccessResponseUrl,
                    PendingResponseUrl = environment.PendingResponseUrl ?? string.Empty,
                    FailureResponseUrl = environment.FailureResponseUrl,
                    IsSandbox = environment.IsSandbox,
                    CreatedAt = environment.CreatedAt,
                    UpdatedAt = environment.UpdatedAt
                };

                return Ok(DataWrapper<Dto_EnvironmentResponse>.Succeed(
                    response,
                    message: "API key regenerated successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error regenerating API key for environment {id}", id);
                return StatusCode(500, DataWrapper<Dto_EnvironmentResponse>.Fail_InternalError(
                    message: "An error occurred while regenerating the API key"));
            }
        }

        /// <summary>
        /// Soft delete environment
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<DataWrapper<bool>>> DeleteEnvironment(Guid id)
        {
            try
            {
                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<bool>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return BadRequest(DataWrapper<bool>.BadRequest(
                        message: "Invalid user ID format"));
                }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var environment = await m_dbContext.Environments
                    .Include(e => e.Application)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (environment == null)
                {
                    return NotFound(DataWrapper<bool>.NotFound(
                        message: "Environment not found"));
                }

                // Check ownership
                if (!isSuperAdmin && environment.Application != null && environment.Application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<bool>.Forbidden(
                        message: "You do not have permission to delete this environment"));
                }

                // Soft delete environment
                environment.IsDeleted = true;
                environment.DeletedAt = DateTime.UtcNow;

                await m_dbContext.SaveChangesAsync();

                await m_activityLog.LogAsync(
                    ActivityLogCategory.Deletion,
                    $"Deleted environment: {environment.Name} ({environment.Id})");

                return Ok(DataWrapper<bool>.Succeed(
                    true,
                    message: "Environment deleted successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error deleting environment {id}", id);
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An error occurred while deleting the environment"));
            }
        }

        private static string? NormalizeOptionalUrl(string? url)
        {
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
    }
}
