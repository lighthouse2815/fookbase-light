using Fookbase.Api.Modules.Identity.Config;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Fookbase.Api;
using Fookbase.Api.Shared.Common;
using Fookbase.Api.Shared.ErrorHandling;
using Fookbase.Api.Shared.Observability;
using Fookbase.Api.Shared.Security;
using AppDataProtectionOptions = Fookbase.Api.Shared.Config.DataProtectionOptions;
using AppForwardedHeadersOptions = Fookbase.Api.Shared.Config.ForwardedHeadersOptions;
using Fookbase.Api.Shared.Config;
using Fookbase.Api.Shared.HealthChecks;
using Fookbase.Api.Modules.Feed.Endpoints;
using Fookbase.Api.Modules.Admin.Common;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.Middleware;
using Fookbase.Api.Modules.Media.HealthChecks;
using Fookbase.Api.Modules.Messages.Hubs;
using Fookbase.Api.Modules.Notifications.Hubs;
using Fookbase.Api.Modules.Photos;
using Fookbase.Api.Modules.Photos.Endpoints;
using Fookbase.Api.Modules.Reels.Endpoints;
using Fookbase.Api.Modules.Memories.Endpoints;
using Fookbase.Api.Modules.Games.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

ProductionConfigurationValidator.Validate(builder.Configuration, builder.Environment.IsProduction());

var jwtOptions = builder.Configuration.GetSection(IdentityModuleConstants.ConfigurationSections.Jwt).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
var googleAuthenticationOptions = builder.Configuration
    .GetSection(IdentityModuleConstants.ConfigurationSections.GoogleAuthentication)
    .Get<GoogleAuthenticationOptions>() ?? new GoogleAuthenticationOptions();
var dataProtectionOptions = builder.Configuration.GetSection(AppDataProtectionOptions.SectionName)
    .Get<AppDataProtectionOptions>() ?? new AppDataProtectionOptions();
dataProtectionOptions.Validate(builder.Environment.IsProduction());
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(dataProtectionOptions.ApplicationName);
if (!string.IsNullOrWhiteSpace(dataProtectionOptions.KeyRingPath))
{
    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(dataProtectionOptions.KeyRingPath));
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var forwardedHeadersOptions = builder.Configuration.GetSection(AppForwardedHeadersOptions.SectionName)
    .Get<AppForwardedHeadersOptions>() ?? new AppForwardedHeadersOptions();
forwardedHeadersOptions.Validate(builder.Environment.IsProduction());
var rateLimitPermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 120);
var rateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
var authLoginPermitLimit = builder.Configuration.GetValue("RateLimiting:SensitiveAuth:LoginPermitLimit", 10);
var authRecoveryPermitLimit = builder.Configuration.GetValue("RateLimiting:SensitiveAuth:RecoveryPermitLimit", 5);
var authResendVerificationPermitLimit = builder.Configuration.GetValue(
    "RateLimiting:SensitiveAuth:ResendVerificationPermitLimit",
    3);
var authRateLimitWindowSeconds = builder.Configuration.GetValue(
    "RateLimiting:SensitiveAuth:WindowSeconds",
    300);
var searchPermitLimit = builder.Configuration.GetValue("RateLimiting:Search:PermitLimit", 30);
var searchRateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:Search:WindowSeconds", 60);
var aiChatPermitLimit = builder.Configuration.GetValue("RateLimiting:AiChat:PermitLimit", 10);
var aiChatRateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:AiChat:WindowSeconds", 60);
if (rateLimitPermitLimit <= 0 ||
    rateLimitWindowSeconds <= 0 ||
    authLoginPermitLimit <= 0 ||
    authRecoveryPermitLimit <= 0 ||
    authResendVerificationPermitLimit <= 0 ||
    authRateLimitWindowSeconds <= 0 ||
    searchPermitLimit <= 0 ||
    searchRateLimitWindowSeconds <= 0 ||
    aiChatPermitLimit <= 0 ||
    aiChatRateLimitWindowSeconds <= 0)
{
    throw new InvalidOperationException("Rate limiting values must be positive.");
}
builder.Services.AddFookbasePersistence(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddUsersModule();
builder.Services.AddFriendsModule(builder.Configuration);
builder.Services.AddFeedModule(builder.Configuration);
builder.Services.AddGroupsModule();
builder.Services.AddPagesModule();
builder.Services.AddPhotosModule();
builder.Services.AddMessagesModule();
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddPostsModule(builder.Configuration);
builder.Services.AddMediaModule(builder.Configuration);
builder.Services.AddReelsModule();
builder.Services.AddStoriesModule(builder.Configuration);
builder.Services.AddAdminModule();
builder.Services.AddSearchModule();
builder.Services.AddEventsModule();
builder.Services.AddMemoriesModule();
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddGamesModule();

jwtOptions.Validate();
googleAuthenticationOptions.Validate(builder.Environment.IsProduction());
var authenticationBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && (context.HttpContext.Request.Path.StartsWithSegments("/hubs/messages") ||
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications") ||
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs/flappy-bird") ||
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs/jumping")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (ApiResponse.AppliesTo(context.HttpContext))
                {
                    await Results.Json(ApiResponse.Failure(
                        "invalid_access_token", "A valid access token is required.", context.HttpContext),
                        statusCode: StatusCodes.Status401Unauthorized).ExecuteAsync(context.HttpContext);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "A valid access token is required."
                };
                problem.Extensions["code"] = "invalid_access_token";
                problem.Extensions["requestId"] = RequestCorrelation.GetId(context.HttpContext);
                await context.Response.WriteAsJsonAsync(problem);
            }
        };
    });
if (googleAuthenticationOptions.Enabled)
{
    authenticationBuilder
        .AddCookie(IdentityModuleConstants.ExternalLogin.ExternalScheme, options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        })
        .AddGoogle(IdentityModuleConstants.ExternalLogin.GoogleScheme, options =>
        {
            options.SignInScheme = IdentityModuleConstants.ExternalLogin.ExternalScheme;
            options.ClientId = googleAuthenticationOptions.ClientId;
            options.ClientSecret = googleAuthenticationOptions.ClientSecret;
            options.CallbackPath = "/signin-google";
            options.ClaimActions.MapJsonKey("sub", "sub");
            options.ClaimActions.MapJsonKey("email", "email");
            options.ClaimActions.MapJsonKey("email_verified", "email_verified");
        });
}
builder.Services.AddAuthorization(options => options.AddPolicy(AdminPolicy.Name, policy =>
    policy.RequireRole(IdentityModuleConstants.Roles.Admin)));
builder.Services.AddSignalR(options => options.AddFilter<AccountModerationHubFilter>());
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy("Client", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
}
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? $"ip:{context.Connection.RemoteIpAddress}"
            : $"user:{userId}";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitPermitLimit,
            Window = TimeSpan.FromSeconds(rateLimitWindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth-login:{ClientAddress(context)}",
            _ => SensitiveAuthRateLimit(authLoginPermitLimit, authRateLimitWindowSeconds)));
    options.AddPolicy("auth-password-recovery", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth-password-recovery:{ClientAddress(context)}",
            _ => SensitiveAuthRateLimit(authRecoveryPermitLimit, authRateLimitWindowSeconds)));
    options.AddPolicy("auth-resend-verification", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth-resend-verification:{ClientAddress(context)}",
            _ => SensitiveAuthRateLimit(authResendVerificationPermitLimit, authRateLimitWindowSeconds)));
    options.AddPolicy("auth-sensitive", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"auth-sensitive:{ClientAddress(context)}",
            _ => SensitiveAuthRateLimit(authLoginPermitLimit, authRateLimitWindowSeconds)));
    options.AddPolicy("search", context =>
    {
        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? $"search-ip:{context.Connection.RemoteIpAddress}"
            : $"search-user:{userId}";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
            SensitiveAuthRateLimit(searchPermitLimit, searchRateLimitWindowSeconds));
    });
    options.AddPolicy("ai-chat", context =>
    {
        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var partitionKey = string.IsNullOrWhiteSpace(userId)
            ? $"ai-chat-ip:{context.Connection.RemoteIpAddress}"
            : $"ai-chat-user:{userId}";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
            SensitiveAuthRateLimit(aiChatPermitLimit, aiChatRateLimitWindowSeconds));
    });
});
builder.Services.AddHealthChecks()
    .AddCheck<FookbaseDatabaseHealthCheck>("postgresql", tags: ["ready"])
    .AddCheck<CloudinaryHealthCheck>("cloudinary", tags: ["ready"]);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Controller errors without a body are formatted by status-code pages below.
    options.SuppressMapClientErrors = true;
    options.InvalidModelStateResponseFactory = context =>
    {
        var invalidBody = context.ModelState.Any(entry =>
            entry.Key.Length == 0 || entry.Key.StartsWith('$') ||
            entry.Value!.Errors.Any(error => error.Exception is not null));
        var error = invalidBody ? ErrorCode.InvalidRequest : ErrorCode.ValidationFailed;
        if (ApiResponse.AppliesTo(context.HttpContext))
        {
            var details = invalidBody ? null : new ValidationProblemDetails(context.ModelState).Errors.ToDictionary();
            return new BadRequestObjectResult(ApiResponse.Failure(
                error.Code, error.Message, context.HttpContext, details));
        }

        ProblemDetails problem = invalidBody
            ? new ProblemDetails()
            : new ValidationProblemDetails(context.ModelState);
        problem.Status = StatusCodes.Status400BadRequest;
        problem.Title = invalidBody ? "Bad request" : "Validation";
        problem.Detail = error.Message;
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/ai"))
        {
            problem.Title = invalidBody ? "Yêu cầu không hợp lệ" : "Dữ liệu không hợp lệ";
            problem.Detail = problem is ValidationProblemDetails validationProblem
                ? string.Join(" ", validationProblem.Errors.Values.SelectMany(errors => errors).Distinct())
                : "Nội dung yêu cầu không hợp lệ.";
        }
        problem.Extensions["code"] = error.Code;
        problem.Extensions["requestId"] = RequestCorrelation.GetId(context.HttpContext);
        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    if (context.ProblemDetails is HttpValidationProblemDetails)
    {
        context.ProblemDetails.Title = "Validation";
        context.ProblemDetails.Detail = ErrorCode.ValidationFailed.Message;
        context.ProblemDetails.Extensions["code"] = ErrorCode.ValidationFailed.Code;
    }
});
builder.Services.AddValidation();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<Fookbase.Api.Persistence.FookbaseDbContext>()
        .Database
        .MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages(async statusContext =>
{
    var context = statusContext.HttpContext;
    if (ApiResponse.AppliesTo(context))
    {
        await Results.Json(ApiResponse.Failure(context.Response.StatusCode, context),
            statusCode: context.Response.StatusCode).ExecuteAsync(context);
    }
});
if (forwardedHeadersOptions.Enabled)
{
    var forwardedHeaders = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = forwardedHeadersOptions.ForwardLimit,
        RequireHeaderSymmetry = true
    };
    forwardedHeaders.KnownIPNetworks.Clear();
    forwardedHeaders.KnownProxies.Clear();
    foreach (var proxy in forwardedHeadersOptions.KnownProxies)
    {
        forwardedHeaders.KnownProxies.Add(IPAddress.Parse(proxy));
    }

    app.UseForwardedHeaders(forwardedHeaders);
}
if (app.Environment.IsProduction() && forwardedHeadersOptions.Enabled)
{
    app.UseHttpsRedirection();
}
app.UseMiddleware<SecurityHeadersMiddleware>();
if (allowedOrigins.Length > 0)
{
    app.UseCors("Client");
}
app.UseAuthentication();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<AccountModerationMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapHealthChecks("/health").DisableRateLimiting();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).DisableRateLimiting();
app.MapControllers();
app.MapFeedEndpoints();
app.MapHub<MessagesHub>("/hubs/messages");
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapReelEndpoints();
app.MapPhotoAlbumEndpoints();
app.MapMemoryEndpoints();
app.MapHub<FlappyBirdHub>("/hubs/flappy-bird");
app.MapHub<JumpingHub>("/hubs/jumping");

app.Run();

static FixedWindowRateLimiterOptions SensitiveAuthRateLimit(int permitLimit, int windowSeconds) =>
    new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(windowSeconds),
        QueueLimit = 0,
        AutoReplenishment = true
    };

static string ClientAddress(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

public partial class Program;
