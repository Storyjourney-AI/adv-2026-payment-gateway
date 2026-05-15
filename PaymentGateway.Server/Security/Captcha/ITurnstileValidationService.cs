namespace PaymentGateway.Server.Security.Captcha
{
    public interface ITurnstileValidationService
    {
        Task<TurnstileValidationResult> ValidateRequestAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
    }
}
