using System.Security.Cryptography;
using Fookbase.Api.Modules.Identity.Abstractions;
using Fookbase.Api.Modules.Identity.Common;
using Fookbase.Api.Modules.Identity.DTOs.Requests;
using Fookbase.Api.Modules.Identity.DTOs.Responses;
using Fookbase.Api.Modules.Identity.Entities;
using Fookbase.Api.Modules.Identity.Services;
using Fookbase.Api.Modules.Users.Entities;
using Fookbase.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fookbase.Identity.Api.IntegrationTests;

public sealed class OtpServiceTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Password_reset_rejects_correct_code_after_five_wrong_attempts()
    {
        using var scope = factory.Services.CreateScope();
        var (user, contact) = await CreatePhoneUserAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<OtpService>();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        await service.SendPasswordResetAsync(user.Id, contact);
        var code = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(contact.Value);
        var wrongCode = code == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<BusinessException>(() => service.VerifyPasswordResetAsync(user.Id, contact.Value, wrongCode));
        }

        var failure = await Assert.ThrowsAsync<BusinessException>(() => service.VerifyPasswordResetAsync(user.Id, contact.Value, code));
        Assert.Equal(ErrorCode.InvalidPasswordReset.Code, failure.Error.Code);
        var challenge = await db.PasswordResetOtps.AsNoTracking().SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(5, challenge.FailedAttemptCount);
        Assert.Null(challenge.ConsumedAtUtc);
    }

    [Fact]
    public async Task Password_reset_consumption_rejects_a_second_verified_request()
    {
        using var scope = factory.Services.CreateScope();
        var (user, contact) = await CreatePhoneUserAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<OtpService>();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        await service.SendPasswordResetAsync(user.Id, contact);
        var code = factory.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(contact.Value);
        var first = await service.VerifyPasswordResetAsync(user.Id, contact.Value, code);
        var second = await service.VerifyPasswordResetAsync(user.Id, contact.Value, code);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await service.ConsumePasswordResetAsync(first.Challenge, code, first.VerifiedAtUtc);
            await transaction.CommitAsync();
        }

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var failure = await Assert.ThrowsAsync<BusinessException>(() =>
                service.ConsumePasswordResetAsync(second.Challenge, code, second.VerifiedAtUtc));
            Assert.Equal(ErrorCode.InvalidPasswordReset.Code, failure.Error.Code);
        }

        await Assert.ThrowsAsync<BusinessException>(() => service.VerifyPasswordResetAsync(user.Id, contact.Value, code));
    }

    [Fact]
    public async Task Failed_password_change_rolls_back_password_reset_otp()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<IdentityOptions>(options => options.Password.RequireDigit = true)));
        using var scope = app.Services.CreateScope();
        var (user, contact) = await CreatePhoneUserAsync(scope.ServiceProvider);
        var service = scope.ServiceProvider.GetRequiredService<OtpService>();
        var authentication = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
        var security = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        Assert.IsType<AuthenticationResponse>(await authentication.LoginAsync(new LoginRequest(contact.Value, "Password123!"), null));
        await service.SendPasswordResetAsync(user.Id, contact);
        var code = app.Services.GetRequiredService<TestContactOtpSender>().LastCodeFor(contact.Value);

        var failure = await Assert.ThrowsAsync<BusinessException>(() => security.ResetPasswordAsync(
            new ResetPasswordRequest(contact.Value, null, "NoDigits!", "NoDigits!", code)));

        Assert.Equal(ErrorCode.ValidationFailed.Code, failure.Error.Code);
        Assert.Contains("PasswordRequiresDigit", failure.Error.Details!.Keys);
        Assert.Null((await db.PasswordResetOtps.AsNoTracking().SingleAsync(item => item.UserId == user.Id)).ConsumedAtUtc);
        Assert.Null((await db.RefreshTokens.AsNoTracking().SingleAsync(item => item.UserId == user.Id)).RevokedAt);

        await security.ResetPasswordAsync(new ResetPasswordRequest(contact.Value, null, "NewPassword123!", "NewPassword123!", code));
        Assert.NotNull((await db.PasswordResetOtps.AsNoTracking().SingleAsync(item => item.UserId == user.Id)).ConsumedAtUtc);
        Assert.NotNull((await db.RefreshTokens.AsNoTracking().SingleAsync(item => item.UserId == user.Id)).RevokedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delivery_failure_removes_the_challenge_and_preserves_the_purpose_error(bool registration)
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IContactOtpSender>();
            services.AddSingleton<IContactOtpSender>(new FailingOtpSender());
        }));
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OtpService>();
        var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
        if (registration)
        {
            var contact = ContactIdentifier.Parse($"otp-{Guid.NewGuid():N}@example.test");
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var now = DateTimeOffset.UtcNow;
            var candidate = new User(Guid.NewGuid(), contact.Value, "pending", now);
            var passwordHash = manager.PasswordHasher.HashPassword(candidate, "Password123!");

            var failure = await Assert.ThrowsAsync<BusinessException>(() => service.StartRegistrationAsync(
                contact, passwordHash, "OTP", "Test", new DateOnly(2000, 1, 2), Gender.Other, now));

            Assert.Equal("email_delivery_unavailable", failure.Error.Code);
            Assert.False(await db.RegistrationChallenges.AnyAsync(item => item.Contact == contact.Value));
        }
        else
        {
            var (user, contact) = await CreatePhoneUserAsync(scope.ServiceProvider);
            var failure = await Assert.ThrowsAsync<BusinessException>(() => service.SendPasswordResetAsync(user.Id, contact));

            Assert.Equal(ErrorCode.SmsUnavailable.Code, failure.Error.Code);
            Assert.False(await db.PasswordResetOtps.AnyAsync(item => item.UserId == user.Id));
        }
    }

    private static async Task<(User User, ContactIdentifier Contact)> CreatePhoneUserAsync(IServiceProvider services)
    {
        var contact = ContactIdentifier.Parse($"09{RandomNumberGenerator.GetInt32(10_000_000, 99_999_999)}");
        var user = new User(Guid.NewGuid(), null, $"otp-{Guid.NewGuid():N}"[..32], DateTimeOffset.UtcNow)
        {
            PhoneNumber = contact.Value,
            PhoneNumberConfirmed = true
        };
        var creation = await services.GetRequiredService<UserManager<User>>().CreateAsync(user, "Password123!");
        Assert.True(creation.Succeeded, string.Join("; ", creation.Errors.Select(error => error.Description)));
        return (user, contact);
    }

    private sealed class FailingOtpSender : IContactOtpSender
    {
        public Task SendAsync(ContactIdentifier contact, string code, CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("OTP gateway is unavailable."));
    }
}
