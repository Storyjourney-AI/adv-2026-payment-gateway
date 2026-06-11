using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dtos;
using PaymentGateway.Server.Midtrans.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Server.Midtrans.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "RequireUser")]
    public class TransactionController : ControllerBase
    {
        private const string MidtransSandboxApiUrl = "https://api.sandbox.midtrans.com/v2";
        private const string MidtransProductionApiUrl = "https://api.midtrans.com/v2";

        private static readonly HashSet<string> s_validStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "pending", "settlement", "capture", "deny", "cancel", "expire", "error",
            "failure", "refund", "partial_refund", "authorize"
        };

        private readonly AppDbContext m_dbContext;
        private readonly UserManager<Db_ApplicationUser> m_userManager;
        private readonly ILogger<TransactionController> m_logger;
        private readonly MidtransOptions m_midtransOptions;
        private readonly WebhookForwardRetryOptions m_webhookForwardRetryOptions;
        private readonly IHttpClientFactory m_httpClientFactory;
        private readonly IWebhookForwardService m_webhookForwardService;
        private readonly IMidtransTransactionReconciliationService m_reconciliationService;

        public TransactionController(
            AppDbContext dbContext,
            UserManager<Db_ApplicationUser> userManager,
            ILogger<TransactionController> logger,
            IOptions<MidtransOptions> midtransOptions,
            IOptions<WebhookForwardRetryOptions> webhookForwardRetryOptions,
            IHttpClientFactory httpClientFactory,
            IWebhookForwardService webhookForwardService,
            IMidtransTransactionReconciliationService reconciliationService)
        {
            m_dbContext = dbContext;
            m_userManager = userManager;
            m_logger = logger;
            m_midtransOptions = midtransOptions.Value;
            m_webhookForwardRetryOptions = webhookForwardRetryOptions.Value;
            m_httpClientFactory = httpClientFactory;
            m_webhookForwardService = webhookForwardService;
            m_reconciliationService = reconciliationService;
        }

        /// <summary>
        /// Get paginated list of transactions with optional date range, status, and search filters.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<DataWrapper<PaginationWrapper<Dto_TransactionListItem>>>> GetTransactions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var userIdClaim = User.FindFirst("sub_id")?.Value;
                if (userIdClaim == null)
                {
                    return Unauthorized(DataWrapper<PaginationWrapper<Dto_TransactionListItem>>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userIdClaim, out var userGuid))
                {
                    return Unauthorized(DataWrapper<PaginationWrapper<Dto_TransactionListItem>>.Unauthorized(
                        message: "Invalid user identity"));
                }

                // Clamp page size to prevent excessive loads
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 50) pageSize = 50;
                if (page < 1) page = 1;

                var user = await m_userManager.FindByIdAsync(userIdClaim);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var query = m_dbContext.SnapTransactions
                    .Include(t => t.Environment)
                        .ThenInclude(e => e!.Application)
                    .AsQueryable();

                // Filter by user's applications unless Super Admin
                if (!isSuperAdmin)
                {
                    query = query.Where(t =>
                        t.Environment != null &&
                        t.Environment.Application != null &&
                        t.Environment.Application.UserId == userGuid);
                }

                // Filter by date range
                if (dateFrom.HasValue)
                {
                    var fromUtc = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc);
                    query = query.Where(t => t.CreatedAt >= fromUtc);
                }

                if (dateTo.HasValue)
                {
                    var toUtc = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
                    query = query.Where(t => t.CreatedAt < toUtc);
                }

                // Filter by status
                if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    if (!s_validStatuses.Contains(status))
                    {
                        return BadRequest(DataWrapper<PaginationWrapper<Dto_TransactionListItem>>.BadRequest(
                            message: "Invalid status filter value."));
                    }
                    query = query.Where(t => t.TransactionStatus == status.ToLowerInvariant());
                }

                // Search by order ID
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(t =>
                        t.CallerOrderId.ToLower().Contains(searchLower) ||
                        t.MidtransOrderId.ToLower().Contains(searchLower));
                }

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var pagedTransactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                await SyncPendingTransactionsAsync(pagedTransactions);

                // Left-join: fetch outbox rows for the paged transaction IDs in one query
                var transactionIds = pagedTransactions.Select(t => t.Id).ToList();
                var outboxByTransactionId = await m_dbContext.WebhookForwardOutbox
                    .Where(o => transactionIds.Contains(o.SnapTransactionId))
                    .ToDictionaryAsync(o => o.SnapTransactionId);

                var transactions = pagedTransactions
                    .Select(t =>
                    {
                        outboxByTransactionId.TryGetValue(t.Id, out var outbox);
                        return new Dto_TransactionListItem
                        {
                            Id = t.Id,
                            CallerOrderId = t.CallerOrderId,
                            MidtransOrderId = t.MidtransOrderId,
                            GrossAmount = t.GrossAmount,
                            TransactionStatus = t.TransactionStatus,
                            MidtransEnv = t.MidtransEnv,
                            MidtransTransactionId = t.MidtransTransactionId,
                            ApplicationName = t.Environment != null && t.Environment.Application != null
                                ? t.Environment.Application.Name
                                : "Unknown",
                            EnvironmentName = t.Environment != null ? t.Environment.Name : "Unknown",
                            IsSandbox = t.Environment != null && t.Environment.IsSandbox,
                            CreatedAt = t.CreatedAt,
                            UpdatedAt = t.UpdatedAt,
                            WebhookForwardStatus = outbox?.Status,
                            WebhookAttemptCount = outbox?.AttemptCount,
                            WebhookLastAttemptAt = outbox?.LastAttemptAt,
                            WebhookLastResponseCode = outbox?.LastResponseCode
                        };
                    })
                    .ToList();

                var result = new PaginationWrapper<Dto_TransactionListItem>
                {
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    Items = transactions
                };

                return Ok(DataWrapper<PaginationWrapper<Dto_TransactionListItem>>.Succeed(
                    result, message: "Transactions retrieved successfully"));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error retrieving transactions");
                return StatusCode(500, DataWrapper<PaginationWrapper<Dto_TransactionListItem>>.Fail_InternalError(
                    message: "An error occurred while retrieving transactions"));
            }
        }

        /// <summary>
        /// Manually trigger a re-verified webhook forward for a transaction.
        /// Re-verifies status against Midtrans, rebuilds the payload, and enqueues + attempts an immediate delivery.
        /// POST /api/transaction/{id}/webhook/retry
        /// </summary>
        [HttpPost("{id}/webhook/retry")]
        public async Task<ActionResult<DataWrapper<Dto_WebhookForwardStatus>>> RetryWebhookForward(Guid id)
        {
            try
            {
                var userIdClaim = User.FindFirst("sub_id")?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Unauthorized(DataWrapper<Dto_WebhookForwardStatus>.Unauthorized(
                        message: "User identity could not be resolved."));
                }

                // Load transaction with Environment + Application
                var transaction = await m_dbContext.SnapTransactions
                    .Include(t => t.Environment)
                        .ThenInclude(e => e!.Application)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction?.Environment == null || transaction.Environment.Application == null)
                {
                    return NotFound(DataWrapper<Dto_WebhookForwardStatus>.NotFound(
                        message: "Transaction not found."));
                }

                // Ownership check — same pattern as SnapController.TestPurchase
                var user = await m_userManager.FindByIdAsync(userIdClaim);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                if (!isSuperAdmin && transaction.Environment.Application.UserId.ToString() != userIdClaim)
                {
                    return StatusCode(403, DataWrapper<Dto_WebhookForwardStatus>.Forbidden(
                        message: "You do not have permission to retry the webhook for this transaction."));
                }

                var webhookUrl = transaction.Environment.WebhookUrl;
                if (string.IsNullOrWhiteSpace(webhookUrl))
                {
                    return BadRequest(DataWrapper<Dto_WebhookForwardStatus>.BadRequest(
                        message: "No webhook URL is registered for this transaction's environment."));
                }

                // Re-verify status against Midtrans
                MidtransTransactionReconciliationResult? reconciliationResult;
                try
                {
                    reconciliationResult = await m_reconciliationService
                        .ReconcileByMidtransOrderIdAsync(transaction.MidtransOrderId, HttpContext.RequestAborted);
                }
                catch (MidtransStatusVerificationException ex)
                {
                    m_logger.LogWarning(ex,
                        "Midtrans status verification failed during manual webhook retry for transaction {TransactionId}.",
                        id);
                    return StatusCode(502, DataWrapper<Dto_WebhookForwardStatus>.Fail(
                        System.Net.HttpStatusCode.BadGateway,
                        message: "Could not verify transaction status with Midtrans. Please try again later."));
                }

                if (reconciliationResult == null)
                {
                    return NotFound(DataWrapper<Dto_WebhookForwardStatus>.NotFound(
                        message: "Transaction not found in Midtrans."));
                }

                // Build a payload-faithful raw body for the forward:
                // 1. Prefer the stored RawNotificationBody (preserves signature_key, transaction_time, va_numbers, etc.)
                // 2. If no stored body exists, fall back to a reconstructed minimal body
                // 3. If re-verified status differs from the stored body's transaction_status, patch it before forwarding
                var existingOutboxRow = await m_dbContext.WebhookForwardOutbox
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.SnapTransactionId == transaction.Id, HttpContext.RequestAborted);

                string rawBodyForEnqueue;
                if (!string.IsNullOrWhiteSpace(existingOutboxRow?.RawNotificationBody))
                {
                    rawBodyForEnqueue = PatchTransactionStatus(
                        existingOutboxRow.RawNotificationBody,
                        reconciliationResult.VerifiedStatus.TransactionStatus);
                }
                else
                {
                    // Fallback: no stored body — reconstruct from reconciliation (reduced field set)
                    rawBodyForEnqueue = BuildMidtransRawBodyFromReconciliation(transaction, reconciliationResult.VerifiedStatus);
                }

                // Enqueue / upsert the outbox row (resets to Pending so retry service covers it on failure).
                // EnqueueAsync returns the tracked row so we can use it for the immediate delivery attempt.
                var outboxRow = await m_webhookForwardService.EnqueueAsync(
                    snapTransactionId: transaction.Id,
                    environmentId: transaction.EnvironmentId,
                    midtransOrderId: transaction.MidtransOrderId,
                    callerOrderId: transaction.CallerOrderId,
                    targetUrl: webhookUrl,
                    rawBody: rawBodyForEnqueue,
                    verifiedStatus: reconciliationResult.VerifiedStatus,
                    maxAttempts: m_webhookForwardRetryOptions.MaxAttempts,
                    cancellationToken: HttpContext.RequestAborted);

                // Immediate delivery attempt
                await m_webhookForwardService.TryDeliverAsync(
                    outboxRow,
                    m_webhookForwardRetryOptions,
                    HttpContext.RequestAborted);

                // Re-read final state (TryDeliverAsync mutates the tracked entity and saves)
                var finalRow = await m_dbContext.WebhookForwardOutbox
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.SnapTransactionId == transaction.Id, HttpContext.RequestAborted);

                if (finalRow == null)
                {
                    return StatusCode(500, DataWrapper<Dto_WebhookForwardStatus>.Fail_InternalError(
                        message: "Outbox row could not be found after enqueue."));
                }

                var statusDto = new Dto_WebhookForwardStatus
                {
                    SnapTransactionId = finalRow.SnapTransactionId,
                    Status = finalRow.Status,
                    AttemptCount = finalRow.AttemptCount,
                    MaxAttempts = finalRow.MaxAttempts,
                    LastAttemptAt = finalRow.LastAttemptAt,
                    NextAttemptAt = finalRow.NextAttemptAt,
                    LastResponseCode = finalRow.LastResponseCode,
                    LastError = finalRow.LastError
                };

                return Ok(DataWrapper<Dto_WebhookForwardStatus>.Succeed(
                    statusDto, message: "Webhook forward retry triggered successfully."));
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error triggering webhook forward retry for transaction {TransactionId}", id);
                return StatusCode(500, DataWrapper<Dto_WebhookForwardStatus>.Fail_InternalError(
                    message: "An error occurred while triggering the webhook retry."));
            }
        }

        /// <summary>
        /// Fallback sync for dashboard list:
        /// if webhook is delayed/missed, refresh "pending/authorize" statuses from Midtrans.
        /// </summary>
        private async Task SyncPendingTransactionsAsync(List<Db_SnapTransaction> transactions)
        {
            if (transactions.Count == 0)
                return;

            var pendingItems = transactions
                .Where(t =>
                    t.Environment != null &&
                    (string.Equals(t.TransactionStatus, "pending", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.TransactionStatus, "authorize", StringComparison.OrdinalIgnoreCase)) &&
                    t.UpdatedAt <= DateTime.UtcNow.AddSeconds(-20))
                .ToList();

            if (pendingItems.Count == 0)
                return;

            var anyChanged = false;
            var client = m_httpClientFactory.CreateClient("midtrans");

            foreach (var tx in pendingItems)
            {
                var isSandbox = tx.Environment!.IsSandbox;
                var envOptions = isSandbox ? m_midtransOptions.Sandbox : m_midtransOptions.Production;
                var baseUrl = isSandbox ? MidtransSandboxApiUrl : MidtransProductionApiUrl;
                var statusUrl = $"{baseUrl}/{tx.MidtransOrderId}/status";

                try
                {
                    var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(envOptions.ServerKey + ":"));
                    using var request = new HttpRequestMessage(HttpMethod.Get, statusUrl);
                    request.Headers.Add("Authorization", $"Basic {authValue}");

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        m_logger.LogDebug(
                            "Skipping pending sync for order {OrderId}: Midtrans returned {StatusCode}",
                            tx.CallerOrderId,
                            response.StatusCode);
                        continue;
                    }

                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    var changed = false;
                    if (root.TryGetProperty("transaction_status", out var statusEl) &&
                        statusEl.ValueKind == JsonValueKind.String)
                    {
                        var latestStatus = statusEl.GetString();
                        if (!string.Equals(tx.TransactionStatus, latestStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            tx.TransactionStatus = latestStatus;
                            changed = true;
                        }
                    }

                    if (root.TryGetProperty("transaction_id", out var txIdEl) &&
                        txIdEl.ValueKind == JsonValueKind.String)
                    {
                        var latestTransactionId = txIdEl.GetString();
                        if (!string.Equals(tx.MidtransTransactionId, latestTransactionId, StringComparison.Ordinal))
                        {
                            tx.MidtransTransactionId = latestTransactionId;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        tx.UpdatedAt = DateTime.UtcNow;
                        anyChanged = true;
                    }
                }
                catch (Exception ex)
                {
                    m_logger.LogWarning(ex,
                        "Failed to sync pending transaction status from Midtrans for order {OrderId}",
                        tx.CallerOrderId);
                }
            }

            if (anyChanged)
            {
                await m_dbContext.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Export transactions to PDF for a given date range. Maximum 10,000 records per export.
        /// </summary>
        [HttpGet("export/pdf")]
        public async Task<IActionResult> ExportPdf(
            [FromQuery] DateTime dateFrom,
            [FromQuery] DateTime dateTo,
            [FromQuery] string? status = null)
        {
            try
            {
                var userIdClaim = User.FindFirst("sub_id")?.Value;
                if (userIdClaim == null)
                {
                    return Unauthorized(DataWrapper<object>.Unauthorized(
                        message: "User not authenticated"));
                }

                if (!Guid.TryParse(userIdClaim, out var userGuid))
                {
                    return Unauthorized(DataWrapper<object>.Unauthorized(
                        message: "Invalid user identity"));
                }

                var fromUtc = DateTime.SpecifyKind(dateFrom.Date, DateTimeKind.Utc);
                var toUtc = DateTime.SpecifyKind(dateTo.Date.AddDays(1), DateTimeKind.Utc);

                // Validate date order
                if (fromUtc >= toUtc)
                {
                    return BadRequest(DataWrapper<object>.BadRequest(
                        message: "Start date must be before end date."));
                }

                // Validate date range (max 1 year)
                if ((toUtc - fromUtc).TotalDays > 366)
                {
                    return BadRequest(DataWrapper<object>.BadRequest(
                        message: "Date range cannot exceed 1 year."));
                }

                var user = await m_userManager.FindByIdAsync(userIdClaim);
                var isSuperAdmin = user != null && await m_userManager.IsInRoleAsync(user, "Super Admin");

                var query = m_dbContext.SnapTransactions
                    .Include(t => t.Environment)
                        .ThenInclude(e => e!.Application)
                    .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt < toUtc);

                // Filter by user's applications unless Super Admin
                if (!isSuperAdmin)
                {
                    query = query.Where(t =>
                        t.Environment != null &&
                        t.Environment.Application != null &&
                        t.Environment.Application.UserId == userGuid);
                }

                // Filter by status
                if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    if (!s_validStatuses.Contains(status))
                    {
                        return BadRequest(DataWrapper<object>.BadRequest(
                            message: "Invalid status filter value."));
                    }
                    query = query.Where(t => t.TransactionStatus == status.ToLowerInvariant());
                }

                // Limit to 10,000 records to prevent server overload
                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(10000)
                    .Select(t => new Dto_TransactionListItem
                    {
                        Id = t.Id,
                        CallerOrderId = t.CallerOrderId,
                        MidtransOrderId = t.MidtransOrderId,
                        GrossAmount = t.GrossAmount,
                        TransactionStatus = t.TransactionStatus,
                        MidtransEnv = t.MidtransEnv,
                        MidtransTransactionId = t.MidtransTransactionId,
                        ApplicationName = t.Environment != null && t.Environment.Application != null
                            ? t.Environment.Application.Name
                            : "Unknown",
                        EnvironmentName = t.Environment != null ? t.Environment.Name : "Unknown",
                        IsSandbox = t.Environment != null && t.Environment.IsSandbox,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    })
                    .ToListAsync();

                if (transactions.Count == 0)
                {
                    return NotFound(DataWrapper<object>.NotFound(
                        message: "No transactions found for the selected date range."));
                }

                var pdfBytes = GeneratePdf(transactions, dateFrom, dateTo, status);
                var fileName = $"transactions_{dateFrom:yyyy-MM-dd}_to_{dateTo:yyyy-MM-dd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                m_logger.LogError(ex, "Error exporting transactions to PDF");
                return StatusCode(500, DataWrapper<object>.Fail_InternalError(
                    message: "An error occurred while generating the PDF export"));
            }
        }

        /// <summary>
        /// Patches the <c>transaction_status</c> field in a stored raw Midtrans notification body
        /// so that a manual retry forwards the re-verified status rather than the original (potentially stale) one.
        /// All other fields (signature_key, transaction_time, va_numbers, etc.) are preserved byte-faithfully.
        /// </summary>
        private static string PatchTransactionStatus(string rawBody, string newTransactionStatus)
        {
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(rawBody);
                if (node is System.Text.Json.Nodes.JsonObject obj)
                {
                    obj["transaction_status"] = newTransactionStatus;
                    return obj.ToJsonString();
                }
            }
            catch
            {
                // If parsing fails, fall back to the original body unchanged
            }

            return rawBody;
        }

        /// <summary>
        /// Reconstructs a minimal Midtrans-formatted JSON body from a reconciliation result so that
        /// <see cref="IWebhookForwardService.EnqueueAsync"/> can pass it through the payload builder.
        /// Used only as a fallback when no stored RawNotificationBody is available.
        /// </summary>
        private static string BuildMidtransRawBodyFromReconciliation(
            Db_SnapTransaction transaction,
            MidtransVerifiedStatus verifiedStatus)
        {
            // Produce a snake_case JSON matching the Midtrans notification shape expected by
            // MidtransWebhookForwardPayloadBuilder.Build (which just appends gateway_fee_breakdown).
            var node = new System.Text.Json.Nodes.JsonObject
            {
                ["order_id"] = transaction.MidtransOrderId,
                ["transaction_status"] = verifiedStatus.TransactionStatus,
                ["gross_amount"] = verifiedStatus.GrossAmount,
                ["transaction_id"] = verifiedStatus.TransactionId,
                ["payment_type"] = verifiedStatus.PaymentType,
                ["status_code"] = verifiedStatus.StatusCode,
                ["status_message"] = verifiedStatus.StatusMessage,
                ["fraud_status"] = verifiedStatus.FraudStatus
            };

            return node.ToJsonString();
        }

        private static byte[] GeneratePdf(
            List<Dto_TransactionListItem> transactions,
            DateTime dateFrom,
            DateTime dateTo,
            string? status)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.MarginHorizontal(30);
                    page.MarginVertical(20);

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Advine Payment Gateway")
                            .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

                        col.Item().Text("Transaction Report")
                            .FontSize(12).FontColor(Colors.Grey.Darken2);

                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Period: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span($"{dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}").FontSize(9).Bold();
                            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
                            {
                                text.Span($"  |  Status: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                                text.Span(status).FontSize(9).Bold();
                            }
                            text.Span($"  |  Total: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span($"{transactions.Count} transaction(s)").FontSize(9).Bold();
                        });

                        col.Item().PaddingTop(4).Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                            .FontSize(8).FontColor(Colors.Grey.Medium);

                        col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingBottom(4);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.5f); // Order ID
                            columns.RelativeColumn(2f);   // Application
                            columns.RelativeColumn(1.5f); // Environment
                            columns.RelativeColumn(1.5f); // Amount
                            columns.RelativeColumn(1.2f); // Status
                            columns.RelativeColumn(1f);   // Type
                            columns.RelativeColumn(2f);   // Created At
                        });

                        // Header
                        table.Header(header =>
                        {
                            var headerStyle = TextStyle.Default.FontSize(8).Bold().FontColor(Colors.White);

                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Order ID").Style(headerStyle);
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Application").Style(headerStyle);
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Environment").Style(headerStyle);
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignRight().Text("Amount").Style(headerStyle);
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Status").Style(headerStyle);
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Type").Style(headerStyle);
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Created At").Style(headerStyle);
                        });

                        // Rows
                        var cellStyle = TextStyle.Default.FontSize(8);
                        foreach (var tx in transactions)
                        {
                            var bgColor = transactions.IndexOf(tx) % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                            table.Cell().Background(bgColor).Padding(3)
                                .Text(tx.CallerOrderId).Style(cellStyle);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(tx.ApplicationName).Style(cellStyle);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text($"{tx.EnvironmentName} ({(tx.IsSandbox ? "Sandbox" : "Production")})").Style(cellStyle);
                            table.Cell().Background(bgColor).Padding(3)
                                .AlignRight().Text($"Rp {tx.GrossAmount:N0}").Style(cellStyle);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(tx.TransactionStatus ?? "-").Style(cellStyle);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(tx.MidtransEnv).Style(cellStyle);
                            table.Cell().Background(bgColor).Padding(3)
                                .Text(tx.CreatedAt.ToString("yyyy-MM-dd HH:mm")).Style(cellStyle);
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(TextStyle.Default.FontSize(7).FontColor(Colors.Grey.Medium));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                        text.Span("  |  Advine Payment Gateway  |  Confidential");
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
