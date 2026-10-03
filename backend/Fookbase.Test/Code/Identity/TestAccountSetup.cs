using System.Net;
using System.Net.Http.Json;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Users.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

internal static class TestAccountSetup
{
    public static async Task<AuthenticationResponse> CreateAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        string email,
        string username,
        string password)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
            var user = new User(Guid.NewGuid(), email, username, now);
            var creation = await userManager.CreateAsync(user, password);
            Assert.True(creation.Succeeded,
                string.Join("; ", creation.Errors.Select(error => error.Description)));
            await scope.ServiceProvider.GetRequiredService<UserProfileService>()
                .EnsureCreatedAsync(user.Id, username);
            await scope.ServiceProvider.GetRequiredService<UserPrivacySettingsService>()
                .EnsureCreatedAsync(user.Id);
        }

        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadApiDataAsync<AuthenticationResponse>()
            ?? throw new InvalidOperationException("Authentication response body was empty.");
    }
}
