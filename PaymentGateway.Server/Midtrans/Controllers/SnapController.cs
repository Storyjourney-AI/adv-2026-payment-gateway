using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.ActivityLog.Services;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dtos;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Security.Operations;
using PaymentGateway.Server.Security.RateLimiting;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentGateway.Server.Midtrans.Controllers
{
    [ApiController]
    [Route("api/snap")]
    public class SnapController : ControllerBase
    {
        private const string MidtransSandboxUrl = "https://app.sandbox.midtrans.com/snap/v1/transactions";
        private const string MidtransProductionUrl = "https://app.midtrans.com/snap/v1/transactions";
        private const string MidtransSandboxApiUrl = "https://api.sandbox.midtrans.com/v2";
        private const string MidtransProductionApiUrl = "https://api.midtrans.com/v2";

        private static readonly JsonSerializerOptions s_midtransJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly AppDbContext m_dbContext;
        private readonly MidtransOptions m_midtransOptions;
        private readonly IHttpClientFactory m_httpClientFactory;
        private readonly ILogger<SnapController> m_logger;
        private readonly UserManager<Db_ApplicationUser> m_userManager;
        private readonly ActivityLogService m_activityLogService;
        private readonly ISecurityMetricsService m_securityMetricsService;
        private readonly IMidtransTransactionReconciliationService m_midtransTransactionReconciliationService;

        public SnapController(
            AppDbContext dbContext,
            IOptions<MidtransOptions> midtransOptions,
            IHttpClientFactory httpClientFactory,
            ILogger<SnapController> logger,
            UserManager<Db_ApplicationUser> userManager,
            ActivityLogService activityLogService,
            ISecurityMetricsService securityMetricsService,
            IMidtransTransactionReconciliationService midtransTransactionReconciliationService)
        {
            m_dbContext = dbContext;
            m_midtransOptions = midtransOptions.Value;
            m_httpClientFactory = httpClientFactory;
            m_logger = logger;
            m_userManager = userManager;
            m_activityLogService = activityLogService;
            m_securityMetricsService = securityMetricsService;
            m_midtransTransactionReconciliationService = midtransTransactionReconciliationService;
        }

        /// <summary>
        /// Create a Midtrans Snap token. The gateway selects Sandbox or Production automatically
        /// based on the <c>IsSandbox</c> flag of the environment matched by the supplied API key.
        /// Child apps authenticate using X-Api-Key header.
        /// </summary>
        [HttpPost("token")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.SnapPublicModerate)]
        public async Task<IActionResult> CreateToken([FromBody] Dto_SnapTokenRequest request)
        {
            var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "X-Api-Key header is required."));
            }

            var environment = await m_dbContext.Environments
                .FirstOrDefaultAsync(e => e.ApiKey == apiKey);

            if (environment == null)
            {
                m_securityMetricsService.Increment("x_api_key_invalid_total", "/api/snap/token");
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "Invalid API key."));
            }

            var envOptions = environment.IsSandbox ? m_midtransOptions.Sandbox : m_midtransOptions.Production;
            var midtransUrl = environment.IsSandbox ? MidtransSandboxUrl : MidtransProductionUrl;
            var midtransEnv = environment.IsSandbox ? "sandbox" : "production";
            var webhookUrl = environment.IsSandbox
                ? $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/sandbox/payment"
                : $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/payment";

            return await CreateTokenAsync(envOptions, midtransUrl, webhookUrl, request, midtransEnv, environment);
        }

        /// <summary>
        /// Create a Midtrans Snap token against the Sandbox environment.
        /// Child apps authenticate using X-Api-Key header.
        /// </summary>
        /// <remarks>
        /// Deprecated. Use POST /api/snap/token instead. This endpoint will be removed in a future version.
        /// </remarks>
        [HttpPost("sandbox/token")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.SnapPublicModerate)]
        public async Task<IActionResult> CreateSandboxToken([FromBody] Dto_SnapTokenRequest request)
        {
            var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "X-Api-Key header is required."));
            }

            var environment = await m_dbContext.Environments
                .FirstOrDefaultAsync(e => e.ApiKey == apiKey);

            if (environment == null)
            {
                m_securityMetricsService.Increment("x_api_key_invalid_total", "/api/snap/sandbox/token");
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "Invalid API key."));
            }

            if (!environment.IsSandbox)
            {
                return BadRequest(DataWrapper<object>.BadRequest(
                    message: "This API key belongs to a production environment. Use /api/snap/token instead."));
            }

            var webhookUrl = $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/sandbox/payment";
            return await CreateTokenAsync(m_midtransOptions.Sandbox, MidtransSandboxUrl, webhookUrl, request, "sandbox", environment);
        }

        /// <summary>
        /// Create a Midtrans Snap token against the Production environment.
        /// Child apps authenticate using X-Api-Key header.
        /// </summary>
        /// <remarks>
        /// Deprecated. Use POST /api/snap/token instead. This endpoint will be removed in a future version.
        /// </remarks>
        [HttpPost("production/token")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.SnapPublicModerate)]
        public async Task<IActionResult> CreateProductionToken([FromBody] Dto_SnapTokenRequest request)
        {
            var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "X-Api-Key header is required."));
            }

            var environment = await m_dbContext.Environments
                .FirstOrDefaultAsync(e => e.ApiKey == apiKey);

            if (environment == null)
            {
                m_securityMetricsService.Increment("x_api_key_invalid_total", "/api/snap/production/token");
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "Invalid API key."));
            }

            if (environment.IsSandbox)
            {
                return BadRequest(DataWrapper<object>.BadRequest(
                    message: "This API key belongs to a sandbox environment. Use /api/snap/token instead."));
            }

            var webhookUrl = $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/payment";
            return await CreateTokenAsync(m_midtransOptions.Production, MidtransProductionUrl, webhookUrl, request, "production", environment);
        }

        /// <summary>
        /// Trigger a test purchase for an environment using a fixed dummy payload.
        /// Requires JWT authentication. Selects Sandbox or Production based on the environment's IsSandbox flag.
        /// </summary>
        [HttpPost("test/{environmentId}")]
        [Authorize(Policy = "RequireUser")]
        public async Task<IActionResult> TestPurchase(Guid environmentId)
        {
            var userId = User.FindFirst("sub_id")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "User identity could not be resolved."));
            }

            // 1. Resolve environment
            var environment = await m_dbContext.Environments
                .Include(e => e.Application)
                .FirstOrDefaultAsync(e => e.Id == environmentId && !e.IsDeleted && e.Application != null && !e.Application.IsDeleted);

            if (environment == null || environment.Application == null)
            {
                return NotFound(DataWrapper<object>.NotFound(
                    message: "Environment not found."));
            }

            // 2. Ownership check
            var user = await m_userManager.FindByIdAsync(userId);
            var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

            if (!isSuperAdmin && environment.Application.UserId.ToString() != userId)
            {
                return StatusCode(403, DataWrapper<object>.Forbidden(
                    message: "You do not have permission to test this environment."));
            }

            // 3. Resolve Midtrans target
            var envOptions = environment.IsSandbox ? m_midtransOptions.Sandbox : m_midtransOptions.Production;
            var midtransUrl = environment.IsSandbox ? MidtransSandboxUrl : MidtransProductionUrl;
            var midtransEnv = environment.IsSandbox ? "sandbox" : "production";
            var webhookCallbackUrl = environment.IsSandbox
                ? $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/sandbox/payment"
                : $"{m_midtransOptions.BaseUrl.TrimEnd('/')}/api/midtrans/payment";

            // 4. Check environment enabled
            if (!envOptions.IsEnabled)
            {
                return StatusCode(503, DataWrapper<object>.Unavailable(
                    message: $"The {midtransEnv} payment environment is currently disabled."));
            }

            // 5. Build dummy request
            var callerOrderId = "test_" + Guid.NewGuid().ToString("N")[..8];
            var dummyRequest = new Dto_SnapTokenRequest
            {
                OrderId = callerOrderId,
                GrossAmount = 30000,
                ItemDetails = new List<SnapItemDetail>
                {
                    new() { Id = "test_item_1", Name = "test_something_1", Price = 10000, Quantity = 1 },
                    new() { Id = "test_item_2", Name = "test_something_2", Price = 10000, Quantity = 1 },
                    new() { Id = "test_item_3", Name = "test_something_3", Price = 10000, Quantity = 1 },
                }
            };

            m_logger.LogInformation("Test purchase initiated for environment {EnvId} by user {UserId}", environmentId, userId);

            return await CreateTokenAsync(envOptions, midtransUrl, webhookCallbackUrl, dummyRequest, midtransEnv, environment);
        }

        /// <summary>
        /// Returns the live Midtrans payment status for a given CallerOrderId, merged with the
        /// gateway's stored DB record. Authenticated by X-Api-Key header.
        /// </summary>
        [HttpGet("status/{orderId}")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.SnapStatusModerate)]
        public async Task<IActionResult> GetPaymentStatus(string orderId)
        {
            // 1. Validate X-Api-Key
            var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "X-Api-Key header is required."));
            }

            var environment = await m_dbContext.Environments
                .FirstOrDefaultAsync(e => e.ApiKey == apiKey && !e.IsDeleted);
            if (environment == null)
            {
                m_securityMetricsService.Increment("x_api_key_invalid_total", "/api/snap/status/{orderId}");
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "Invalid API key."));
            }

            // 2. Look up transaction scoped to this environment
            var transaction = await m_dbContext.SnapTransactions
                .FirstOrDefaultAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == orderId);
            if (transaction == null)
            {
                return NotFound(DataWrapper<object>.NotFound(
                    message: $"Transaction with orderId '{orderId}' not found."));
            }

            try
            {
                var reconciliationResult = await m_midtransTransactionReconciliationService
                    .ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId, HttpContext.RequestAborted);

                if (reconciliationResult == null)
                {
                    return NotFound(DataWrapper<object>.NotFound(
                        message: $"Transaction with orderId '{orderId}' not found."));
                }

                return Ok(DataWrapper<Dto_SnapStatusResponse>.Succeed(
                    reconciliationResult.StatusResponse, message: "Payment status retrieved successfully."));
            }
            catch (MidtransStatusVerificationException ex)
            {
                m_logger.LogWarning(ex, "Midtrans status API error for order {OrderId}", orderId);
                return StatusCode(502, DataWrapper<object>.Fail(
                    System.Net.HttpStatusCode.BadGateway,
                    message: ex.Message));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Unexpected error calling Midtrans status API for order {OrderId}", orderId);
                return StatusCode(502, DataWrapper<object>.Fail(
                    System.Net.HttpStatusCode.BadGateway,
                    message: "Failed to communicate with Midtrans. Please try again."));
            }
        }

        /// <summary>
        /// Cancels a pending Midtrans payment by CallerOrderId. Updates TransactionStatus in DB on success.
        /// Authenticated by X-Api-Key header.
        /// </summary>
        [HttpPost("cancel/{orderId}")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.SnapCancelModerate)]
        public async Task<IActionResult> CancelPayment(string orderId)
        {
            // 1. Validate X-Api-Key
            var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "X-Api-Key header is required."));
            }

            var environment = await m_dbContext.Environments
                .FirstOrDefaultAsync(e => e.ApiKey == apiKey && !e.IsDeleted);
            if (environment == null)
            {
                m_securityMetricsService.Increment("x_api_key_invalid_total", "/api/snap/cancel/{orderId}");
                return Unauthorized(DataWrapper<object>.Unauthorized(
                    message: "Invalid API key."));
            }

            // 2. Look up transaction scoped to this environment
            var transaction = await m_dbContext.SnapTransactions
                .FirstOrDefaultAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == orderId);
            if (transaction == null)
            {
                return NotFound(DataWrapper<object>.NotFound(
                    message: $"Transaction with orderId '{orderId}' not found."));
            }

            // 3. Resolve Midtrans API base URL and server key
            var envOptions = environment.IsSandbox ? m_midtransOptions.Sandbox : m_midtransOptions.Production;
            var baseUrl = environment.IsSandbox ? MidtransSandboxApiUrl : MidtransProductionApiUrl;
            var cancelUrl = $"{baseUrl}/{transaction.MidtransOrderId}/cancel";
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(envOptions.ServerKey + ":"));

            // 4. Call Midtrans cancel API
            try
            {
                var client = m_httpClientFactory.CreateClient("midtrans");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, cancelUrl);
                httpRequest.Headers.Add("Authorization", $"Basic {authValue}");

                var httpResponse = await client.SendAsync(httpRequest);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    m_logger.LogWarning("Midtrans cancel API error for order {OrderId}. Status: {Status}, Body: {Body}",
                        orderId, httpResponse.StatusCode, responseBody);

                    string? midtransMessage = null;
                    try
                    {
                        using var errDoc = JsonDocument.Parse(responseBody);
                        errDoc.RootElement.TryGetProperty("status_message", out var msgEl);
                        midtransMessage = msgEl.ValueKind == JsonValueKind.String ? msgEl.GetString() : null;
                    }
                    catch { /* ignore parse failure */ }

                    // 412 Precondition Failed = Midtrans business rejection (terminal state, not cancellable) → 422.
                    // Other 4xx (e.g. 401 wrong key, 404 order not found at Midtrans) are gateway-side failures → 502.
                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                    {
                        return UnprocessableEntity(DataWrapper<object>.Unprocessable(
                            message: midtransMessage ?? "Transaction cannot be cancelled in its current state."));
                    }

                    return StatusCode(502, DataWrapper<object>.Fail(
                        System.Net.HttpStatusCode.BadGateway,
                        message: midtransMessage ?? "Midtrans cancel API returned an error."));
                }

                // 5. Update DB record with cancelled status
                using var doc = JsonDocument.Parse(responseBody);

                if (doc.RootElement.TryGetProperty("transaction_status", out var txStatusEl)
                    && txStatusEl.ValueKind == JsonValueKind.String)
                {
                    transaction.TransactionStatus = txStatusEl.GetString();
                }

                transaction.UpdatedAt = DateTime.UtcNow;
                await m_dbContext.SaveChangesAsync();

                var reconciliationResult = await m_midtransTransactionReconciliationService
                    .ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId, HttpContext.RequestAborted);

                if (reconciliationResult == null)
                {
                    return StatusCode(502, DataWrapper<object>.Fail(
                        System.Net.HttpStatusCode.BadGateway,
                        message: "Failed to verify cancelled payment status with Midtrans."));
                }

                return Ok(DataWrapper<Dto_SnapStatusResponse>.Succeed(
                    reconciliationResult.StatusResponse, message: "Payment cancelled successfully."));
            }
            catch (MidtransStatusVerificationException ex)
            {
                m_logger.LogWarning(ex, "Midtrans status verification failed after cancel for order {OrderId}", orderId);
                return StatusCode(502, DataWrapper<object>.Fail(
                    System.Net.HttpStatusCode.BadGateway,
                    message: ex.Message));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Unexpected error calling Midtrans cancel API for order {OrderId}", orderId);
                return StatusCode(502, DataWrapper<object>.Fail(
                    System.Net.HttpStatusCode.BadGateway,
                    message: "Failed to communicate with Midtrans. Please try again."));
            }
        }

        /// <summary>
        /// Finish redirect for production — Midtrans sends the customer here after a completed payment.
        /// GET /api/midtrans/snap/callback
        /// </summary>
        [HttpGet("/api/midtrans/snap/callback")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.CallbackLenient)]
        public async Task<IActionResult> Callback([FromQuery(Name = "order_id")] string orderId)
        {
            return await HandleCallbackRedirectAsync(orderId);
        }

        /// <summary>
        /// Finish redirect for sandbox — Midtrans sends the customer here after a completed payment.
        /// GET /api/midtrans/sandbox/snap/callback
        /// </summary>
        [HttpGet("/api/midtrans/sandbox/snap/callback")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.CallbackLenient)]
        public async Task<IActionResult> SandboxCallback([FromQuery(Name = "order_id")] string orderId)
        {
            return await HandleCallbackRedirectAsync(orderId);
        }

        /// <summary>
        /// Unfinish redirect for production — Midtrans sends the customer here when payment remains pending or is abandoned before completion.
        /// GET /api/midtrans/snap/callback/unfinish
        /// </summary>
        [HttpGet("/api/midtrans/snap/callback/unfinish")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.CallbackLenient)]
        public async Task<IActionResult> CallbackUnfinish([FromQuery(Name = "order_id")] string orderId)
        {
            return await HandleCallbackRedirectAsync(orderId);
        }

        /// <summary>
        /// Unfinish redirect for sandbox — Midtrans sends the customer here when payment remains pending or is abandoned before completion.
        /// GET /api/midtrans/sandbox/snap/callback/unfinish
        /// </summary>
        [HttpGet("/api/midtrans/sandbox/snap/callback/unfinish")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.CallbackLenient)]
        public async Task<IActionResult> SandboxCallbackUnfinish([FromQuery(Name = "order_id")] string orderId)
        {
            return await HandleCallbackRedirectAsync(orderId);
        }

        /// <summary>
        /// Error redirect for production — Midtrans sends the customer here on payment error or abandonment.
        /// GET /api/midtrans/snap/callback/error
        /// </summary>
        [HttpGet("/api/midtrans/snap/callback/error")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.CallbackLenient)]
        public async Task<IActionResult> CallbackError([FromQuery(Name = "order_id")] string orderId)
        {
            return await HandleCallbackRedirectAsync(orderId);
        }

        /// <summary>
        /// Error redirect for sandbox — Midtrans sends the customer here on payment error or abandonment.
        /// GET /api/midtrans/sandbox/snap/callback/error
        /// </summary>
        [HttpGet("/api/midtrans/sandbox/snap/callback/error")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicyNames.CallbackLenient)]
        public async Task<IActionResult> SandboxCallbackError([FromQuery(Name = "order_id")] string orderId)
        {
            return await HandleCallbackRedirectAsync(orderId);
        }

        private async Task<IActionResult> HandleCallbackRedirectAsync(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                return BadRequest("Missing order_id");

            MidtransTransactionReconciliationResult? reconciliationResult;
            try
            {
                reconciliationResult = await m_midtransTransactionReconciliationService
                    .ReconcileByMidtransOrderIdAsync(orderId, HttpContext.RequestAborted);
            }
            catch (MidtransStatusVerificationException ex)
            {
                m_logger.LogWarning(ex, "Failed to verify Midtrans browser callback for order_id: {OrderId}", orderId);

                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return UnprocessableEntity("Unable to verify payment with Midtrans.");
                }

                return StatusCode(502, "Failed to verify payment with Midtrans.");
            }

            if (reconciliationResult?.Environment == null)
            {
                m_logger.LogWarning("Snap callback received for unknown order_id: {OrderId}", orderId);
                return NotFound("Transaction not found");
            }

            var env = reconciliationResult.Environment;
            var targetUrl = ResolveBrowserCallbackUrl(env, reconciliationResult.RedirectKind);

            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                m_logger.LogWarning(
                    "No redirect URL configured for environment {EnvId} (order {OrderId}, redirect kind {RedirectKind})",
                    env.Id,
                    orderId,
                    reconciliationResult.RedirectKind);
                return Ok("Payment processed. Please return to the application.");
            }

            return Redirect(BuildVerifiedRedirectUrl(targetUrl, reconciliationResult));
        }

        internal static string? ResolveBrowserCallbackUrl(Db_Environment environment, MidtransRedirectKind redirectKind)
        {
            return redirectKind switch
            {
                MidtransRedirectKind.Success => environment.SuccessResponseUrl,
                MidtransRedirectKind.Pending when !string.IsNullOrWhiteSpace(environment.PendingResponseUrl) => environment.PendingResponseUrl,
                MidtransRedirectKind.Pending => environment.FailureResponseUrl,
                _ => environment.FailureResponseUrl
            };
        }

        private static string BuildVerifiedRedirectUrl(
            string targetUrl,
            MidtransTransactionReconciliationResult reconciliationResult)
        {
            var query = new Dictionary<string, string?>
            {
                ["order_id"] = reconciliationResult.Transaction.MidtransOrderId,
                ["caller_order_id"] = reconciliationResult.Transaction.CallerOrderId,
                ["status_code"] = reconciliationResult.VerifiedStatus.StatusCode,
                ["transaction_status"] = reconciliationResult.VerifiedStatus.TransactionStatus,
                ["fraud_status"] = reconciliationResult.VerifiedStatus.FraudStatus,
                ["payment_type"] = reconciliationResult.VerifiedStatus.PaymentType,
                ["transaction_id"] = reconciliationResult.VerifiedStatus.TransactionId,
                ["redirect_kind"] = reconciliationResult.RedirectKind.ToString().ToLowerInvariant(),
                ["verified"] = "true"
            };

            var filteredQuery = query
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                .ToDictionary(entry => entry.Key, entry => entry.Value!);

            return QueryHelpers.AddQueryString(targetUrl, filteredQuery);
        }

        private async Task<IActionResult> CreateTokenAsync(
            MidtransEnvironmentOptions envOptions,
            string midtransUrl,
            string webhookCallbackUrl,
            Dto_SnapTokenRequest request,
            string midtransEnv,
            Db_Environment environment)
        {
            // 1. Check environment is enabled
            if (!envOptions.IsEnabled)
            {
                return StatusCode(503, DataWrapper<object>.Unavailable(
                    message: $"The {midtransEnv} payment environment is currently disabled."));
            }

            // 2. Validate request body
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(DataWrapper<object>.BadRequest(
                    message: "Validation failed.",
                    errors: errors));
            }

            // 4. Build the Midtrans order_id: first 8 chars of env Guid (no dashes) + "_" + caller's OrderId
            var midtransOrderId = environment.Id.ToString("N")[..8] + "_" + request.OrderId;

            // 4b. Guard against duplicate: same caller OrderId + same environment
            var duplicate = await m_dbContext.SnapTransactions
                .AnyAsync(t => t.EnvironmentId == environment.Id && t.CallerOrderId == request.OrderId);
            if (duplicate)
            {
                return Conflict(DataWrapper<object>.Conflict(
                    message: $"A transaction with OrderId '{request.OrderId}' already exists for this environment."));
            }

            // 5. Insert Snap transaction log
            var snapTransaction = new Db_SnapTransaction
            {
                Id = Guid.NewGuid(),
                EnvironmentId = environment.Id,
                MidtransOrderId = midtransOrderId,
                CallerOrderId = request.OrderId,
                GrossAmount = request.GrossAmount,
                MidtransEnv = midtransEnv,
                TransactionStatus = "pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            m_dbContext.SnapTransactions.Add(snapTransaction);

            try
            {
                await m_dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                m_logger.LogWarning(
                    "Concurrent duplicate detected for OrderId '{OrderId}' in environment {EnvId}. " +
                    "The DB unique constraint prevented a duplicate insert.",
                    request.OrderId, environment.Id);

                return Conflict(DataWrapper<object>.Conflict(
                    message: $"A transaction with OrderId '{request.OrderId}' already exists for this environment."));
            }

            // 6. Build Midtrans request body
            var midtransBody = new MidtransSnapRequest
            {
                TransactionDetails = new MidtransTransactionDetails
                {
                    OrderId = midtransOrderId,
                    GrossAmount = request.GrossAmount
                },
                CustomerDetails = request.CustomerDetails != null ? new MidtransCustomerDetails
                {
                    FirstName = request.CustomerDetails.FirstName,
                    LastName = request.CustomerDetails.LastName,
                    Email = request.CustomerDetails.Email,
                    Phone = request.CustomerDetails.Phone
                } : null,
                ItemDetails = request.ItemDetails?.Select(i => new MidtransItemDetail
                {
                    Id = i.Id,
                    Price = i.Price,
                    Quantity = i.Quantity,
                    Name = i.Name
                }).ToList()
            };

            var bodyJson = JsonSerializer.Serialize(midtransBody, s_midtransJsonOptions);

            // 7. Call Midtrans Snap API
            try
            {
                var client = m_httpClientFactory.CreateClient("midtrans");
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(envOptions.ServerKey + ":"));

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, midtransUrl);
                httpRequest.Headers.Add("Authorization", $"Basic {authValue}");
                if (Uri.TryCreate(webhookCallbackUrl, UriKind.Absolute, out _))
                {
                    // Force Midtrans notification delivery to this gateway endpoint.
                    httpRequest.Headers.Add("X-Override-Notification", webhookCallbackUrl);
                }
                else
                {
                    m_logger.LogWarning(
                        "Skipping X-Override-Notification for order {OrderId} because webhook callback URL is invalid: {WebhookCallbackUrl}",
                        midtransOrderId,
                        webhookCallbackUrl);
                }
                httpRequest.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

                var httpResponse = await client.SendAsync(httpRequest);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    m_logger.LogError("Midtrans Snap API error. Status: {Status}, Body: {Body}",
                        httpResponse.StatusCode, responseBody);

                    await m_activityLogService.LogAsync("Snap", $"Snap token creation failed for OrderId '{request.OrderId}'. Midtrans returned {httpResponse.StatusCode}.");

                    snapTransaction.TransactionStatus = "error";
                    snapTransaction.UpdatedAt = DateTime.UtcNow;
                    await m_dbContext.SaveChangesAsync();

                    return StatusCode(502, DataWrapper<object>.Fail_InternalError(
                        message: "Midtrans API returned an error. Please try again."));
                }

                // 8. Parse and return Midtrans response
                using var doc = JsonDocument.Parse(responseBody);
                var token = doc.RootElement.GetProperty("token").GetString() ?? string.Empty;
                var redirectUrl = doc.RootElement.GetProperty("redirect_url").GetString() ?? string.Empty;

                return Ok(DataWrapper<Dto_SnapTokenResponse>.Succeed(new Dto_SnapTokenResponse
                {
                    Token = token,
                    RedirectUrl = redirectUrl
                }, message: "Snap token created successfully."));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Unexpected error calling Midtrans Snap API for order {OrderId}", midtransOrderId);

                await m_activityLogService.LogAsync("Snap", $"Snap token creation failed for OrderId '{request.OrderId}'. Error: {ex.Message}");

                snapTransaction.TransactionStatus = "error";
                snapTransaction.UpdatedAt = DateTime.UtcNow;
                await m_dbContext.SaveChangesAsync();

                return StatusCode(502, DataWrapper<object>.Fail_InternalError(
                    message: "Failed to communicate with Midtrans. Please try again."));
            }
        }

        /// <summary>
        /// Syncs transaction fields from Midtrans status payload into local transaction record.
        /// Returns true when any tracked value changes.
        /// </summary>
        private static bool ApplyMidtransStatusToTransaction(JsonElement root, Db_SnapTransaction transaction)
        {
            var changed = false;

            if (root.TryGetProperty("transaction_status", out var txStatusEl)
                && txStatusEl.ValueKind == JsonValueKind.String)
            {
                var latestStatus = txStatusEl.GetString();
                if (!string.Equals(transaction.TransactionStatus, latestStatus, StringComparison.OrdinalIgnoreCase))
                {
                    transaction.TransactionStatus = latestStatus;
                    changed = true;
                }
            }

            if (root.TryGetProperty("transaction_id", out var txIdEl)
                && txIdEl.ValueKind == JsonValueKind.String)
            {
                var latestTransactionId = txIdEl.GetString();
                if (!string.Equals(transaction.MidtransTransactionId, latestTransactionId, StringComparison.Ordinal))
                {
                    transaction.MidtransTransactionId = latestTransactionId;
                    changed = true;
                }
            }

            if (changed)
            {
                transaction.UpdatedAt = DateTime.UtcNow;
            }

            return changed;
        }

        // Internal DTOs for Midtrans API serialization (snake_case)
        private class MidtransSnapRequest
        {
            public MidtransTransactionDetails TransactionDetails { get; set; } = new();
            public MidtransCustomerDetails? CustomerDetails { get; set; }
            public List<MidtransItemDetail>? ItemDetails { get; set; }
        }

        private class MidtransTransactionDetails
        {
            public string OrderId { get; set; } = string.Empty;
            public int GrossAmount { get; set; }
        }

        private class MidtransCustomerDetails
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
        }

        private class MidtransItemDetail
        {
            public string? Id { get; set; }
            public int Price { get; set; }
            public int Quantity { get; set; }
            public string? Name { get; set; }
        }

    }
}
