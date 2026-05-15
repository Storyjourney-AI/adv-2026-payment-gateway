using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Server.ActivityLog.Models.Dtos;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;

namespace PaymentGateway.Server.ActivityLog.Controllers
{
    [ApiController]
    [Route("api/activity-log")]
    [Authorize(Policy = "RequireAdmin")]
    public class ActivityLogController : ControllerBase
    {
        private readonly AppDbContext m_dbContext;
        private readonly ILogger<ActivityLogController> m_logger;

        public ActivityLogController(
            AppDbContext dbContext,
            ILogger<ActivityLogController> logger)
        {
            m_dbContext = dbContext;
            m_logger = logger;
        }

        /// <summary>
        /// Get paginated activity logs with optional filters
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<DataWrapper<PaginationWrapper<Dto_ActivityLogItem>>>> GetActivityLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? category = null)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var query = m_dbContext.ActivityLogs.AsQueryable();

                // Filter by category
                if (!string.IsNullOrWhiteSpace(category))
                {
                    query = query.Where(l => l.Category == category);
                }

                // Search by user email or action description
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(l =>
                        (l.UserEmail != null && l.UserEmail.Contains(search)) ||
                        l.Action.Contains(search));
                }

                var totalItems = await query.CountAsync();

                var logs = await query
                    .OrderByDescending(l => l.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new Dto_ActivityLogItem
                    {
                        Id = l.Id,
                        UserId = l.UserId,
                        UserEmail = l.UserEmail,
                        SessionToken = l.SessionToken,
                        Category = l.Category,
                        Action = l.Action,
                        Timestamp = l.Timestamp
                    })
                    .ToListAsync();

                var paginationWrapper = new PaginationWrapper<Dto_ActivityLogItem>
                {
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                    Items = logs
                };

                return Ok(DataWrapper<PaginationWrapper<Dto_ActivityLogItem>>.Succeed(
                    paginationWrapper,
                    message: "Activity logs retrieved successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error retrieving activity logs");
                return StatusCode(500, DataWrapper<PaginationWrapper<Dto_ActivityLogItem>>.Fail_InternalError(
                    message: "An error occurred while retrieving activity logs"));
            }
        }
    }
}
