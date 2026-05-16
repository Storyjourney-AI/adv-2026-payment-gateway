using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentGateway.Server.Applications.Models.Dbs;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Models.Dbs;
using PaymentGateway.Server.Midtrans.Models.Dtos;
using System.Net;
using System.Text;
using System.Text.Json;

namespace PaymentGateway.Server.Midtrans.Services
{
    public interface IMidtransTransactionReconciliationService
    {
        Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
            string midtransOrderId,
            CancellationToken cancellationToken = default);
    }

    public enum MidtransRedirectKind
    {
        Success,
        Pending,
        Failure
    }

    public sealed class MidtransStatusVerificationException : Exception
    {
        public HttpStatusCode? StatusCode { get; }

        public MidtransStatusVerificationException(string message, HttpStatusCode? statusCode = null)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public sealed class MidtransVerifiedStatus
    {
        public string TransactionStatus { get; init; } = string.Empty;
        public string? FraudStatus { get; init; }
        public string GrossAmount { get; init; } = string.Empty;
        public string? TransactionId { get; init; }
        public string? PaymentType { get; init; }
        public string? StatusCode { get; init; }
        public string? StatusMessage { get; init; }
    }

    public sealed class MidtransTransactionReconciliationResult
    {
        public Db_SnapTransaction Transaction { get; init; } = null!;
        public Db_Environment Environment { get; init; } = null!;
        public MidtransVerifiedStatus VerifiedStatus { get; init; } = null!;
        public MidtransRedirectKind RedirectKind { get; init; }
        public Dto_SnapStatusResponse StatusResponse { get; init; } = null!;
    }

    public sealed class MidtransTransactionReconciliationService : IMidtransTransactionReconciliationService
    {
        private const string MidtransSandboxApiUrl = "https://api.sandbox.midtrans.com/v2";
        private const string MidtransProductionApiUrl = "https://api.midtrans.com/v2";

        private readonly AppDbContext m_dbContext;
        private readonly MidtransOptions m_midtransOptions;
        private readonly IHttpClientFactory m_httpClientFactory;
        private readonly ILogger<MidtransTransactionReconciliationService> m_logger;

        public MidtransTransactionReconciliationService(
            AppDbContext dbContext,
            IOptions<MidtransOptions> midtransOptions,
            IHttpClientFactory httpClientFactory,
            ILogger<MidtransTransactionReconciliationService> logger)
        {
            m_dbContext = dbContext;
            m_midtransOptions = midtransOptions.Value;
            m_httpClientFactory = httpClientFactory;
            m_logger = logger;
        }

        public async Task<MidtransTransactionReconciliationResult?> ReconcileByMidtransOrderIdAsync(
            string midtransOrderId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(midtransOrderId))
            {
                return null;
            }

            var transaction = await m_dbContext.SnapTransactions
                .Include(t => t.Environment)
                .FirstOrDefaultAsync(t => t.MidtransOrderId == midtransOrderId, cancellationToken);

            if (transaction?.Environment == null)
            {
                return null;
            }

            var verifiedStatus = await GetVerifiedStatusAsync(transaction, transaction.Environment.IsSandbox, cancellationToken);

            if (ApplyVerifiedStatus(transaction, verifiedStatus))
            {
                await m_dbContext.SaveChangesAsync(cancellationToken);
            }

            return new MidtransTransactionReconciliationResult
            {
                Transaction = transaction,
                Environment = transaction.Environment,
                VerifiedStatus = verifiedStatus,
                RedirectKind = ResolveRedirectKind(verifiedStatus.TransactionStatus, verifiedStatus.FraudStatus),
                StatusResponse = BuildStatusResponse(transaction, verifiedStatus)
            };
        }

        internal static MidtransRedirectKind ResolveRedirectKind(string? transactionStatus, string? fraudStatus)
        {
            if (string.Equals(transactionStatus, "settlement", StringComparison.OrdinalIgnoreCase))
            {
                return MidtransRedirectKind.Success;
            }

            if (string.Equals(transactionStatus, "capture", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(fraudStatus, "challenge", StringComparison.OrdinalIgnoreCase))
                {
                    return MidtransRedirectKind.Pending;
                }

                if (string.Equals(fraudStatus, "deny", StringComparison.OrdinalIgnoreCase))
                {
                    return MidtransRedirectKind.Failure;
                }

                return MidtransRedirectKind.Success;
            }

            if (string.Equals(transactionStatus, "pending", StringComparison.OrdinalIgnoreCase)
                || string.Equals(transactionStatus, "authorize", StringComparison.OrdinalIgnoreCase))
            {
                return MidtransRedirectKind.Pending;
            }

            return MidtransRedirectKind.Failure;
        }

        internal static bool ApplyVerifiedStatus(Db_SnapTransaction transaction, MidtransVerifiedStatus verifiedStatus)
        {
            var changed = false;

            if (!string.Equals(transaction.TransactionStatus, verifiedStatus.TransactionStatus, StringComparison.OrdinalIgnoreCase))
            {
                transaction.TransactionStatus = verifiedStatus.TransactionStatus;
                changed = true;
            }

            if (!string.Equals(transaction.MidtransTransactionId, verifiedStatus.TransactionId, StringComparison.Ordinal))
            {
                transaction.MidtransTransactionId = verifiedStatus.TransactionId;
                changed = true;
            }

            if (changed)
            {
                transaction.UpdatedAt = DateTime.UtcNow;
            }

            return changed;
        }

        private async Task<MidtransVerifiedStatus> GetVerifiedStatusAsync(
            Db_SnapTransaction transaction,
            bool isSandbox,
            CancellationToken cancellationToken)
        {
            var envOptions = isSandbox ? m_midtransOptions.Sandbox : m_midtransOptions.Production;
            var baseUrl = isSandbox ? MidtransSandboxApiUrl : MidtransProductionApiUrl;
            var statusUrl = $"{baseUrl}/{transaction.MidtransOrderId}/status";
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(envOptions.ServerKey + ":"));

            var client = m_httpClientFactory.CreateClient("midtrans");
            using var request = new HttpRequestMessage(HttpMethod.Get, statusUrl);
            request.Headers.Add("Authorization", $"Basic {authValue}");

            var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var statusMessage = TryGetStringProperty(responseBody, "status_message");

                m_logger.LogWarning(
                    "Midtrans status verification failed for order {OrderId}. Status: {Status}, Body: {Body}",
                    transaction.MidtransOrderId,
                    response.StatusCode,
                    responseBody);

                throw new MidtransStatusVerificationException(
                    statusMessage ?? "Midtrans status API returned an error.",
                    response.StatusCode);
            }

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var transactionStatus = GetRequiredString(root, "transaction_status");

            return new MidtransVerifiedStatus
            {
                TransactionStatus = transactionStatus,
                FraudStatus = GetOptionalString(root, "fraud_status"),
                GrossAmount = GetOptionalString(root, "gross_amount") ?? string.Empty,
                TransactionId = GetOptionalString(root, "transaction_id"),
                PaymentType = GetOptionalString(root, "payment_type"),
                StatusCode = GetOptionalString(root, "status_code"),
                StatusMessage = GetOptionalString(root, "status_message")
            };
        }

        private static Dto_SnapStatusResponse BuildStatusResponse(
            Db_SnapTransaction transaction,
            MidtransVerifiedStatus verifiedStatus)
        {
            return new Dto_SnapStatusResponse
            {
                CallerOrderId = transaction.CallerOrderId,
                MidtransOrderId = transaction.MidtransOrderId,
                GatewayStatus = transaction.TransactionStatus,
                MidtransStatus = verifiedStatus.TransactionStatus,
                FraudStatus = verifiedStatus.FraudStatus,
                GrossAmount = verifiedStatus.GrossAmount,
                MidtransTransactionId = verifiedStatus.TransactionId,
                PaymentType = verifiedStatus.PaymentType,
                CreatedAt = transaction.CreatedAt,
                UpdatedAt = transaction.UpdatedAt
            };
        }

        private static string GetRequiredString(JsonElement root, string propertyName)
        {
            var value = GetOptionalString(root, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new MidtransStatusVerificationException(
                    $"Midtrans status response is missing '{propertyName}'.");
            }

            return value;
        }

        private static string? GetOptionalString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                return null;
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                _ => null
            };
        }

        private static string? TryGetStringProperty(string rawJson, string propertyName)
        {
            try
            {
                using var document = JsonDocument.Parse(rawJson);
                return GetOptionalString(document.RootElement, propertyName);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}