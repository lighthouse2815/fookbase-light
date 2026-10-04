using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class MonolithEventFlowTests(MonolithApiFactory factory)
    : IClassFixture<MonolithApiFactory>
{
    [Fact]
    public async Task Registration_creates_the_user_profile_directly()
    {
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..16];
        var email = $"mono-{suffix}@example.com";
        using var start = await client.PostAsJsonAsync("/api/auth/registration/start",
            new RegistrationStartRequest("Mono", suffix, new DateOnly(2000, 1, 2), "other", email, "Password123!"));
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var challenge = await start.Content.ReadApiDataAsync<RegistrationChallengeResponse>();
        Assert.NotNull(challenge);
        var code = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(email);
        using var response = await client.PostAsJsonAsync("/api/auth/registration/verify",
            new RegistrationVerifyRequest(challenge.ChallengeId, code));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authentication = await response.Content.ReadApiDataAsync<AuthenticationResponse>();
        Assert.NotNull(authentication);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var profile = await dbContext.UserProfiles.SingleAsync(item => item.UserId == authentication.User.Id);
        Assert.Equal($"Mono {suffix}", profile.DisplayName);
        Assert.Equal(new DateOnly(2000, 1, 2), profile.DateOfBirth);
        Assert.True(await dbContext.UserPrivacySettings.AnyAsync(item => item.UserId == authentication.User.Id));
        Assert.NotNull((await dbContext.RegistrationChallenges.SingleAsync(item => item.Id == challenge.ChallengeId)).ConsumedAtUtc);
    }
}

public sealed class MonolithApiFactory : IdentityApiFactory;
