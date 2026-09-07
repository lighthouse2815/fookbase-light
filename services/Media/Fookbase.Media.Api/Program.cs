using System.Text;
using Fookbase.Media.Api.Authentication;
using Fookbase.Media.Api.Endpoints;
using Fookbase.Media.Api.ErrorHandling;
using Fookbase.Media.Infrastructure;
using Fookbase.Media.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MediaDatabase")
    ?? throw new InvalidOperationException("Connection string 'MediaDatabase' is required.");
var jwtOptions = builder.Configuration.GetSection(JwtValidationOptions.SectionName)
    .Get<JwtValidationOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
var minioOptions = builder.Configuration.GetSection(MinioOptions.SectionName)
    .Get<MinioOptions>()
    ?? throw new InvalidOperationException("MinIO configuration is required.");

jwtOptions.Validate();
builder.Services.AddMediaInfrastructure(connectionString, minioOptions);
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
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapMediaEndpoints();

app.Run();

public partial class Program;
