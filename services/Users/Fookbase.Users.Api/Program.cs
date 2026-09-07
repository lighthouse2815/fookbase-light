using System.Text;
using Fookbase.Users.Api.Endpoints;
using Fookbase.Users.Api.ErrorHandling;
using Fookbase.Users.Infrastructure;
using Fookbase.Users.Infrastructure.Authentication;
using Fookbase.Users.Infrastructure.IntegrationEvents;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var usersConnectionString = builder.Configuration.GetConnectionString("UsersDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'UsersDatabase' is required. Copy .env.example to .env and load it before starting the service.");
var jwtOptions = builder.Configuration
    .GetSection(JwtValidationOptions.SectionName)
    .Get<JwtValidationOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
var rabbitMqOptions = builder.Configuration
    .GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException("RabbitMQ configuration is required.");

jwtOptions.Validate();
builder.Services.AddUsersInfrastructure(usersConnectionString, rabbitMqOptions);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
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
app.MapUserProfileEndpoints();

app.Run();

public partial class Program;
