using Fookbase.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var identityConnectionString = builder.Configuration.GetConnectionString("IdentityDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'IdentityDatabase' is required. Copy .env.example to .env and load it before starting the service.");

builder.Services.AddIdentityInfrastructure(identityConnectionString);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
