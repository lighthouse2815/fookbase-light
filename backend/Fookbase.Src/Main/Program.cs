using Fookbase.Api.Modules.Identity.Config;
using System.Text;
using Fookbase.Api;
using Fookbase.Api.Shared.ErrorHandling;
using Fookbase.Api.Modules.Friends.Endpoints;
using Fookbase.Api.Modules.Identity.Endpoints;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Media.Endpoints;
using Fookbase.Api.Modules.Messages.Endpoints;
using Fookbase.Api.Modules.Posts.Endpoints;
using Fookbase.Api.Modules.Users.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddFriendsModule(builder.Configuration);
builder.Services.AddMessagesModule(builder.Configuration);
builder.Services.AddPostsModule(builder.Configuration);
builder.Services.AddMediaModule(builder.Configuration);
builder.Services.AddApplicationUseCases();

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
app.MapMessageEndpoints();
app.MapPostEndpoints();
app.MapMediaEndpoints();

app.Run();

public partial class Program;
