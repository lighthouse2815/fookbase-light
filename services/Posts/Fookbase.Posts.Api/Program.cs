using System.Text;
using Fookbase.Posts.Api.Endpoints;
using Fookbase.Posts.Api.ErrorHandling;
using Fookbase.Posts.Infrastructure;
using Fookbase.Posts.Infrastructure.Authentication;
using Fookbase.Posts.Infrastructure.IntegrationEvents;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostsDatabase")
    ?? throw new InvalidOperationException("Connection string 'PostsDatabase' is required.");
var jwtOptions = builder.Configuration.GetSection(JwtValidationOptions.SectionName)
    .Get<JwtValidationOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
var rabbitMqOptions = builder.Configuration.GetSection(RabbitMqOptions.SectionName)
    .Get<RabbitMqOptions>()
    ?? throw new InvalidOperationException("RabbitMQ configuration is required.");
var outboxOptions = builder.Configuration.GetSection(OutboxOptions.SectionName)
    .Get<OutboxOptions>()
    ?? new OutboxOptions();

jwtOptions.Validate();
builder.Services.AddPostsInfrastructure(connectionString, rabbitMqOptions, outboxOptions);
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
app.MapPostEndpoints();

app.Run();

public partial class Program;
