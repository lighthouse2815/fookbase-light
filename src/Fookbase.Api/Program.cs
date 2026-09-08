using System.Text;
using Fookbase.Api.ErrorHandling;
using Fookbase.Api.IntegrationEvents;
using Fookbase.Api.Media;
using Fookbase.Api.Endpoints.Friends;
using Fookbase.Friends.Infrastructure;
using Fookbase.Api.Endpoints.Identity;
using Fookbase.Identity.Infrastructure;
using Fookbase.Identity.Infrastructure.Authentication;
using Fookbase.Api.Endpoints.Media;
using Fookbase.Media.Application.Media;
using Fookbase.Media.Infrastructure;
using Fookbase.Media.Infrastructure.Storage;
using Fookbase.Api.Endpoints.Posts;
using Fookbase.Posts.Application.Abstractions;
using Fookbase.Posts.Application.Posts;
using Fookbase.Posts.Infrastructure;
using Fookbase.Api.Endpoints.Users;
using Fookbase.Users.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using FriendsOutboxOptions = Fookbase.Friends.Infrastructure.IntegrationEvents.OutboxOptions;
using IdentityOutboxOptions = Fookbase.Identity.Infrastructure.IntegrationEvents.OutboxOptions;
using MediaOutboxOptions = Fookbase.Media.Infrastructure.IntegrationEvents.OutboxOptions;
using PostsOutboxOptions = Fookbase.Posts.Infrastructure.IntegrationEvents.OutboxOptions;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
var minioOptions = builder.Configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>()
    ?? throw new InvalidOperationException("MinIO configuration is required.");
var mediaOptions = builder.Configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>()
    ?? new MediaOptions();
var postsOptions = builder.Configuration.GetSection(PostsOptions.SectionName).Get<PostsOptions>()
    ?? new PostsOptions();

builder.Services.AddIdentityInfrastructure(
    RequiredConnectionString("IdentityDatabase"),
    jwtOptions,
    builder.Configuration.GetSection(IdentityOutboxOptions.SectionName).Get<IdentityOutboxOptions>()
        ?? new IdentityOutboxOptions());
builder.Services.AddUsersInfrastructure(RequiredConnectionString("UsersDatabase"));
builder.Services.AddFriendsInfrastructure(
    RequiredConnectionString("FriendsDatabase"),
    builder.Configuration.GetSection(FriendsOutboxOptions.SectionName).Get<FriendsOutboxOptions>()
        ?? new FriendsOutboxOptions());
builder.Services.AddPostsInfrastructure(
    RequiredConnectionString("PostsDatabase"),
    builder.Configuration.GetSection(PostsOutboxOptions.SectionName).Get<PostsOutboxOptions>()
        ?? new PostsOutboxOptions(),
    postsOptions);
builder.Services.AddMediaInfrastructure(
    RequiredConnectionString("MediaDatabase"),
    minioOptions,
    builder.Configuration.GetSection(MediaOutboxOptions.SectionName).Get<MediaOutboxOptions>()
        ?? new MediaOutboxOptions(),
    mediaOptions);

builder.Services.AddScoped<InProcessIntegrationEventPublisher>();
builder.Services.AddScoped<Fookbase.Identity.Application.Abstractions.IIntegrationEventPublisher>(
    provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
builder.Services.AddScoped<Fookbase.Friends.Application.Abstractions.IIntegrationEventPublisher>(
    provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
builder.Services.AddScoped<Fookbase.Posts.Application.Abstractions.IIntegrationEventPublisher>(
    provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
builder.Services.AddScoped<Fookbase.Media.Application.Abstractions.IIntegrationEventPublisher>(
    provider => provider.GetRequiredService<InProcessIntegrationEventPublisher>());
builder.Services.AddScoped<IMediaReadUrlClient, DirectMediaReadUrlClient>();

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
app.MapAuthenticationEndpoints();
app.MapUserProfileEndpoints();
app.MapFriendEndpoints();
app.MapPostEndpoints();
app.MapMediaEndpoints();

app.Run();

string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name)
    ?? throw new InvalidOperationException($"Connection string '{name}' is required.");

public partial class Program;
