using PaymentGateway.Server.ActivityLog.Services;
using PaymentGateway.Server.Authorization.Models.Dbs;
using PaymentGateway.Server.Authorization.Services;
using PaymentGateway.Server.Authorization.Utils;
using PaymentGateway.Server.Common.Models;
using PaymentGateway.Server.Databases;
using PaymentGateway.Server.Midtrans.Models;
using PaymentGateway.Server.Midtrans.Services;
using PaymentGateway.Server.Security.Captcha;
using PaymentGateway.Server.Security.Operations;
using PaymentGateway.Server.Security.RateLimiting;
using PaymentGateway.Server.Security.Webhook;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add DbContext with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration. Please add it to appsettings.json");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Identity services
builder.Services.AddIdentity<Db_ApplicationUser, Db_ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton(serviceProvider =>
{
    var gcsBase64Creds = builder.Configuration["GCS:Base64Credential"] ?? throw new ArgumentNullException("No GCS Service Credential Provided");
    byte[] jsonBytes = Convert.FromBase64String(gcsBase64Creds);
    using var stream = new MemoryStream(jsonBytes);
    var credential = GoogleCredential.FromStream(stream);
    return StorageClient.Create(credential);
});


builder.Services.AddSingleton(serviceProvider =>
{
    var gcsBase64Creds = builder.Configuration["GCS:Base64Credential"] ?? throw new ArgumentNullException("No GCS Service Credential Provided");
    byte[] jsonBytes = Convert.FromBase64String(gcsBase64Creds);
    using var stream = new MemoryStream(jsonBytes);
    var credential = GoogleCredential.FromStream(stream);
    return UrlSigner.FromCredential(credential);
});

// Register AuthService
builder.Services.AddScoped<AuthService>();

// Register UsersService
builder.Services.AddScoped<PaymentGateway.Server.Authorization.Services.UsersService>();

// Register RoleService
builder.Services.AddScoped<RoleService>();

// Register ClaimsService
builder.Services.AddScoped<ClaimsService>();

// Register ActivityLogService
builder.Services.AddScoped<ActivityLogService>();

// Register ActivityLogCleanupService (background service for 30-day retention)
builder.Services.AddHostedService<ActivityLogCleanupService>();

// Add HttpClient (registers IHttpClientFactory; named clients below set explicit timeouts)
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("midtrans", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("webhook-forward", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddHttpClient("turnstile-verify", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddMemoryCache();

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add Data Protection for email password encryption
builder.Services.AddDataProtection();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x => $"{e.Key}: {x.ErrorMessage}"))
                .ToList();

            var result = DataWrapper<object>.BadRequest(
                message: "One or more validation errors occurred.",
                errors: errors
            );

            return new Microsoft.AspNetCore.Mvc.ObjectResult(result)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        };
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT authentication
var key = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
});

// Add custom authorization policies
builder.Services.AddCustomAuthorizationPolicies();

// Configure Midtrans options
builder.Services.Configure<MidtransOptions>(builder.Configuration.GetSection("Midtrans"));
builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.Configure<TurnstileOptions>(builder.Configuration.GetSection("Turnstile"));
builder.Services.Configure<WebhookHardeningOptions>(builder.Configuration.GetSection("WebhookHardening"));
builder.Services.AddScoped<ITurnstileValidationService, TurnstileValidationService>();
builder.Services.AddScoped<IMidtransTransactionReconciliationService, MidtransTransactionReconciliationService>();
builder.Services.AddSingleton<IWebhookReplayGuard, WebhookReplayGuard>();
builder.Services.AddSingleton<ISecurityMetricsService, SecurityMetricsService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var metrics = context.HttpContext.RequestServices.GetRequiredService<ISecurityMetricsService>();
        var path = context.HttpContext.Request.Path.Value ?? "unknown_path";
        metrics.Increment("rate_limit_reject_total", path);

        var payload = JsonSerializer.Serialize(new
        {
            success = false,
            message = "Too many requests. Please retry later.",
            data = (object?)null,
            errors = new[] { "Rate limit exceeded." },
            code = 429
        });

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(payload, token);
    };

    options.AddPolicy(RateLimitPolicyNames.AuthLoginStrict, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.AuthLoginStrict;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: false);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicyNames.AuthRefreshModerate, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.AuthRefreshModerate;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: false);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicyNames.AuthLogoutModerate, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.AuthLogoutModerate;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: false);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicyNames.SnapPublicModerate, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.SnapPublicModerate;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: true);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicyNames.SnapStatusModerate, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.SnapStatusModerate;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: true);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicyNames.SnapCancelModerate, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.SnapCancelModerate;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: true);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicyNames.WebhookTolerant, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.WebhookTolerant;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: false);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });

    options.AddPolicy(RateLimitPolicyNames.CallbackLenient, httpContext =>
    {
        var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value.CallbackLenient;
        var key = RateLimitKeyBuilder.Build(httpContext, includeApiKeyHash: false);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            QueueLimit = settings.QueueLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddAuthorization();

// IHttpClientFactory already registered above with named clients.

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder => builder
                    .WithOrigins("http://localhost:5500", "http://localhost")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });
}


var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Initialize cookie options based on environment
using (var scope = app.Services.CreateScope())
{
    PaymentGateway.Server.Authorization.Utils.CookieOptionsHelper.Initialize(app.Environment);
}

// Apply pending EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Seed roles and default data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<Db_ApplicationRole>>();
    var authService = services.GetRequiredService<AuthService>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // Seed SuperAdmin role
        var superAdminRole = await roleManager.FindByNameAsync("Super Admin");

        if (superAdminRole == null)
        {
            // Create Super Admin role if it doesn't exist
            superAdminRole = new Db_ApplicationRole
            {
                Name = "Super Admin",
                IsSystemRole = true,
                Description = "Super Administrator with full system access",
                CreatedAt = DateTime.UtcNow
            };

            var result = await roleManager.CreateAsync(superAdminRole);
            if (result.Succeeded)
            {
                logger.LogInformation("Super Admin role created successfully");
            }
        }

        // Seed Super Admin user
        var seedUserResult = await authService.SeedSuperAdminAsync();
        if (seedUserResult.Success)
        {
            logger.LogInformation("Super Admin user seeding: {message}", seedUserResult.Message);
        }
        else
        {
            logger.LogWarning("Super Admin user seeding: {message}", seedUserResult.Message);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding roles and users");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // ✅ Explicitly disable HTTPS redirect in development
}
else
{
    // Only enable HTTPS redirect in production
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
