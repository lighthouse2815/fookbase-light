# Google Sign-In Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users sign into Fookbase Web and Zola Light with a verified Google account while preserving Fookbase sessions, 2FA, and safe account linking.

**Architecture:** The API owns the Google Authorization Code callback using ASP.NET Core's Google authentication handler and stores the provider identity in the existing Identity `AspNetUserLogins` table. A short-lived, hashed completion code crosses from the API callback to the selected SPA; the SPA exchanges it for the existing Fookbase authentication response. Existing email accounts require their Fookbase password before the provider is linked, and any enabled 2FA challenge remains mandatory.

**Tech Stack:** .NET 10 minimal APIs, ASP.NET Core Identity, EF Core/Npgsql migrations, `Microsoft.AspNetCore.Authentication.Google`, React 19, TypeScript, Vite.

**Spec:** `docs/superpowers/specs/2026-09-16-google-sign-in-design.md`

## Global Constraints

- Google OAuth client ID and secret are server-only configuration; never add a credential to a tracked file or a `VITE_*` variable.
- Keep JWT bearer as the default API authentication scheme and retain the existing JWT, refresh rotation, session revocation, lockout, moderation, rate limiting, and 2FA contracts.
- Accept only Google `sub`, `email`, and `email_verified=true`; use `sub`, not email, as the durable provider key.
- Allow only the fixed `web` and `zola-light` client keys; never accept an arbitrary return URL.
- Return Fookbase tokens only from JSON API responses, never in a redirect URL or URL fragment.
- Google-only accounts have no initial password, have `EmailConfirmed=true`, and can set a password later through the existing password-reset flow.
- Do not start Google OAuth from Zalo/Messenger embedded WebViews; offer an external-browser instruction while preserving password login.
- Use the pre-existing `AspNetUserLogins` Identity table through `UserManager.AddLoginAsync`; do not add a duplicate provider-link table.
- Create a separate Git commit after each task and push it to `origin/main` only after its checks pass.

---

## File structure

| Path | Responsibility |
| --- | --- |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/Config/GoogleAuthenticationOptions.cs` | Validated server-only Google/client-origin configuration and fixed client-target lookup. |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/Entities/ExternalLoginCompletion.cs` | One-time hashed completion record and purpose/client invariants. |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/Data/Configurations/ExternalLoginCompletionConfiguration.cs` | Table/index/max-length mapping. |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/GoogleAuthenticationService.cs` | Callback claim validation, account provisioning/linking, completion consumption, and username generation. |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Requests/GoogleAuthenticationRequests.cs` | Exchange/link request records. |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Responses/ExternalAuthenticationResponses.cs` | Provider availability and callback response contracts. |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/Endpoints/AuthenticationEndpoints.cs` | Public provider status, OAuth start/callback, completion exchange, and password-confirm-link routes. |
| `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/GoogleExternalIdentityReader.cs` | Focused adapter that reads and clears the temporary external cookie after the Google handler callback. |
| `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/TestGoogleExternalIdentityReader.cs` | Test-only reader that returns controlled Google claims without a network call or forged cookie. |
| `frontend/web/src/pages/auth/LoginPage.tsx` | Google entry point, callback completion/link-password UX, 2FA handoff, WebView fallback. |
| `frontend/zola-light/src/App.tsx` and `frontend/zola-light/src/api.ts` | Equivalent Google entry point and callback exchange using Zola's existing session store. |
| `.env.example`, `README.md`, `docs/production-deployment.md` | Public configuration names, Cloud Console callback checklist, production deployment instructions. |

### Task 1: Register and validate the Google authentication infrastructure

**Files:**
- Modify: `backend/Fookbase.Src/Main/Fookbase.Api.csproj`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Config/GoogleAuthenticationOptions.cs`
- Modify: `backend/Fookbase.Src/Main/Code/ModuleServiceCollectionExtensions.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/DependencyInjection.cs`
- Modify: `backend/Fookbase.Src/Main/Program.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Shared/Config/ProductionConfigurationValidator.cs`
- Modify: `backend/Fookbase.Src/Main/appsettings.json`
- Modify: `backend/Fookbase.Src/Main/appsettings.Testing.json`
- Modify: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/IdentityApiFactory.cs`
- Test: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/ProductionRuntimeTests.cs`

**Interfaces:**
- Produces `GoogleAuthenticationOptions` with `SectionName = "GoogleAuthentication"`, `Enabled`, `ClientId`, `ClientSecret`, `WebBaseUrl`, `ZolaLightBaseUrl`, `GetClientLoginUri(string client)`, and `Validate(bool production)`.
- Produces the named `GoogleExternal` cookie scheme and the named `Google` remote scheme. The JWT bearer scheme remains default.

- [ ] **Step 1: Add a failing production-configuration test for enabled Google without a secret**

  Add a test to `ProductionRuntimeTests.cs` that starts the API with `GoogleAuthentication:Enabled=true`, a client ID, two HTTPS client URLs, but no secret. Assert startup throws an `InvalidOperationException` containing `GoogleAuthentication:ClientSecret`.

  ```csharp
  [Fact]
  public void Production_requires_secret_when_google_authentication_is_enabled()
  {
      var values = ProductionValues();
      values["GoogleAuthentication:Enabled"] = "true";
      values["GoogleAuthentication:ClientId"] = "client-id";
      values["GoogleAuthentication:WebBaseUrl"] = "https://app.example.test";
      values["GoogleAuthentication:ZolaLightBaseUrl"] = "https://zola.example.test";
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

      var exception = Assert.Throws<InvalidOperationException>(() =>
          ProductionConfigurationValidator.Validate(configuration, production: true));

      Assert.Contains("GoogleAuthentication:ClientSecret", exception.Message);
  }
  ```

- [ ] **Step 2: Run the focused test to verify it fails**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Production_requires_secret_when_google_authentication_is_enabled"
  ```

  Expected: FAIL because `ProductionConfigurationValidator` has no Google configuration validation.

- [ ] **Step 3: Add the minimal configuration type and wiring**

  Add `Microsoft.AspNetCore.Authentication.Google` at version `10.0.11`. Implement a sealed options type that accepts only client keys `web` and `zola-light`, normalizes base URLs without a trailing slash, and returns `"{baseUrl}/login"`. `Validate` must require ID, secret, absolute host URLs, and HTTPS client URLs in production only when `Enabled` is true.

  Register it in `AddIdentityModule`, expose it as a singleton from `AddIdentityInfrastructure`, and extend `ProductionConfigurationValidator`. In `Program.cs`, append the external cookie and Google handler to the existing authentication builder without changing its default scheme:

  ```csharp
  .AddCookie("GoogleExternal", options =>
  {
      options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
      options.Cookie.HttpOnly = true;
      options.Cookie.SameSite = SameSiteMode.Lax;
      options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
  })
  .AddGoogle("Google", options =>
  {
      options.SignInScheme = "GoogleExternal";
      options.ClientId = googleOptions.ClientId;
      options.ClientSecret = googleOptions.ClientSecret;
      options.CallbackPath = "/signin-google";
  });
  ```

  Register the Google handler only when enabled. Add disabled-by-default values to `appsettings.json`; inject enabled test values in `IdentityApiFactory` so later endpoint tests can exercise the routes.

- [ ] **Step 4: Run configuration and compile checks**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~ProductionRuntimeTests"
  dotnet build backend/Fookbase.Src/Main/Fookbase.Api.csproj
  ```

  Expected: all production runtime tests pass and the API compiles with Google handler registration.

- [ ] **Step 5: Commit the infrastructure slice**

  ```bash
  git add backend/Fookbase.Src/Main/Fookbase.Api.csproj \
    backend/Fookbase.Src/Main/Code/Modules/Identity/Config/GoogleAuthenticationOptions.cs \
    backend/Fookbase.Src/Main/Code/ModuleServiceCollectionExtensions.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/DependencyInjection.cs \
    backend/Fookbase.Src/Main/Program.cs \
    backend/Fookbase.Src/Main/Code/Shared/Config/ProductionConfigurationValidator.cs \
    backend/Fookbase.Src/Main/appsettings.json \
    backend/Fookbase.Src/Main/appsettings.Testing.json \
    backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/IdentityApiFactory.cs \
    backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/ProductionRuntimeTests.cs
  git commit -m "feat: cấu hình xác thực Google"
  git push origin main
  ```

### Task 2: Persist replay-safe external-login completions and pending 2FA links

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Entities/ExternalLoginCompletion.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Data/Configurations/ExternalLoginCompletionConfiguration.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Persistence/FookbaseDbContext.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/Entities/TwoFactorLoginChallenge.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/Data/Configurations/TwoFactorLoginChallengeConfiguration.cs`
- Create: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/<timestamp>_AddGoogleAuthentication.cs`
- Create: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/<timestamp>_AddGoogleAuthentication.Designer.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/FookbaseDbContextModelSnapshot.cs`
- Test: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs`

**Interfaces:**
- Produces `ExternalLoginCompletionPurpose` (`IssueSession`, `LinkExisting`) and `ExternalLoginCompletion.Create(string codeHash, ExternalLoginCompletionPurpose purpose, string client, string provider, string providerKey, string email, Guid? userId, DateTimeOffset now)` plus `TryConsumeAt(now)`; it stores a SHA-256 hash only, expires after five minutes, and can be consumed once.
- Extends `TwoFactorLoginChallenge.Create` with optional pending provider/provider key fields; `VerifyTwoFactorAsync` later adds that login only after a valid TOTP/recovery code.

- [ ] **Step 1: Add a failing persistence test for completion hash and consumption**

  In `AuthenticationEndpointsTests.cs`, create an entity with a SHA-256 digest, persist it through `FookbaseDbContext`, retrieve it, and assert it stores exactly a 64-character hex digest rather than a raw completion value. Consume it once and assert a second consume returns `false`.

  ```csharp
  var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("raw-completion")));
  var completion = ExternalLoginCompletion.Create(codeHash,
      ExternalLoginCompletionPurpose.IssueSession, "web", "Google", "google-sub",
      "person@example.test", Guid.NewGuid(), now);
  Assert.Equal(64, completion.CodeHash.Length);
  Assert.NotEqual("raw-completion", completion.CodeHash);
  Assert.True(completion.TryConsumeAt(now));
  Assert.False(completion.TryConsumeAt(now.AddSeconds(1)));
  ```

- [ ] **Step 2: Run the focused test to verify it fails**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~External_login_completion"
  ```

  Expected: FAIL because neither the entity nor the test service seam exists.

- [ ] **Step 3: Implement the entity, mapping, and migration**

  Define a `Google` provider constant, fixed client values, `Purpose`, `CodeHash` (64 chars), `ProviderKey` (256 chars), `Email` (256 chars), nullable `UserId`, `CreatedAtUtc`, `ExpiresAtUtc`, and nullable `ConsumedAtUtc`. Add an index on `(CodeHash, ExpiresAtUtc)` and a foreign key to `AspNetUsers` for `UserId`.

  The entity must refuse invalid client/provider/key/email/hash values before persistence and make expiry explicit:

  ```csharp
  public bool IsUsableAt(DateTimeOffset now) => ConsumedAtUtc is null && ExpiresAtUtc > now;

  public bool TryConsumeAt(DateTimeOffset now)
  {
      if (!IsUsableAt(now)) return false;
      ConsumedAtUtc = now;
      return true;
  }
  ```

  Extend `TwoFactorLoginChallenge` with nullable `PendingExternalProvider` and `PendingExternalProviderKey` (both max 256); index active pending challenges by user and expiry. Generate the migration using the existing main project and DbContext, then run `bash scripts/check-migrations.sh` to verify the snapshot is synchronized.

- [ ] **Step 4: Run the focused test and migration checks**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~External_login_completion"
  bash scripts/check-migrations.sh
  ```

  Expected: completion storage is hashed and single-use; migration check succeeds.

- [ ] **Step 5: Commit the persistence slice**

  ```bash
  git add backend/Fookbase.Src/Main/Code/Modules/Identity/Entities/ExternalLoginCompletion.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/Data/Configurations/ExternalLoginCompletionConfiguration.cs \
    backend/Fookbase.Src/Main/Code/Persistence/FookbaseDbContext.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/Entities/TwoFactorLoginChallenge.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/Data/Configurations/TwoFactorLoginChallengeConfiguration.cs \
    backend/Fookbase.Src/Main/Code/Persistence/Migrations \
    backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs
  git commit -m "feat: lưu mã hoàn tất đăng nhập ngoài"
  git push origin main
  ```

### Task 3: Implement Google identity resolution, provisioning, linking, and 2FA completion

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/GoogleAuthenticationService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/AuthenticationService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/DependencyInjection.cs`
- Test: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs`

**Interfaces:**
- Produces `GoogleAuthenticationService.CreateCompletionAsync(string client, string providerKey, string email, bool emailVerified, CancellationToken)` returning `ApplicationResult<GoogleCompletionRedirect>` where `GoogleCompletionRedirect(string Code, bool RequiresPassword, string? Email)` contains a raw completion code only for the redirect response.
- Produces `GoogleAuthenticationService.ExchangeAsync(string code, string client, string? userAgent, CancellationToken)` and `LinkExistingAsync(string code, string client, string password, string? userAgent, CancellationToken)`, each returning `ApplicationResult<object>` containing either `AuthenticationResponse` or `TwoFactorChallengeResponse`.
- Extends `AuthenticationService.VerifyTwoFactorAsync` to add `UserLoginInfo("Google", pendingProviderKey, "Google")` after code validation and before issuing the JWT session.

- [ ] **Step 1: Write failing end-to-end service tests for the three account cases**

  Add tests that drive the future API contract/service seam with deterministic Google claims:

  ```csharp
  [Fact] public async Task Google_new_verified_email_creates_confirmed_user_profile_privacy_and_login();
  [Fact] public async Task Google_existing_email_requires_correct_password_before_linking();
  [Fact] public async Task Google_linked_user_returns_the_standard_token_pair();
  [Fact] public async Task Google_unverified_email_is_rejected();
  [Fact] public async Task Google_link_with_two_factor_adds_provider_only_after_two_factor_verification();
  ```

  The new-user assertion must check `EmailConfirmed`, a `UserProfile`, `UserPrivacySettings`, and `UserManager.GetLoginsAsync(user)` contains provider `Google` and the supplied `sub`.

- [ ] **Step 2: Run the focused tests to verify they fail**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Google_"
  ```

  Expected: FAIL because the Google completion service and contract do not exist.

- [ ] **Step 3: Implement minimal domain behavior**

  `CreateCompletionAsync` must reject blank provider key/email or `emailVerified=false`; find a linked user with `UserManager.FindByLoginAsync("Google", providerKey)` first. For an unlinked normalized email, make a `LinkExisting` completion requiring password. For no user, atomically create a passwordless `User`, set `EmailConfirmed=true`, generate a unique 3–32-character username from the sanitized email local-part (lowercase letters, digits, dot, underscore, hyphen; append `-2`, `-3`, and so on), call `UserProfileService.EnsureCreatedAsync`, `UserPrivacySettingsService.EnsureCreatedAsync`, and `UserManager.AddLoginAsync` within a database transaction.

  Generate and persist only a hash of each completion code:

  ```csharp
  var rawCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
  var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));
  dbContext.ExternalLoginCompletions.Add(ExternalLoginCompletion.Create(
      codeHash, purpose, client, "Google", providerKey, email, userId, now));
  await dbContext.SaveChangesAsync(cancellationToken);
  return ApplicationResult<GoogleCompletionRedirect>.Success(
      new GoogleCompletionRedirect(rawCode, purpose == ExternalLoginCompletionPurpose.LinkExisting,
          purpose == ExternalLoginCompletionPurpose.LinkExisting ? email : null));
  ```

  Exchange only an `IssueSession` completion created for the requested client. Locate it by hash and atomically consume it with an `ExecuteUpdateAsync` predicate requiring `ConsumedAtUtc == null` and `ExpiresAtUtc > now`; a zero row count is `invalid_google_completion`. Apply the same active/lockout/moderation checks as password login. Reuse `AuthenticationService` for standard session issuance and 2FA challenge creation rather than duplicating JWT/refresh code.

  `LinkExistingAsync` must consume only the link-required completion after `CheckPasswordAsync` succeeds for the completion's normalized email user. If the user enables 2FA, retain the pending provider key in `TwoFactorLoginChallenge` and defer `AddLoginAsync` until `VerifyTwoFactorAsync` succeeds. If not, add the login then use the regular session issuer. Provider-key conflict, account unavailable, expired/replayed code, wrong client, and wrong password return stable application errors without a raw credential.

- [ ] **Step 4: Run the service tests and existing authentication regression**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Google_|FullyQualifiedName~Two_factor|FullyQualifiedName~Login_"
  ```

  Expected: all Google case tests plus login/2FA regression tests pass.

- [ ] **Step 5: Commit the domain slice**

  ```bash
  git add backend/Fookbase.Src/Main/Code/Modules/Identity/Services/GoogleAuthenticationService.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/Services/AuthenticationService.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/DependencyInjection.cs \
    backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs
  git commit -m "feat: xử lý đăng nhập Google"
  git push origin main
  ```

### Task 4: Expose and test the OAuth HTTP contract

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Requests/GoogleAuthenticationRequests.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Responses/ExternalAuthenticationResponses.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/GoogleExternalIdentityReader.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/Endpoints/AuthenticationEndpoints.cs`
- Create: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/TestGoogleExternalIdentityReader.cs`
- Modify: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/IdentityApiFactory.cs`
- Test: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs`

**Interfaces:**
- Produces `GET /api/auth/providers` → `{ google: boolean }`.
- Produces `GET /api/auth/google/start?client=web|zola-light` → OAuth challenge redirect; applies `auth-login` rate limit.
- Produces `POST /api/auth/google/exchange` with `GoogleCompletionRequest(string Code)` → existing `AuthenticationResponse` or `TwoFactorChallengeResponse`.
- Produces `POST /api/auth/google/link` with `GoogleLinkRequest(string Code, string Password)` → existing `AuthenticationResponse` or `TwoFactorChallengeResponse`.
- Produces a callback redirect shaped as `<fixed-login-uri>?provider=google&code=<one-time-code>` for an existing Google link/new account, or `...?provider=google&mode=link&email=<escaped-verified-email>&code=<one-time-code>` when the user must enter a matching Fookbase password.
- Produces `IGoogleExternalIdentityReader.ReadAsync(HttpContext, CancellationToken)` → `ApplicationResult<GoogleExternalIdentity>`; production reads/signs out the `GoogleExternal` cookie, while the integration factory replaces this one adapter.

- [ ] **Step 1: Add failing HTTP tests**

  Register `TestGoogleExternalIdentityReader` in `IdentityApiFactory`; it returns controlled claims only from headers `X-Test-Google-Sub`, `X-Test-Google-Email`, and `X-Test-Google-Email-Verified`. This tests callback business behavior without a Google network call or a forged encrypted temporary cookie. Add tests for:

  ```csharp
  [Fact] public async Task Google_start_rejects_unknown_client();
  [Fact] public async Task Google_providers_reports_enabled_state();
  [Fact] public async Task Google_exchange_rejects_replayed_completion_code();
  [Fact] public async Task Google_link_rejects_wrong_password();
  [Fact] public async Task Google_callback_redirects_only_to_fixed_client_login_route();
  ```

  Assert callback redirect locations begin with either configured `/login` URL and never contain `accessToken` or `refreshToken`.

- [ ] **Step 2: Run the focused HTTP tests to verify they fail**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Google_start|FullyQualifiedName~Google_providers|FullyQualifiedName~Google_exchange|FullyQualifiedName~Google_link|FullyQualifiedName~Google_callback"
  ```

  Expected: FAIL because public provider, start, callback, exchange, and link endpoints are absent.

- [ ] **Step 3: Map the fixed client-key endpoints**

  Add anonymous routes to `MapAuthenticationEndpoints`. `start` validates `GoogleAuthenticationOptions.Enabled` and the exact client key, stores the client key in protected authentication properties, then calls `Results.Challenge` using the named Google scheme. The handler callback redirects internally to `/api/auth/google/callback`; that endpoint asks `IGoogleExternalIdentityReader` to authenticate and clear only the named temporary external cookie, then passes its Google claims to `GoogleAuthenticationService` and redirects to the fixed login URI. Include `mode=link` and the escaped verified email only for the password-confirmation branch; never include Fookbase access/refresh tokens.

  ```csharp
  var target = googleOptions.GetClientLoginUri(client);
  var query = new Dictionary<string, string?>
  {
      ["provider"] = "google",
      ["code"] = completion.Code,
      ["mode"] = completion.RequiresPassword ? "link" : null,
      ["email"] = completion.RequiresPassword ? completion.Email : null,
  };
  return Results.Redirect(QueryHelpers.AddQueryString(target, query));
  ```

  `providers` does not expose ID/secret/URLs. `exchange` and `link` use `auth-login` rate limiting, obtain user agent from the request headers, return existing response JSON shapes, and map every `ApplicationError` through current `ToHttpResult` behavior.

- [ ] **Step 4: Run focused and full Identity integration tests**

  Run:

  ```bash
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --filter "FullyQualifiedName~AuthenticationEndpointsTests"
  ```

  Expected: provider status, route validation, callback redirect, one-time exchange, password link, existing auth, refresh, logout, email, and 2FA tests all pass.

- [ ] **Step 5: Commit the HTTP contract slice**

  ```bash
  git add backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Requests/GoogleAuthenticationRequests.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Responses/ExternalAuthenticationResponses.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/Services/GoogleExternalIdentityReader.cs \
    backend/Fookbase.Src/Main/Code/Modules/Identity/Endpoints/AuthenticationEndpoints.cs \
    backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/TestGoogleExternalIdentityReader.cs \
    backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/IdentityApiFactory.cs \
    backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs
  git commit -m "feat: thêm API đăng nhập Google"
  git push origin main
  ```

### Task 5: Integrate the Fookbase Web login page

**Files:**
- Modify: `frontend/web/src/api/auth.ts`
- Modify: `frontend/web/src/auth/AuthProvider.tsx`
- Modify: `frontend/web/src/auth/context.ts`
- Modify: `frontend/web/src/pages/auth/LoginPage.tsx`
- Modify: `frontend/web/src/preferences/PreferencesProvider.tsx`

**Interfaces:**
- Consumes `GET /api/auth/providers`, `POST /api/auth/google/exchange`, and `POST /api/auth/google/link`.
- Produces `beginGoogleSignIn('web')`, `completeGoogleSignIn(code)`, and `linkGoogleSignIn(code, password)` typed with the current `LoginResponse` union.

- [ ] **Step 1: Add failing TypeScript call sites and Vietnamese/English copy keys**

  Add the API types below and reference them from `LoginPage.tsx`; then run TypeScript build before implementing the API functions.

  ```ts
  export interface ExternalProviders { google: boolean }
  export interface GoogleCompletionRequest { code: string }
  export interface GoogleLinkRequest { code: string; password: string }
  ```

  Add required copy keys for “Continue with Google”, completion failure, password confirmation for the shown email, and “Open in Chrome or Safari to sign in with Google”.

- [ ] **Step 2: Run the Web type build to verify it fails**

  Run:

  ```bash
  cd frontend/web && npm run build
  ```

  Expected: FAIL because the referenced auth functions and preference keys do not exist.

- [ ] **Step 3: Implement the minimal Web flow**

  Add provider availability fetch on the normal login state. Start OAuth with a top-level `window.location.assign(`${apiBaseUrl}/api/auth/google/start?client=web`)`. On returning to `/login?provider=google&code=...`, remove the code from browser history immediately, call the exchange endpoint once, then either apply the session or reuse `completeTwoFactor` UI when the union is a 2FA challenge.

  For `mode=link`, show the decoded `email` query value as read-only, collect the Fookbase password, and call the typed link endpoint; handle its session/2FA union identically. Keep the existing password, registration, reset, and verification branches unchanged. Detect `Zalo`, `FBAN`, `FBAV`, and `Messenger` user-agent markers only around the Google button; show the external-browser guidance instead of invoking OAuth.

  ```ts
  const isEmbeddedBrowser = /\b(Zalo|FBAN|FBAV|Messenger)\b/i.test(navigator.userAgent)
  const completionCode = searchParams.get('provider') === 'google'
    ? searchParams.get('code')
    : null
  ```

- [ ] **Step 4: Run Web lint and production build**

  Run:

  ```bash
  cd frontend/web && npm run lint && npm run build
  ```

  Expected: lint is clean and Vite production build succeeds without adding a browser credential or Google token.

- [ ] **Step 5: Commit the Web client slice**

  ```bash
  git add frontend/web/src/api/auth.ts frontend/web/src/auth/AuthProvider.tsx \
    frontend/web/src/auth/context.ts frontend/web/src/pages/auth/LoginPage.tsx \
    frontend/web/src/preferences/PreferencesProvider.tsx
  git commit -m "feat: đăng nhập Google trên Fookbase"
  git push origin main
  ```

### Task 6: Integrate the Zola Light login page

**Files:**
- Modify: `frontend/zola-light/src/api.ts`
- Modify: `frontend/zola-light/src/App.tsx`
- Modify: `frontend/zola-light/src/styles.css`

**Interfaces:**
- Consumes the same `ExternalProviders`, completion and link endpoints as Task 5.
- Produces `authApi.providers()`, `authApi.completeGoogle(code)`, and `authApi.linkGoogle(code, password)` while preserving `AuthSession` local-storage persistence.

- [ ] **Step 1: Add failing typed calls in Zola Login**

  Extend `authApi` and use a `provider=google`/`code` query-state branch in `Login`. Add a typed `GoogleLinkRequired` state containing the completion code and displayed email, then run the build before those methods exist.

- [ ] **Step 2: Run the Zola build to verify it fails**

  Run:

  ```bash
  cd frontend/zola-light && npm run build
  ```

  Expected: FAIL because the new `authApi` methods and Google login state are incomplete.

- [ ] **Step 3: Implement the minimal Zola flow**

  Fetch public provider availability at login mount. Render a Google button only when enabled and the browser is not embedded; direct it to `apiBaseUrl + '/api/auth/google/start?client=zola-light'`. When callback query state exists, replace the history URL, exchange its completion code, persist an `AuthSession` using `saveSession`, and call `onSession`; show the existing 2FA input when appropriate.

  In `mode=link` state, render `email` from the callback query as read-only with one password input and submit the typed link request. Add scoped CSS using current `.login-*` conventions for the Google button, separator, and WebView explanatory notice. Preserve Zola's return-to-Fookbase link and existing password/2FA behavior.

  ```ts
  const callback = new URLSearchParams(window.location.search)
  const googleCode = callback.get('provider') === 'google' ? callback.get('code') : null
  const needsGoogleLinkPassword = callback.get('mode') === 'link'
  if (googleCode) window.history.replaceState({}, document.title, window.location.pathname)
  ```

- [ ] **Step 4: Run Zola lint and production build**

  Run:

  ```bash
  cd frontend/zola-light && npm run lint && npm run build
  ```

  Expected: lint is clean and production build succeeds.

- [ ] **Step 5: Commit the Zola client slice**

  ```bash
  git add frontend/zola-light/src/api.ts frontend/zola-light/src/App.tsx frontend/zola-light/src/styles.css
  git commit -m "feat: đăng nhập Google trên Zola Light"
  git push origin main
  ```

### Task 7: Document deployment and run the end-to-end verification suite

**Files:**
- Modify: `.env.example`
- Modify: `README.md`
- Modify: `docs/production-deployment.md`
- Modify: `docs/superpowers/plans/2026-09-16-google-sign-in.md`

**Interfaces:**
- Documents `GoogleAuthentication__Enabled`, `GoogleAuthentication__ClientId`, `GoogleAuthentication__ClientSecret`, `GoogleAuthentication__WebBaseUrl`, and `GoogleAuthentication__ZolaLightBaseUrl` without values.
- Documents fixed API callback `https://<api-host>/signin-google` and both SPA login destinations.

- [ ] **Step 1: Add configuration documentation and checklist**

  Update `.env.example` with blank Google variables and a comment that client secret is server-only. Add an Authentication table/API section to README for provider status/start/exchange/link routes. In production deployment docs, specify Google Cloud OAuth consent screen, owned HTTPS domain, exact API callback registration, public homepage/terms/privacy requirements, secret injection, and the in-app browser limitation.

- [ ] **Step 2: Add manual acceptance cases to the plan**

  Mark the completed implementation steps in this plan and add an acceptance checklist requiring:

  ```text
  1. A new Google account creates one confirmed Fookbase account and logs into both Web and Zola independently.
  2. An existing password account with the same verified Google email rejects a wrong password, then links only after the correct password and 2FA when enabled.
  3. Reopening or replaying a callback URL cannot create another session.
  4. A Zalo/Messenger in-app browser shows the external-browser instruction; a normal Chrome/Safari browser completes Google login.
  5. No redirect URL, browser local storage, browser source map, git diff, or server log contains a Google secret or a Fookbase access/refresh token.
  ```

- [ ] **Step 3: Run all automated verification**

  Run:

  ```bash
  bash scripts/check-migrations.sh
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj
  (cd frontend/web && npm run lint && npm run build)
  (cd frontend/zola-light && npm run lint && npm run build)
  git diff --check
  rg -n "GoogleAuthentication__(ClientSecret|ClientId)=.+[^[:space:]]" .env.example README.md docs backend frontend
  ```

  Expected: migration/model check, Identity integration suite, both frontend lint/build checks, and whitespace check pass. The final `rg` output has no credential values; it may list only prose or blank variable declarations.

- [ ] **Step 4: Commit the documentation and verification record**

  ```bash
  git add .env.example README.md docs/production-deployment.md docs/superpowers/plans/2026-09-16-google-sign-in.md
  git commit -m "docs: hướng dẫn triển khai đăng nhập Google"
  git push origin main
  ```

## Plan self-review

| Spec requirement | Implementing task |
| --- | --- |
| Server-only Google OAuth, fixed clients, callback protection | Task 1 and Task 4 |
| Existing Identity provider links and single-use completion persistence | Task 2 and Task 3 |
| New verified-email provisioning, profile/privacy creation, username collision behavior | Task 3 |
| Existing-email password confirmation and preserved 2FA | Task 3 and Task 4 |
| Fookbase and Zola callbacks/session handoff | Task 5 and Task 6 |
| WebView guidance, production configuration, documentation | Task 5, Task 6, Task 7 |
| Backend tests, migration checks, frontend lint/build, acceptance validation | Tasks 1–7 |

No plan step relies on arbitrary return URLs, raw OAuth credentials, token-in-URL behavior, a duplicate provider-link table, or a disabled security control.
