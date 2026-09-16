# Contact OTP Registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Let a new user register and sign in with either a verified email or verified Vietnamese phone number, using an OTP before the account exists.

**Architecture:** The API persists a short-lived registration challenge containing password and OTP hashes only. A contact-specific sender delivers through SMTP or SpeedSMS. Atomic OTP consumption provisions the Identity user, profile, privacy settings, and normal JWT session; login and recovery resolve either contact type.

**Tech Stack:** .NET 10 minimal APIs, ASP.NET Core Identity, EF Core/Npgsql, HttpClient, React 19, TypeScript, Vite, xUnit.

**Spec:** docs/superpowers/specs/2026-09-16-contact-otp-registration-design.md

## Global Constraints

- Accept exactly one contact: valid email or Vietnamese mobile normalized to E.164 +84....
- Never persist/log plaintext OTP, password, SpeedSMS credentials, SMTP credentials, or full contact.
- OTP: six digits, 10-minute expiry, one use, 60-second resend cooldown, five sends/hour, five failed attempts.
- Create an account only after OTP verification; confirm the respective email or phone.
- Preserve JWT, refresh token, lockout, moderation, 2FA, Google, and existing email-account behavior.
- Sms:Enabled defaults false. Secret values use environment/secret store.
- Run focused tests plus relevant backend tests and web lint/build before every Vietnamese Conventional Commit; push green commits to origin.

## File structure

| Files | Responsibility |
| --- | --- |
| Identity/Config/SmsOptions.cs; Identity/Services/SpeedSmsSender.cs | Validate and call SpeedSMS with HttpClient. |
| Identity/Services/ContactIdentifier.cs; IContactOtpSender.cs | Normalize contact and route email/SMS OTP. |
| Identity/Entities/RegistrationChallenge.cs; configuration; migration | Store replay-safe pending-registration state. |
| Identity/Services/RegistrationChallengeService.cs; registration DTOs | Start, resend, verify, provision registrations. |
| Users/Entities/Gender.cs; UserProfile.cs | Persist private registration profile data. |
| AuthenticationService.cs; auth DTOs | Resolve email or phone for login/recovery. |
| frontend/web LoginPage, auth API/provider | Two-step registration/OTP UX. |

### Task 1: SMS OTP delivery foundation

**Files:**
- Create: backend/Fookbase.Src/Main/Code/Modules/Identity/Config/SmsOptions.cs
- Create: backend/Fookbase.Src/Main/Code/Modules/Identity/Services/IContactOtpSender.cs
- Create: backend/Fookbase.Src/Main/Code/Modules/Identity/Services/SpeedSmsSender.cs
- Create: backend/Fookbase.Src/Main/Code/Modules/Identity/Services/ContactOtpSender.cs
- Modify: backend/Fookbase.Src/Main/Code/Modules/Identity/DependencyInjection.cs; backend/Fookbase.Src/Main/appsettings.json; IdentityApiFactory.cs
- Test: ProductionRuntimeTests.cs; AuthenticationEndpointsTests.cs

**Interfaces:** Produce ContactKind, ContactIdentifier, IContactOtpSender.SendAsync(ContactIdentifier, string, CancellationToken), and SmsOptions(Enabled, AccessToken, Sender, BaseUrl).

- [ ] **Step 1: Write failing options and outbound request tests**

```csharp
[Fact]
public void Production_requires_sms_access_token_when_enabled()
{
    var values = TestConfiguration.ValidValues();
    values["Sms:Enabled"] = "true";
    Assert.Throws<InvalidOperationException>(() => TestConfiguration.StartProduction(values));
}

[Fact]
public async Task Phone_otp_sender_posts_the_otp_to_speed_sms()
{
    var handler = new RecordingHandler();
    await new SpeedSmsSender(new HttpClient(handler), EnabledSmsOptions())
        .SendAsync("+84912345678", "123456");
    Assert.Contains("123456", handler.Body);
}
```

- [ ] **Step 2: Run focused test and verify it fails**

Run: dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~sms_access_token|FullyQualifiedName~Phone_otp_sender"

Expected: FAIL because options and senders do not exist.

- [ ] **Step 3: Implement minimal safe delivery**

```csharp
public interface IContactOtpSender
{
    Task SendAsync(ContactIdentifier contact, string code, CancellationToken cancellationToken = default);
}
```

Add a typed HttpClient adapter for SpeedSMS, route email through IEmailSender, validate enabled options at startup, use safe disabled defaults, and replace the interface with a recording fake in IdentityApiFactory. Add no provider SDK dependency.

- [ ] **Step 4: Run focused tests**

Run the command in Step 2. Expected: PASS.

- [ ] **Step 5: Commit and push**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Identity backend/Fookbase.Src/Main/appsettings.json backend/Fookbase.Src/Tests/Identity
git commit -m "feat: thêm gửi OTP qua SMS"
git push origin HEAD
```

### Task 2: Contact parsing and challenge persistence

**Files:**
- Create: Identity/Services/ContactIdentifier.cs; Identity/Entities/RegistrationChallenge.cs; Identity/Data/Configurations/RegistrationChallengeConfiguration.cs
- Modify: FookbaseDbContext.cs; FookbaseDbContextModelSnapshot.cs
- Create: Code/Persistence/Migrations/<timestamp>_AddContactOtpRegistration.cs
- Test: AuthenticationEndpointsTests.cs

**Interfaces:** Produce ContactIdentifier.TryParse, RegistrationChallenge.Create, IsUsableAt, TryConsumeAt, RegisterFailedAttempt, and FookbaseDbContext.RegistrationChallenges.

- [ ] **Step 1: Write failing parser/entity-invariant tests**

```csharp
[Theory]
[InlineData("0912 345 678", "+84912345678")]
[InlineData("+84912345678", "+84912345678")]
public void Vietnamese_phone_is_normalized_to_e164(string raw, string expected)
{
    Assert.True(ContactIdentifier.TryParse(raw, out var contact));
    Assert.Equal(expected, contact.Value);
}

[Fact]
public void Challenge_is_unusable_after_five_failed_attempts()
{
    var challenge = RegistrationChallenge.Create(
        ContactIdentifier.Email("person@example.test"), "ABCDEF", "password-hash",
        "An", "Nguyen", new DateOnly(2000, 1, 2), Gender.PreferNotToSay,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
    for (var i = 0; i < 5; i++) challenge.RegisterFailedAttempt(DateTimeOffset.UtcNow);
    Assert.False(challenge.IsUsableAt(DateTimeOffset.UtcNow));
}
```

- [ ] **Step 2: Run and verify failure**

Run: dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Vietnamese_phone_is_normalized|FullyQualifiedName~Challenge_is_unusable"

Expected: FAIL because types do not exist.

- [ ] **Step 3: Implement model and migration**

Parse email with EmailAddressAttribute. Strip spaces/dots/hyphens/parentheses from phone, accept Vietnamese mobile prefixes 03/05/07/08/09, normalize 0/84 to +84. Store SHA-256 OTP hash (64 hex chars), Identity password hash, form payload, expiry, resend time, send window/count, failures, nullable consume time. Add unique normalized-contact index; generate the EF migration and inspect migration/snapshot for unrelated changes.

- [ ] **Step 4: Verify persistence**

Run: dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Vietnamese_phone_is_normalized|FullyQualifiedName~Challenge_is_unusable" && ./scripts/check-migrations.sh

Expected: PASS.

- [ ] **Step 5: Commit and push**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Identity backend/Fookbase.Src/Main/Code/Persistence backend/Fookbase.Src/Tests/Identity
git commit -m "feat: thêm trạng thái đăng ký chờ OTP"
git push origin HEAD
```

### Task 3: Registration profile data and usernames

**Files:**
- Create: backend/Fookbase.Src/Main/Code/Modules/Users/Entities/Gender.cs
- Modify: UserProfile.cs; UserProfileConfiguration.cs; UserProfileService.cs
- Create: Code/Persistence/Migrations/<timestamp>_AddUserProfileGender.cs
- Test: UserProfileEndpointsTests.cs

**Interfaces:** Produce Gender.Female/Male/Other/PreferNotToSay and EnsureCreatedAsync(Guid, username, displayName, dateOfBirth, gender, CancellationToken).

- [ ] **Step 1: Write failing provisioning test**

```csharp
[Fact]
public async Task Verified_registration_creates_profile_from_submitted_data()
{
    var session = await CompleteRegistrationAsync("Nguyễn", "An", new DateOnly(2000, 1, 2));
    var profile = await GetProfileFromDatabaseAsync(session.User.Id);
    Assert.Equal("Nguyễn An", profile.DisplayName);
    Assert.Equal(new DateOnly(2000, 1, 2), profile.DateOfBirth);
    Assert.Equal(Gender.PreferNotToSay, profile.Gender);
}
```

- [ ] **Step 2: Run and verify failure**

Run: dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Verified_registration_creates_profile_from_submitted_data"

Expected: FAIL because gender and creation overload are absent.

- [ ] **Step 3: Implement private profile initialization**

Add required gender with default PreferNotToSay and retain existing public response/update behavior. Build an Identity username generator that transliterates Vietnamese, limits result 3--32 ASCII chars, and tries base, base.2, etc. through UserManager.FindByNameAsync.

- [ ] **Step 4: Generate migration and run test**

Run: dotnet ef migrations add AddUserProfileGender --project backend/Fookbase.Src/Main/Fookbase.Api.csproj --startup-project backend/Fookbase.Src/Main/Fookbase.Api.csproj --output-dir Code/Persistence/Migrations && dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Verified_registration_creates_profile_from_submitted_data"

Expected: migration contains only UserProfiles.Gender; test PASS.

- [ ] **Step 5: Commit and push**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Users backend/Fookbase.Src/Main/Code/Persistence backend/Fookbase.Src/Tests/Users
git commit -m "feat: lưu thông tin hồ sơ khi đăng ký"
git push origin HEAD
```

### Task 4: Start, resend, verify, and provision registration

**Files:**
- Create: Identity/DTOs/Requests/RegistrationRequests.cs; Identity/DTOs/Responses/RegistrationResponses.cs; Identity/Services/RegistrationChallengeService.cs
- Modify: AuthenticationEndpoints.cs; DependencyInjection.cs
- Test: AuthenticationEndpointsTests.cs

**Interfaces:** Produce RegistrationStartRequest, RegistrationVerifyRequest, RegistrationResendRequest, RegistrationChallengeResponse, and RegistrationChallengeService.StartAsync/ResendAsync/VerifyAsync.

- [ ] **Step 1: Write failing end-to-end tests**

```csharp
[Fact]
public async Task Email_registration_creates_no_user_until_correct_otp_is_verified()
{
    var start = await client.PostAsJsonAsync("/api/auth/registration/start", ValidStart(UniqueEmail()));
    Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
    Assert.Null(await userManager.FindByEmailAsync(email));
    var code = fakeOtp.LastCodeFor(email);
    var verify = await client.PostAsJsonAsync("/api/auth/registration/verify", new { challengeId, code });
    Assert.Equal(HttpStatusCode.Created, verify.StatusCode);
    Assert.True((await userManager.FindByEmailAsync(email))!.EmailConfirmed);
}
```

Add a parallel phone test that checks normalized PhoneNumberConfirmed.

- [ ] **Step 2: Run and verify failure**

Run: dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Email_registration_creates_no_user|FullyQualifiedName~Phone_registration"

Expected: FAIL with missing routes.

- [ ] **Step 3: Implement service and routes**

Validate names 1--50 chars, calendar date, age >=13, gender and Identity password validators. Generate OTP with RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); hash with SHA-256. Start replaces challenge, saves then sends; delivery failure invalidates it. Verify atomically consumes challenge and transactionally creates user/profile/privacy/session. Map /registration/start, /registration/resend, /registration/verify anonymously with sensitive-auth limits; remove direct /register.

- [ ] **Step 4: Cover error paths and run suite**

Test invalid/underage data, duplicate contact, unavailable sender, resend cooldown/sixth send, fifth/sixth invalid code, expiry, and replay.

Run: dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthenticationEndpointsTests"

Expected: PASS.

- [ ] **Step 5: Commit and push**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Identity backend/Fookbase.Src/Tests/Identity
git commit -m "feat: xác minh OTP trước khi tạo tài khoản"
git push origin HEAD
```

### Task 5: Dual-contact login and recovery

**Files:**
- Modify: LoginRequest.cs; ForgotPasswordRequest.cs; ResetPasswordRequest.cs; AuthenticatedUserResponse.cs
- Modify: AuthenticationValidation.cs; AuthenticationService.cs; AuthenticationEndpoints.cs
- Test: AuthenticationEndpointsTests.cs

**Interfaces:** LoginRequest accepts identifier and backwards-compatible JSON email; authenticated user response exposes nullable email and phone; AuthenticationService.FindByIdentifierAsync is the one lookup path.

- [ ] **Step 1: Write failing phone/compatibility tests**

```csharp
[Fact]
public async Task Phone_only_user_can_login_with_a_spaced_local_number()
{
    await CompletePhoneRegistrationAsync("0912345678");
    var response = await client.PostAsJsonAsync("/api/auth/login",
        new { identifier = "0912 345 678", password = TestPassword });
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task Legacy_login_email_json_still_works()
{
    var response = await client.PostAsJsonAsync("/api/auth/login",
        new { email = existing.Email, password = existing.Password });
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

- [ ] **Step 2: Run and verify phone case fails**

Run: dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Phone_only_user_can_login|FullyQualifiedName~Legacy_login_email_json"

Expected: phone FAIL; legacy email PASS before code changes.

- [ ] **Step 3: Implement contact-aware authentication and reset**

Parse identifier once; query email with FindByEmailAsync or normalized phone through Identity users. Reuse password/lockout/moderation/2FA/token code unchanged. Preserve email reset links. Add a phone reset OTP purpose using the same security rules, and return no content for unknown contacts.

- [ ] **Step 4: Run auth regression suite**

Run: dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthenticationEndpointsTests"

Expected: PASS including email login/reset, 2FA, and phone cases.

- [ ] **Step 5: Commit and push**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Identity backend/Fookbase.Src/Tests/Identity
git commit -m "feat: đăng nhập bằng email hoặc số điện thoại"
git push origin HEAD
```

### Task 6: Web registration and confirmation UX

**Files:**
- Modify: frontend/web/src/api/auth.ts; frontend/web/src/auth/context.ts; frontend/web/src/auth/AuthProvider.tsx; frontend/web/src/pages/auth/LoginPage.tsx; frontend/web/src/preferences/PreferencesProvider.tsx

**Interfaces:** Add authApi.startRegistration, resendRegistration, verifyRegistration; only verification returns a session and calls AuthProvider.applySession.

- [ ] **Step 1: Add failing typed client usage**

```ts
const pending = await authApi.startRegistration({
  firstName: 'Nguyễn', lastName: 'An', dateOfBirth: '2000-01-02',
  gender: 'preferNotToSay', contact: '0912345678', password: 'Password123!',
})
expect(pending.expiresAtUtc).toBeTruthy()
```

- [ ] **Step 2: Run web checks and verify APIs are missing**

Run in frontend/web: npm run lint && npm run build

Expected: FAIL until new types are introduced.

- [ ] **Step 3: Implement form and confirmation state**

In register mode render first/last name, day/month/year selects, gender, contact, password, “Gửi mã”. On success show confirmation with masked contact, six-digit numeric input, resend countdown, and back-to-edit. Keep values after failure. Add Vietnamese/English translations through the existing preferences provider. Preserve login, Google, 2FA, reset branches and label login/recovery “Email hoặc số điện thoại”.

- [ ] **Step 4: Run lint/build**

Run in frontend/web: npm run lint && npm run build

Expected: PASS.

- [ ] **Step 5: Commit and push**

```bash
git add frontend/web/src
git commit -m "feat: thêm form đăng ký xác minh OTP"
git push origin HEAD
```

### Task 7: Docs and release verification

**Files:**
- Modify: .env.example; README.md; docs/production-deployment.md

- [ ] **Step 1: Document only safe configuration names**

Document Sms__Enabled, Sms__AccessToken, Sms__Sender, Sms__BaseUrl as empty placeholders. Explain SpeedSMS account setup, secret injection, test SMS, costs, OTP limits, and sms_unavailable when credentials are absent.

- [ ] **Step 2: Run final verification**

```bash
./scripts/check-migrations.sh
dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj
cd frontend/web && npm run lint && npm run build
git diff --check
```

Expected: every command exits 0.

- [ ] **Step 3: Commit and push**

```bash
git add .env.example README.md docs/production-deployment.md
git commit -m "docs: hướng dẫn cấu hình OTP đăng ký"
git push origin HEAD
```

## Plan self-review

- Spec coverage: Tasks 1--2 secure sending/persistence; Task 3 provisions profile data; Task 4 implements OTP registration; Task 5 supports dual login/recovery; Task 6 builds requested UI; Task 7 documents/verifies release.
- Placeholder scan: only EF-generated migration timestamps vary; no behavior is deferred.
- Type consistency: later tasks use ContactIdentifier, IContactOtpSender, RegistrationChallengeService, and endpoint names created earlier.
