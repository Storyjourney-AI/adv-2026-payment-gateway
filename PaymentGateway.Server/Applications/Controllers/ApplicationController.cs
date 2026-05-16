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
    public class ApplicationController : ControllerBase
    {
        private readonly AppDbContext m_dbContext;
        private readonly UserManager<Db_ApplicationUser> m_userManager;
        private readonly ActivityLogService m_activityLog;
        private readonly ILogger<ApplicationController> m_logger;

        public ApplicationController(
            AppDbContext dbContext,
            UserManager<Db_ApplicationUser> userManager,
            ActivityLogService activityLog,
            ILogger<ApplicationController> logger)
        {
            m_dbContext = dbContext;
            m_userManager = userManager;
            m_activityLog = activityLog;
            m_logger = logger;
        }

        /// <summary>
        /// Get paginated list of applications
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<DataWrapper<PaginationWrapper<Dto_ApplicationListItem>>>> GetApplications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                var userIdClaim = User.FindFirst("sub_id")?.Value;
                if (userIdClaim == null)
                {
                    return Unauthorized(DataWrapper<PaginationWrapper<Dto_ApplicationListItem>>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userIdClaim, out var userGuid))
                {
                    return BadRequest(DataWrapper<PaginationWrapper<Dto_ApplicationListItem>>.BadRequest(
                        message: "Invalid user ID format"));
                }

                var user = await m_userManager.FindByIdAsync(userIdClaim);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                // Build query
                var query = m_dbContext.Applications
                    .Include(a => a.Environments)
                    .AsQueryable();

                // Filter by user if not Super Admin
                if (!isSuperAdmin)
                {
                    query = query.Where(a => a.UserId == userGuid);
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(a => a.Name.Contains(search) || (a.Description != null && a.Description.Contains(search)));
                }

                // Get total count
                var totalItems = await query.CountAsync();

                // Apply pagination
                var applications = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => new Dto_ApplicationListItem
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Description = a.Description,
                        CreatedAt = a.CreatedAt,
                        EnvironmentCount = a.Environments.Count(e => !e.IsDeleted)
                    })
                    .ToListAsync();

                var paginationWrapper = new PaginationWrapper<Dto_ApplicationListItem>
                {
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                    Items = applications
                };

                return Ok(DataWrapper<PaginationWrapper<Dto_ApplicationListItem>>.Succeed(
                    paginationWrapper,
                    message: "Applications retrieved successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error retrieving applications");
                return StatusCode(500, DataWrapper<PaginationWrapper<Dto_ApplicationListItem>>.Fail_InternalError(
                    message: "An error occurred while retrieving applications"));
            }
        }

        /// <summary>
        /// Get single application by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<DataWrapper<Dto_ApplicationResponse>>> GetApplication(Guid id)
        {
            try
            {
                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<Dto_ApplicationResponse>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return BadRequest(DataWrapper<Dto_ApplicationResponse>.BadRequest(
                        message: "Invalid user ID format"));
                }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var application = await m_dbContext.Applications
                    .Include(a => a.Environments.Where(e => !e.IsDeleted))
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    return NotFound(DataWrapper<Dto_ApplicationResponse>.NotFound(
                        message: "Application not found"));
                }

                // Check ownership
                if (!isSuperAdmin && application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<Dto_ApplicationResponse>.Forbidden(
                        message: "You do not have permission to access this application"));
                }

                var response = new Dto_ApplicationResponse
                {
                    Id = application.Id,
                    Name = application.Name,
                    Description = application.Description,
                    UserId = application.UserId,
                    CreatedAt = application.CreatedAt,
                    UpdatedAt = application.UpdatedAt,
                    Environments = application.Environments.Select(e => new Dto_EnvironmentResponse
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
                    }).ToList()
                };

                return Ok(DataWrapper<Dto_ApplicationResponse>.Succeed(
                    response,
                    message: "Application retrieved successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error retrieving application {id}", id);
                return StatusCode(500, DataWrapper<Dto_ApplicationResponse>.Fail_InternalError(
                    message: "An error occurred while retrieving the application"));
            }
        }

        /// <summary>
        /// Create new application
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DataWrapper<Dto_ApplicationResponse>>> CreateApplication(
            [FromBody] Dto_ApplicationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_ApplicationResponse>.BadRequest(
                        message: "Validation failed",
                        errors: errors));
                }

                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<Dto_ApplicationResponse>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return BadRequest(DataWrapper<Dto_ApplicationResponse>.BadRequest(
                        message: "Invalid user ID format"));
                }

                // Create application
                var application = new Db_Application
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    UserId = userGuid,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                // Create default environments (staging and production)
                var stagingEnv = new Db_Environment
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = application.Id,
                    Name = "staging",
                    ApiKey = Guid.NewGuid().ToString("N"),
                    AllowedOrigins = "*",
                    SuccessResponseUrl = "https://payment.advine.id/payment/success",
                    PendingResponseUrl = null,
                    FailureResponseUrl = "https://payment.advine.id/payment/failed",
                    IsSandbox = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                var productionEnv = new Db_Environment
                {
                    Id = Guid.NewGuid(),
                    ApplicationId = application.Id,
                    Name = "production",
                    ApiKey = Guid.NewGuid().ToString("N"),
                    AllowedOrigins = "*",
                    SuccessResponseUrl = "https://payment.advine.id/payment/success",
                    PendingResponseUrl = null,
                    FailureResponseUrl = "https://payment.advine.id/payment/failed",
                    IsSandbox = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                application.Environments.Add(stagingEnv);
                application.Environments.Add(productionEnv);

                m_dbContext.Applications.Add(application);
                await m_dbContext.SaveChangesAsync();

                await m_activityLog.LogAsync(
                    ActivityLogCategory.Creation,
                    $"Created application: {application.Name} ({application.Id})");

                var response = new Dto_ApplicationResponse
                {
                    Id = application.Id,
                    Name = application.Name,
                    Description = application.Description,
                    UserId = application.UserId,
                    CreatedAt = application.CreatedAt,
                    UpdatedAt = application.UpdatedAt,
                    Environments = application.Environments.Select(e => new Dto_EnvironmentResponse
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
                    }).ToList()
                };

                return Ok(DataWrapper<Dto_ApplicationResponse>.Succeed(
                    response,
                    message: "Application created successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error creating application");
                return StatusCode(500, DataWrapper<Dto_ApplicationResponse>.Fail_InternalError(
                    message: "An error occurred while creating the application"));
            }
        }

        /// <summary>
        /// Update application
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<DataWrapper<Dto_ApplicationResponse>>> UpdateApplication(
            Guid id,
            [FromBody] Dto_ApplicationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return BadRequest(DataWrapper<Dto_ApplicationResponse>.BadRequest(
                        message: "Validation failed",
                        errors: errors));
                }

                var userId = User.FindFirst("sub_id")?.Value;
                if (userId == null)
                {
                    return Unauthorized(DataWrapper<Dto_ApplicationResponse>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userId, out var userGuid))
                {
                    return BadRequest(DataWrapper<Dto_ApplicationResponse>.BadRequest(
                        message: "Invalid user ID format"));
                }
                var user = await m_userManager.FindByIdAsync(userId);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var application = await m_dbContext.Applications
                    .Include(a => a.Environments.Where(e => !e.IsDeleted))
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    return NotFound(DataWrapper<Dto_ApplicationResponse>.NotFound(
                        message: "Application not found"));
                }

                // Check ownership
                if (!isSuperAdmin && application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<Dto_ApplicationResponse>.Forbidden(
                        message: "You do not have permission to update this application"));
                }

                // Update application
                application.Name = request.Name;
                application.Description = request.Description;
                application.UpdatedAt = DateTime.UtcNow;

                await m_dbContext.SaveChangesAsync();

                await m_activityLog.LogAsync(
                    ActivityLogCategory.Modification,
                    $"Updated application: {application.Name} ({application.Id})");

                var response = new Dto_ApplicationResponse
                {
                    Id = application.Id,
                    Name = application.Name,
                    Description = application.Description,
                    UserId = application.UserId,
                    CreatedAt = application.CreatedAt,
                    UpdatedAt = application.UpdatedAt,
                    Environments = application.Environments.Select(e => new Dto_EnvironmentResponse
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
                    }).ToList()
                };

                return Ok(DataWrapper<Dto_ApplicationResponse>.Succeed(
                    response,
                    message: "Application updated successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error updating application {id}", id);
                return StatusCode(500, DataWrapper<Dto_ApplicationResponse>.Fail_InternalError(
                    message: "An error occurred while updating the application"));
            }
        }

        /// <summary>
        /// Soft delete application
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<DataWrapper<bool>>> DeleteApplication(Guid id)
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

                var application = await m_dbContext.Applications
                    .Include(a => a.Environments)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    return NotFound(DataWrapper<bool>.NotFound(
                        message: "Application not found"));
                }

                // Check ownership
                if (!isSuperAdmin && application.UserId != userGuid)
                {
                    return StatusCode(403, DataWrapper<bool>.Forbidden(
                        message: "You do not have permission to delete this application"));
                }

                // Soft delete application and all environments
                application.IsDeleted = true;
                application.DeletedAt = DateTime.UtcNow;

                foreach (var env in application.Environments)
                {
                    env.IsDeleted = true;
                    env.DeletedAt = DateTime.UtcNow;
                }

                await m_dbContext.SaveChangesAsync();

                await m_activityLog.LogAsync(
                    ActivityLogCategory.Deletion,
                    $"Deleted application: {application.Name} ({application.Id})");

                return Ok(DataWrapper<bool>.Succeed(
                    true,
                    message: "Application deleted successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error deleting application {id}", id);
                return StatusCode(500, DataWrapper<bool>.Fail_InternalError(
                    message: "An error occurred while deleting the application"));
            }
        }
    }
}
