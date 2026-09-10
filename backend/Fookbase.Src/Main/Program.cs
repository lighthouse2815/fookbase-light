using Fookbase.Api.Modules.Identity.Config;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Fookbase.Api;
using Fookbase.Api.Shared.ErrorHandling;
using Fookbase.Api.Shared.HealthChecks;
using Fookbase.Api.Modules.Friends.Endpoints;
using Fookbase.Api.Modules.Admin;
using Fookbase.Api.Modules.Admin.Endpoints;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Endpoints;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Media.Endpoints;
using Fookbase.Api.Modules.Media.HealthChecks;
using Fookbase.Api.Modules.Messages.Endpoints;
using Fookbase.Api.Modules.Messages.Hubs;
using Fookbase.Api.Modules.Posts.Endpoints;
using Fookbase.Api.Modules.Users.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
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
if (rateLimitPermitLimit <= 0 ||
    rateLimitWindowSeconds <= 0 ||
    authLoginPermitLimit <= 0 ||
    authRecoveryPermitLimit <= 0 ||
    authResendVerificationPermitLimit <= 0 ||
    authRateLimitWindowSeconds <= 0)
{
    throw new InvalidOperationException("Rate limiting values must be positive.");
}
builder.Services.AddFookbasePersistence(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddUsersModule();
builder.Services.AddFriendsModule();
builder.Services.AddMessagesModule();
builder.Services.AddPostsModule(builder.Configuration);
builder.Services.AddMediaModule(builder.Configuration);
builder.Services.AddAdminModule();

jwtOptions.Validate();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/messages"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "A valid access token is required."
                };
                problem.Extensions["code"] = "invalid_access_token";
                await context.Response.WriteAsJsonAsync(problem);
            }
        };
    });
builder.Services.AddAuthorization(options => options.AddPolicy(AdminPolicy.Name, policy =>
    policy.RequireRole(AdminRole.Name)));
builder.Services.AddSignalR();
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
});
builder.Services.AddHealthChecks()
    .AddCheck<FookbaseDatabaseHealthCheck>("postgresql", tags: ["ready"])
    .AddCheck<MinioBucketHealthCheck>("minio", tags: ["ready"]);
builder.Services.AddProblemDetails();
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
if (allowedOrigins.Length > 0)
{
    app.UseCors("Client");
}
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapAuthenticationEndpoints();
app.MapUserProfileEndpoints();
app.MapFriendEndpoints();
app.MapMessageEndpoints();
app.MapHub<MessagesHub>("/hubs/messages");
app.MapPostEndpoints();
app.MapReportEndpoints();
app.MapAdminEndpoints();
app.MapMediaEndpoints();

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
