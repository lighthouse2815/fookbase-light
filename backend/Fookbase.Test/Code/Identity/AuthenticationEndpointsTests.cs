using Fookbase.Api.Shared.ErrorHandling;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Identity.Data;
using Fookbase.Api.Modules.Identity.Domain.Enums;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class AuthenticationEndpointsTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Email_registration_creates_no_user_until_correct_otp_is_verified()
    {
        var email = $"otp-{Guid.NewGuid():N}@example.test";
        using var client = factory.CreateClient();

        var start = await client.PostAsJsonAsync(
            "/api/auth/registration/start",
            new RegistrationStartRequest(
                "An",
                "Nguyễn",
                new DateOnly(2000, 1, 2),
                "preferNotToSay",
                email,
                "Password123!"));

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.Null(await userManager.FindByEmailAsync(email));

        var challenge = await start.Content.ReadApiDataAsync<RegistrationChallengeResponse>();
        Assert.NotNull(challenge);
        var code = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(email);
        var verify = await client.PostAsJsonAsync(
            "/api/auth/registration/verify",
            new RegistrationVerifyRequest(challenge!.ChallengeId, code));

        Assert.Equal(HttpStatusCode.Created, verify.StatusCode);
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
        Assert.NotNull(await dbContext.UserProfiles.SingleOrDefaultAsync(profile => profile.UserId == user.Id));
        Assert.NotNull(await dbContext.UserPrivacySettings.SingleOrDefaultAsync(settings => settings.UserId == user.Id));
    }

    [Fact]
    public async Task Phone_registration_normalizes_confirms_phone_and_initializes_profile()
    {
        var phone = $"09{RandomNumberGenerator.GetInt32(10_000_000, 99_999_999)}";
        using var client = factory.CreateClient();
        var start = await client.PostAsJsonAsync(
            "/api/auth/registration/start",
            new RegistrationStartRequest(
                "Nguyễn",
                "An",
                new DateOnly(2000, 1, 2),
                "female",
                phone,
                "Password123!"));

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var challenge = await start.Content.ReadApiDataAsync<RegistrationChallengeResponse>();
        Assert.NotNull(challenge);
        var normalizedPhone = $"+84{phone[1..]}";
        var code = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(normalizedPhone);
        var verify = await client.PostAsJsonAsync(
            "/api/auth/registration/verify",
            new RegistrationVerifyRequest(challenge!.ChallengeId, code));

        Assert.Equal(HttpStatusCode.Created, verify.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.PhoneNumber == normalizedPhone);
        var profile = await dbContext.UserProfiles.SingleAsync(item => item.UserId == user.Id);
        Assert.True(user.PhoneNumberConfirmed);
        Assert.Null(user.Email);
        Assert.Equal("Nguyễn An", profile.DisplayName);
        Assert.Equal("nguyen.an", profile.Username);
        Assert.Equal(new DateOnly(2000, 1, 2), profile.DateOfBirth);
        Assert.Equal(Gender.Female, profile.Gender);
    }

    [Fact]
    public async Task Phone_only_user_can_login_with_a_spaced_local_number()
    {
        var phone = $"09{RandomNumberGenerator.GetInt32(10_000_000, 99_999_999)}";
        using var client = factory.CreateClient();
        var start = await client.PostAsJsonAsync(
            "/api/auth/registration/start",
            new RegistrationStartRequest("An", "Nguyễn", new DateOnly(2000, 1, 2), "male", phone, "Password123!"));
        var challenge = await start.Content.ReadApiDataAsync<RegistrationChallengeResponse>();
        Assert.NotNull(challenge);
        var code = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor($"+84{phone[1..]}");
        var verify = await client.PostAsJsonAsync(
            "/api/auth/registration/verify",
            new RegistrationVerifyRequest(challenge!.ChallengeId, code));
        Assert.Equal(HttpStatusCode.Created, verify.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { identifier = $"{phone[..4]} {phone[4..7]} {phone[7..]}", password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var authentication = await ReadAuthenticationResponseAsync(login);
        Assert.Equal($"+84{phone[1..]}", authentication.User.PhoneNumber);
    }

    [Fact]
    public async Task Legacy_login_email_json_still_works()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = account.Email, password = account.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Browser_authentication_uses_http_only_refresh_cookie()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var registration = await RegisterAsync(client, account);
        client.DefaultRequestHeaders.Remove("X-Fookbase-Auth-Transport");
        client.DefaultRequestHeaders.Add("X-Fookbase-Auth-Transport", "cookie:web");

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = account.Email, password = account.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = ReadRefreshCookie(login);
        var browserLogin = await login.Content.ReadApiDataAsync<BrowserAuthenticationResponse>();
        Assert.Equal(registration.User.Id, browserLogin.User.Id);
        Assert.False(await HasDataPropertyAsync(login, "refreshToken"));
        Assert.Contains("HttpOnly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);

        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';', 2)[0]);
        client.DefaultRequestHeaders.Remove("X-Fookbase-Auth-Transport");
        var csrfAttempt = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, csrfAttempt.StatusCode);

        client.DefaultRequestHeaders.Add("X-Fookbase-Auth-Transport", "cookie:web");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", browserLogin.AccessToken);
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { });

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshedCookie = ReadRefreshCookie(refresh);
        var browserRefresh = await refresh.Content.ReadApiDataAsync<BrowserAuthenticationResponse>();
        Assert.NotEqual(browserLogin.AccessToken, browserRefresh.AccessToken);
        Assert.False(await HasDataPropertyAsync(refresh, "refreshToken"));

        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", refreshedCookie.Split(';', 2)[0]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", browserRefresh.AccessToken);
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Contains("fookbase.web.refresh=", string.Join(";", logout.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phone_password_reset_uses_otp_and_revokes_existing_sessions()
    {
        var phone = $"09{RandomNumberGenerator.GetInt32(10_000_000, 99_999_999)}";
        const string oldPassword = "Password123!";
        const string newPassword = "NewPassword123!";
        using var client = factory.CreateClient();
        var start = await client.PostAsJsonAsync(
            "/api/auth/registration/start",
            new RegistrationStartRequest("An", "Nguyễn", new DateOnly(2000, 1, 2), "male", phone, oldPassword));
        var registration = await start.Content.ReadApiDataAsync<RegistrationChallengeResponse>();
        Assert.NotNull(registration);
        var normalizedPhone = $"+84{phone[1..]}";
        var registrationCode = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(normalizedPhone);
        var verify = await client.PostAsJsonAsync(
            "/api/auth/registration/verify",
            new RegistrationVerifyRequest(registration!.ChallengeId, registrationCode));
        var initialSession = await ReadAuthenticationResponseAsync(verify);

        var forgot = await client.PostAsJsonAsync(
            "/api/auth/password/forgot",
            new { identifier = phone });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var resetCode = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(normalizedPhone);

        var reset = await client.PostAsJsonAsync(
            "/api/auth/password/reset",
            new { identifier = phone, code = resetCode, password = newPassword, confirmPassword = newPassword });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/auth/login", new { identifier = phone, password = oldPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/auth/login", new { identifier = phone, password = newPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshRequest(initialSession.RefreshToken))).StatusCode);
    }

    [Fact]
    public async Task Registration_verification_locks_after_five_incorrect_codes_and_cannot_be_replayed()
    {
        var email = $"otp-lock-{Guid.NewGuid():N}@example.test";
        using var client = factory.CreateClient();
        var start = await client.PostAsJsonAsync(
            "/api/auth/registration/start",
            new RegistrationStartRequest("An", "Nguyễn", new DateOnly(2000, 1, 2), "other", email, "Password123!"));
        var challenge = await start.Content.ReadApiDataAsync<RegistrationChallengeResponse>();
        Assert.NotNull(challenge);

        var code = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(email);
        var incorrectCode = code == "000000" ? "000001" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalid = await client.PostAsJsonAsync(
                "/api/auth/registration/verify",
                new RegistrationVerifyRequest(challenge!.ChallengeId, incorrectCode));
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        }

        var locked = await client.PostAsJsonAsync(
            "/api/auth/registration/verify",
            new RegistrationVerifyRequest(challenge!.ChallengeId, code));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task Registration_start_obeys_the_same_sixty_second_delivery_cooldown()
    {
        var email = $"otp-cooldown-{Guid.NewGuid():N}@example.test";
        using var client = factory.CreateClient();
        var request = new RegistrationStartRequest("An", "Nguyễn", new DateOnly(2000, 1, 2), "other", email, "Password123!");

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/registration/start", request)).StatusCode);
        var repeated = await client.PostAsJsonAsync("/api/auth/registration/start", request);

        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }

    [Fact]
    public void Registration_challenge_allows_at_most_five_sends_per_hour()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = new RegistrationChallenge(
            new ContactIdentifier(ContactKind.Email, "resend@example.test"),
            new string('A', 64),
            "identity-password-hash",
            "Nguyễn",
            "An",
            new DateOnly(2000, 1, 2),
            Gender.PreferNotToSay,
            now);

        for (var send = 1; send < 5; send++)
        {
            Assert.True(challenge.TryResend(new string((char)('A' + send), 64), now.AddMinutes(send)));
        }

        Assert.False(challenge.TryResend(new string('F', 64), now.AddMinutes(5)));
    }

    [Theory]
    [InlineData("0912 345 678", "+84912345678")]
    [InlineData("+84912345678", "+84912345678")]
    [InlineData("84912345678", "+84912345678")]
    public void Vietnamese_phone_is_normalized_to_e164(string raw, string expected)
    {
        Assert.True(ContactIdentifier.TryParse(raw, out var contact));
        Assert.Equal(ContactKind.Phone, contact.Kind);
        Assert.Equal(expected, contact.Value);
    }

    [Theory]
    [InlineData("0212345678")]
    [InlineData("12345")]
    [InlineData("+12025550123")]
    public void Unsupported_phone_is_rejected(string raw) =>
        Assert.False(ContactIdentifier.TryParse(raw, out _));

    [Fact]
    public void Registration_challenge_locks_after_five_invalid_codes()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = new RegistrationChallenge(
            new ContactIdentifier(ContactKind.Email, "person@example.test"),
            new string('A', 64),
            "identity-password-hash",
            "Nguyễn",
            "An",
            new DateOnly(2000, 1, 2),
            Gender.PreferNotToSay,
            now);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            challenge.RegisterFailedAttempt(now.AddSeconds(attempt));
        }

        Assert.False(challenge.IsUsableAt(now.AddSeconds(5)));
    }

    [Fact]
    public async Task Google_providers_reports_enabled_state()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("data").GetProperty("google").GetBoolean());
    }

    [Fact]
    public async Task Google_start_rejects_unknown_client()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/auth/google/start?client=unknown");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Google_callback_redirects_only_to_fixed_client_login_route()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Test-Google-Sub", $"google-sub-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add("X-Test-Google-Email", $"google-callback-{Guid.NewGuid():N}@example.test");
        client.DefaultRequestHeaders.Add("X-Test-Google-Email-Verified", "true");

        var response = await client.GetAsync("/api/auth/google/callback?client=web");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("http://web.example.test/login?", location, StringComparison.Ordinal);
        Assert.Contains("provider=google", location, StringComparison.Ordinal);
        Assert.Contains("code=", location, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Google_exchange_rejects_replayed_completion_code()
    {
        using var scope = factory.Services.CreateScope();
        var googleAuthentication = scope.ServiceProvider.GetRequiredService<GoogleAuthenticationService>();
        var completion = await googleAuthentication.CreateCompletionAsync(
            "web",
            $"google-sub-{Guid.NewGuid():N}",
            $"google-replay-{Guid.NewGuid():N}@example.test",
            emailVerified: true);
        Assert.NotNull(completion);

        using var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/auth/google/exchange", new { code = completion.Code, client = "web" });
        var second = await client.PostAsJsonAsync("/api/auth/google/exchange", new { code = completion.Code, client = "web" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Google_link_rejects_wrong_password()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);
        using var scope = factory.Services.CreateScope();
        var googleAuthentication = scope.ServiceProvider.GetRequiredService<GoogleAuthenticationService>();
        var completion = await googleAuthentication.CreateCompletionAsync(
            "web",
            $"google-sub-{Guid.NewGuid():N}",
            account.Email,
            emailVerified: true);
        Assert.NotNull(completion);

        var response = await client.PostAsJsonAsync(
            "/api/auth/google/link",
            new { code = completion.Code, password = "wrong-password", client = "web" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task External_login_completion_stores_hash_and_can_only_be_consumed_once()
    {
        var now = DateTimeOffset.UtcNow;
        var rawCode = "raw-completion";
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var email = $"ticket-{Guid.NewGuid():N}@example.test";
        var user = new User(Guid.NewGuid(), email, $"ticket-{Guid.NewGuid():N}", now)
        {
            EmailConfirmed = true
        };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);

        var completion = new ExternalLoginTicket(
            codeHash,
            ExternalLoginTicketPurpose.IssueSession,
            "web",
            "Google",
            "google-subject",
            "person@example.test",
            user.Id,
            now);

        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        dbContext.ExternalLoginTickets.Add(completion);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var stored = await dbContext.ExternalLoginTickets.SingleAsync(item => item.Id == completion.Id);
        Assert.Equal(64, stored.CodeHash.Length);
        Assert.NotEqual(rawCode, stored.CodeHash);
        Assert.True(stored.TryConsumeAt(now.AddSeconds(1)));
        Assert.False(stored.TryConsumeAt(now.AddSeconds(2)));
    }

    [Fact]
    public async Task Google_verified_new_email_creates_confirmed_user_profile_privacy_and_login()
    {
        using var scope = factory.Services.CreateScope();
        var googleAuthentication = scope.ServiceProvider.GetRequiredService<GoogleAuthenticationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var email = $"google-{Guid.NewGuid():N}@example.test";

        var completion = await googleAuthentication.CreateCompletionAsync(
            "web",
            $"google-sub-{Guid.NewGuid():N}",
            email,
            emailVerified: true);

        Assert.NotNull(completion);
        Assert.False(completion.RequiresPassword);

        var exchange = await googleAuthentication.ExchangeAsync(completion.Code, "web", null);
        var session = Assert.IsType<AuthenticationResponse>(exchange);
        var user = await userManager.FindByIdAsync(session.User.Id.ToString());

        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
        Assert.NotNull(await dbContext.UserProfiles.SingleOrDefaultAsync(item => item.UserId == user.Id));
        Assert.NotNull(await dbContext.UserPrivacySettings.SingleOrDefaultAsync(item => item.UserId == user.Id));
        Assert.Contains(await userManager.GetLoginsAsync(user), login =>
            login.LoginProvider == "Google" && login.ProviderKey.StartsWith("google-sub-"));
    }

    [Fact]
    public async Task Google_unverified_email_does_not_create_or_link_an_account()
    {
        using var scope = factory.Services.CreateScope();
        var googleAuthentication = scope.ServiceProvider.GetRequiredService<GoogleAuthenticationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var email = $"google-unverified-{Guid.NewGuid():N}@example.test";

        var exception = await Assert.ThrowsAsync<BusinessException>(() => googleAuthentication.CreateCompletionAsync(
            "web",
            $"google-sub-{Guid.NewGuid():N}",
            email,
            emailVerified: false));

        Assert.Equal("invalid_google_identity", exception.Error.Code);
        Assert.Null(await userManager.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Google_existing_email_rejects_wrong_password_before_linking()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        using var scope = factory.Services.CreateScope();
        var googleAuthentication = scope.ServiceProvider.GetRequiredService<GoogleAuthenticationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var user = await userManager.FindByEmailAsync(account.Email);

        var completion = await googleAuthentication.CreateCompletionAsync(
            "web",
            $"google-sub-{Guid.NewGuid():N}",
            account.Email,
            emailVerified: true);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => googleAuthentication.LinkExistingAsync(
            completion.Code,
            "web",
            "wrong-password",
            null));

        Assert.NotNull(completion);
        Assert.True(completion.RequiresPassword);
        Assert.Equal("invalid_credentials", exception.Error.Code);
        Assert.DoesNotContain(await userManager.GetLoginsAsync(user!), login => login.LoginProvider == "Google");
    }

    [Fact]
    public async Task Google_existing_email_links_after_correct_password_and_issues_session()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        using var scope = factory.Services.CreateScope();
        var googleAuthentication = scope.ServiceProvider.GetRequiredService<GoogleAuthenticationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var user = await userManager.FindByEmailAsync(account.Email);
        var providerKey = $"google-sub-{Guid.NewGuid():N}";

        var completion = await googleAuthentication.CreateCompletionAsync(
            "web",
            providerKey,
            account.Email,
            emailVerified: true);
        Assert.NotNull(completion);
        Assert.True(completion.RequiresPassword);

        var linked = await googleAuthentication.LinkExistingAsync(
            completion.Code,
            "web",
            account.Password,
            null);

        Assert.NotNull(linked);
        Assert.IsType<AuthenticationResponse>(linked);
        Assert.Contains(await userManager.GetLoginsAsync(user!), login =>
            login.LoginProvider == "Google" && login.ProviderKey == providerKey);
    }

    [Fact]
    public async Task Register_with_malformed_json_returns_bad_request()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_succeeds_and_stores_only_hashed_secrets()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(account.Email, account.Username, account.Password));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authentication = await ReadAuthenticationResponseAsync(response);
        Assert.Equal(account.Email, authentication.User.Email);
        Assert.Equal(account.Username, authentication.User.Username);
        Assert.False(authentication.User.EmailConfirmed);
        Assert.False(string.IsNullOrWhiteSpace(authentication.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authentication.RefreshToken));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authentication.AccessToken);
        Assert.Equal(authentication.User.Id.ToString(), jwt.Subject);
        Assert.Equal(account.Email, jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(account.Username, jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Id));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == authentication.User.Id);
        var refreshToken = await dbContext.RefreshTokens
            .SingleAsync(item => item.UserId == authentication.User.Id);
        var profile = await dbContext.UserProfiles
            .SingleAsync(item => item.UserId == authentication.User.Id);
        Assert.NotEqual(account.Password, user.PasswordHash);
        Assert.Equal(account.Email.ToUpperInvariant(), user.NormalizedEmail);
        Assert.Equal(account.Username.ToUpperInvariant(), user.NormalizedUserName);
        Assert.NotEqual(authentication.RefreshToken, refreshToken.TokenHash);
        Assert.Equal(Hash(authentication.RefreshToken), refreshToken.TokenHash);
        Assert.Equal(account.Username, profile.Username);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_conflict()
    {
        var first = CreateUniqueAccount();
        var second = CreateUniqueAccount() with { Email = first.Email };
        using var client = factory.CreateClient();

        await RegisterAsync(client, first);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(second.Email, second.Username, second.Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_duplicate_username_returns_conflict()
    {
        var first = CreateUniqueAccount();
        var second = CreateUniqueAccount() with { Username = first.Username };
        using var client = factory.CreateClient();

        await RegisterAsync(client, first);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(second.Email, second.Username, second.Password));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_correct_password_returns_token_pair()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, account.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authentication = await ReadAuthenticationResponseAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(authentication.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(authentication.RefreshToken));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_without_access_token_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_valid_access_token_returns_authenticated_user()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var authentication = await RegisterAsync(client, account);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authentication.AccessToken);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadApiDataAsync<AuthenticatedUserResponse>();
        Assert.NotNull(user);
        Assert.Equal(authentication.User.Id, user!.Id);
        Assert.Equal(authentication.User.Email, user.Email);
        Assert.Equal(authentication.User.Username, user.Username);
        Assert.Equal(authentication.User.EmailConfirmed, user.EmailConfirmed);
        Assert.Equal(authentication.User.Roles, user.Roles);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_old_token_cannot_be_reused()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var original = await RegisterAsync(client, account);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(original.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await ReadAuthenticationResponseAsync(refreshResponse);
        Assert.NotEqual(original.AccessToken, rotated.AccessToken);
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);

        var reuseResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_revoked_token_cannot_refresh()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var authentication = await RegisterAsync(client, account);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authentication.AccessToken);

        var logoutResponse = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest(authentication.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(authentication.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Email_verification_confirms_the_registered_account()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        await RegisterAsync(client, account);

        var email = GetLatestEmail(account.Email, "Verify your Fookbase email");
        var (emailAddress, token) = GetLinkParameters(email.HtmlBody);
        var response = await client.PostAsJsonAsync(
            "/api/auth/email/verify",
            new VerifyEmailRequest(emailAddress, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, account.Password));
        var authentication = await ReadAuthenticationResponseAsync(loginResponse);
        Assert.True(authentication.User.EmailConfirmed);
    }

    [Fact]
    public async Task Password_reset_revokes_existing_sessions_and_allows_new_password()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var original = await RegisterAsync(client, account);

        var requestResponse = await client.PostAsJsonAsync(
            "/api/auth/password/forgot",
            new ForgotPasswordRequest(account.Email));
        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var email = GetLatestEmail(account.Email, "Reset your Fookbase password");
        var (emailAddress, token) = GetLinkParameters(email.HtmlBody);
        const string newPassword = "New-password-123!";
        var resetResponse = await client.PostAsJsonAsync(
            "/api/auth/password/reset",
            new ResetPasswordRequest(emailAddress, token, newPassword, newPassword));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, newPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Password_change_rotates_the_current_session()
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var original = await RegisterAsync(client, account);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", original.AccessToken);

        const string newPassword = "New-password-123!";
        var changeResponse = await client.PostAsJsonAsync(
            "/api/auth/password/change",
            new ChangePasswordRequest(account.Password, newPassword, newPassword));

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        var rotated = await ReadAuthenticationResponseAsync(changeResponse);
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);

        client.DefaultRequestHeaders.Authorization = null;
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(original.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(account.Email, newPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Theory]
    [InlineData("New-password-123!", "different-password", "ConfirmPassword")]
    [InlineData("short", "short", "NewPassword")]
    [InlineData(null, null, "NewPassword")]
    [InlineData("New-password-123!", null, "ConfirmPassword")]
    public async Task Password_change_returns_validation_details_for_invalid_input(
        string? newPassword, string? confirmation, string errorField)
    {
        var account = CreateUniqueAccount();
        using var client = factory.CreateClient();
        var original = await RegisterAsync(client, account);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", original.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/auth/password/change",
            new ChangePasswordRequest(account.Password, newPassword, confirmation));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_failed", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.NotEmpty(document.RootElement.GetProperty("error").GetProperty("details").GetProperty(errorField).EnumerateArray());
    }

    private static async Task<AuthenticationResponse> RegisterAsync(
        HttpClient client,
        TestAccount account)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(account.Email, account.Username, account.Password));

        response.EnsureSuccessStatusCode();
        return await ReadAuthenticationResponseAsync(response);
    }

    private static async Task<AuthenticationResponse> ReadAuthenticationResponseAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadApiDataAsync<AuthenticationResponse>()
        ?? throw new InvalidOperationException("Authentication response body was empty.");

    private static string ReadRefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("fookbase.web.refresh=", StringComparison.Ordinal));

    private static async Task<bool> HasDataPropertyAsync(HttpResponseMessage response, string propertyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").TryGetProperty(propertyName, out _);
    }

    private static TestAccount CreateUniqueAccount()
    {
        var suffix = Guid.NewGuid().ToString("N")[..16];
        return new TestAccount(
            $"user-{suffix}@example.com",
            $"user_{suffix}",
            "Password123!");
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private SentEmail GetLatestEmail(string recipientEmail, string subject) =>
        factory.Services.GetRequiredService<TestEmailSender>().Emails
            .LastOrDefault(email =>
                email.RecipientEmail.Equals(recipientEmail, StringComparison.OrdinalIgnoreCase) &&
                email.Subject == subject)
        ?? throw new InvalidOperationException($"No '{subject}' email was sent to {recipientEmail}.");

    private static (string Email, string Token) GetLinkParameters(string htmlBody)
    {
        var linkStart = htmlBody.IndexOf("href=\"", StringComparison.Ordinal);
        var linkEnd = linkStart < 0 ? -1 : htmlBody.IndexOf('"', linkStart + 6);
        if (linkEnd < 0)
        {
            throw new InvalidOperationException("Email did not contain a link.");
        }

        var uri = new Uri(htmlBody[(linkStart + 6)..linkEnd]);
        var parameters = uri.Query[1..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => pair[0],
                pair => pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : string.Empty,
                StringComparer.Ordinal);

        return (
            parameters.GetValueOrDefault("email") ?? throw new InvalidOperationException("Email link was missing an email."),
            parameters.GetValueOrDefault("token") ?? throw new InvalidOperationException("Email link was missing a token."));
    }

    private sealed record TestAccount(string Email, string Username, string Password);
}
