namespace PaymentGateway.Server.Security.Captcha
{
    public sealed class TurnstileOptions
    {
        public bool IsEnabled { get; set; } = true;
        public string SecretKey { get; set; } = string.Empty;
        public string VerificationUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        public string HeaderName { get; set; } = "X-Turnstile-Token";
        public bool AllowBypassInDevelopment { get; set; } = true;
        public string DevelopmentBypassToken { get; set; } = "dev-turnstile-bypass";
    }

    public sealed record TurnstileValidationResult(bool Success, string Message)
    {
        public static TurnstileValidationResult Ok() => new(true, string.Empty);
        public static TurnstileValidationResult Fail(string message) => new(false, message);
    }
}
