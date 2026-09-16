# Firebase Phone Auth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send and verify Vietnamese phone OTP through Firebase Authentication, while retaining the existing backend email OTP registration and email-password-reset flows.

**Architecture:** The web client uses Firebase Web Phone Auth and invisible reCAPTCHA to send and confirm an SMS. It sends the resulting Firebase ID token to the API. The API verifies that token with Firebase Admin, checks its `phone_number` against the normalized pending registration/account contact, then provisions the user or resets the password. Existing email flows continue using the local one-time-code records.

**Tech Stack:** React 19 + Vite + TypeScript, Firebase Web SDK, ASP.NET Core/.NET 10, Firebase Admin SDK, ASP.NET Core Identity, PostgreSQL, xUnit integration tests.

**Spec:** `docs/superpowers/specs/2026-09-16-firebase-phone-auth-design.md`

## Global Constraints

- Keep Firebase web configuration in Vite environment variables only; it is public configuration, but values still must not be hard-coded into source.
- Keep the Firebase Admin service-account JSON only in deployment secrets; it must never be committed or printed in logs.
- The existing `RegistrationChallenge` and `PasswordResetChallenge` schema must not be changed solely for Firebase. Phone challenges use the existing lifetime/one-time-use transaction guard, while Firebase owns SMS resend/throttling.
- Email registration and email password reset must remain behaviorally compatible.
- Firebase token verification must require both a valid Firebase signature/audience and an exact normalized E.164 phone-number match.

---

## Task 1: Add Firebase server configuration and token verifier

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Config/FirebaseAuthenticationOptions.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/IFirebasePhoneTokenVerifier.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/FirebasePhoneTokenVerifier.cs`
- Modify: `backend/Fookbase.Src/Main/Fookbase.Api.csproj`
- Modify: `backend/Fookbase.Src/Main/Code/ModuleServiceCollectionExtensions.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/DependencyInjection.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Shared/Config/ProductionConfigurationValidator.cs`
- Modify: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/IdentityApiFactory.cs`
- Create: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/TestFirebasePhoneTokenVerifier.cs`

- [ ] **Step 1: Write the failing test double and production-wiring test.**
  - Add `TestFirebasePhoneTokenVerifier`, exposing a deterministic Firebase identity for a supplied test token and rejecting all other tokens.
  - Replace the production `IFirebasePhoneTokenVerifier` registration in `IdentityApiFactory` with that test double.
  - Add an integration assertion that a test server starts with `FirebaseAuthentication:Enabled=true`, its project id, and a test verifier, without requiring a real service-account JSON.
  - Run: `dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --no-restore --filter Firebase`
  - Expected: FAIL because the interface/configuration do not exist.

- [ ] **Step 2: Add configuration and the verifier contract.**
  - Define `FirebaseAuthenticationOptions` with `Enabled`, `ProjectId`, and `ServiceAccountJson`; when enabled, `Validate(production)` requires `ProjectId` and in production requires non-empty service-account JSON.
  - Define `FirebasePhoneIdentity(string Uid, string PhoneNumber)` and `IFirebasePhoneTokenVerifier.VerifyAsync(string? idToken, string expectedPhoneNumber, CancellationToken)` returning `ApplicationResult<FirebasePhoneIdentity>`.
  - Add the maintained `FirebaseAdmin` package to `Fookbase.Api.csproj` and restore the solution.

- [ ] **Step 3: Implement secure Admin-token verification.**
  - Initialise a named `FirebaseApp` from `FirebaseAuthenticationOptions.ServiceAccountJson` only when Firebase is enabled; obtain `FirebaseAuth` from that app.
  - Use `VerifyIdTokenAsync` and reject an absent/invalid token, a token without `phone_number`, and a phone number that differs from `expectedPhoneNumber` using ordinal comparison.
  - Return an `invalid_firebase_phone_token` unauthorized application error without exposing provider exception contents.
  - Register options and `IFirebasePhoneTokenVerifier` in Identity DI, and validate production configuration through `ProductionConfigurationValidator`.
  - Run: `dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --no-restore --filter Firebase`
  - Expected: PASS.

- [ ] **Step 4: Commit the isolated backend wiring.**
  - Run: `dotnet build backend/Fookbase.Src/Main/Fookbase.Api.csproj --no-restore`.
  - Commit: `feat: thêm xác thực token Firebase cho số điện thoại`.

## Task 2: Use Firebase token verification for phone registration

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Requests/RegistrationRequests.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Responses/RegistrationResponses.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/RegistrationChallengeService.cs`
- Modify: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs`

- [ ] **Step 1: Write failing phone-registration integration tests.**
  - Change the phone registration test to verify with `{ challengeId, firebaseIdToken }`, using the test verifier token tied to the normalized phone number.
  - Assert phone `start` returns `verificationMethod: "firebasePhone"` and that `TestContactOtpSender` contains no code for that phone.
  - Add a negative test: a valid test token for another number returns unauthorized and creates no user.
  - Preserve the email registration test and assert it returns `verificationMethod: "emailOtp"`.
  - Run: `dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Phone_registration|FullyQualifiedName~Email_registration"`
  - Expected: FAIL because registration only accepts locally generated six-digit codes.

- [ ] **Step 2: Extend the registration API contract without breaking email clients.**
  - Add optional `FirebaseIdToken` to `RegistrationVerifyRequest`, retaining optional `Code` for email clients.
  - Add `VerificationMethod` to `RegistrationChallengeResponse` with the serialized values `emailOtp` and `firebasePhone`.
  - Return `emailOtp` for email and `firebasePhone` for a normalized phone contact.

- [ ] **Step 3: Branch registration challenge delivery and verification by contact kind.**
  - On phone `StartAsync`, persist/restart the existing challenge with a cryptographically random server-only hash but do not call `IContactOtpSender`; Firebase’s browser flow sends the SMS.
  - On email, preserve current code generation, send, resend, expiry, five-attempt lockout, and delivery failure behavior.
  - On phone `VerifyAsync`, require `FirebaseIdToken`, call `IFirebasePhoneTokenVerifier` with the challenge contact, then consume the challenge atomically before creating the Identity user/profile/privacy settings/session.
  - On email, preserve the hash comparison and five-attempt behavior. Reject server-side `registration/resend` for a phone challenge with a stable validation error directing the client to Firebase rather than sending SpeedSMS.

- [ ] **Step 4: Run targeted tests and commit.**
  - Run: `dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Registration|FullyQualifiedName~Phone_only_user"`.
  - Commit: `feat: xác minh đăng ký số điện thoại qua Firebase`.

## Task 3: Use Firebase token verification for phone password reset

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/DTOs/Requests/ResetPasswordRequest.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Identity/Services/AuthenticationService.cs`
- Modify: `backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/AuthenticationEndpointsTests.cs`

- [ ] **Step 1: Write failing phone-reset integration tests.**
  - Update the existing phone reset test to use `{ identifier, firebaseIdToken, password, confirmPassword }` and the test verifier token for that user’s E.164 number.
  - Confirm old password and the prior refresh token are invalid after reset, and that an incorrect-number Firebase token cannot reset the account.
  - Confirm email reset requests still generate their existing reset-email behavior.
  - Run: `dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Phone_password_reset|FullyQualifiedName~Password"`
  - Expected: FAIL because reset expects local SMS code storage.

- [ ] **Step 2: Replace only the phone-reset proof.**
  - Add optional `FirebaseIdToken` to `ResetPasswordRequest`, retaining email `Token` fields unchanged.
  - Make a phone forgot-password request return the existing non-enumerating success response without sending an SMS; the browser sends the Firebase SMS after detecting a phone number.
  - In `ResetPhonePasswordAsync`, validate matching passwords, locate the active phone user, verify the Firebase ID token against the normalized account phone, generate an Identity reset token, reset the password, and revoke all refresh tokens.
  - Do not use or mutate `PasswordResetChallenge` for Firebase phone flow; leave its persisted schema untouched for safe rollout compatibility.

- [ ] **Step 3: Run targeted tests and commit.**
  - Run: `dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Phone_password_reset|FullyQualifiedName~Email"`.
  - Commit: `feat: đặt lại mật khẩu số điện thoại qua Firebase`.

## Task 4: Add Firebase Phone Auth to the web registration and reset forms

**Files:**
- Modify: `frontend/web/package.json`
- Modify: `frontend/web/package-lock.json`
- Create: `frontend/web/src/firebase/phoneAuth.ts`
- Modify: `frontend/web/src/api/auth.ts`
- Modify: `frontend/web/src/auth/AuthProvider.tsx`
- Modify: `frontend/web/src/pages/auth/LoginPage.tsx`
- Create: `frontend/web/.env.example` (or extend it if present)

- [ ] **Step 1: Add failing component-level coverage or a reproducible frontend check.**
  - If the project has no frontend test runner, document the manual acceptance sequence in the implementation PR notes and use `npm run lint` and `npm run build` as the required checks.
  - Update TypeScript types first so a phone registration response requires a Firebase token while an email response continues using a six-digit code; `npm run build` must initially fail until the UI call sites are updated.
  - Run: `npm run build` from `frontend/web`.
  - Expected: FAIL due to obsolete `completeRegistration(challengeId, code)` and phone reset payload types.

- [ ] **Step 2: Add a small Firebase Web SDK adapter.**
  - Add the official `firebase` dependency.
  - In `phoneAuth.ts`, read `VITE_FIREBASE_API_KEY`, `VITE_FIREBASE_AUTH_DOMAIN`, `VITE_FIREBASE_PROJECT_ID`, `VITE_FIREBASE_APP_ID`, and optional `VITE_FIREBASE_MESSAGING_SENDER_ID`; reject clearly when required public configuration is missing.
  - Initialise a singleton Firebase app/auth, create an invisible `RecaptchaVerifier` bound to a dedicated DOM element, call `signInWithPhoneNumber`, and expose operations to confirm a six-digit SMS code and obtain a fresh Firebase ID token.
  - Clear/reset the verifier and confirmation state on cancellation, failed confirmation, resend, and component unmount to avoid stale reCAPTCHA instances.

- [ ] **Step 3: Update API and auth context types.**
  - Model `RegistrationChallenge` with `verificationMethod: 'emailOtp' | 'firebasePhone'`.
  - Change `verifyRegistration` and `completeRegistration` to accept a discriminated email-code or Firebase-token proof; serialise `code` only for email and `firebaseIdToken` only for phone.
  - Change phone password reset details to use `firebaseIdToken` instead of `code`; preserve email reset types and endpoints.

- [ ] **Step 4: Implement the UI paths.**
  - When registration start responds `firebasePhone`, start Firebase SMS delivery, show the existing OTP input, and on submit exchange that OTP for a Firebase ID token before calling API registration verification.
  - Keep email registration UI behavior and backend resend button unchanged. For phone, wire “Gửi lại mã” to a fresh Firebase SMS attempt and show any Firebase quota/reCAPTCHA error as a normal form error.
  - In phone password recovery, initiate Firebase SMS instead of calling `/password/forgot`; confirm the OTP to an ID token and call `/password/reset`. Keep email recovery unchanged.
  - Add the dedicated invisible reCAPTCHA host element only during phone flows, label the UI accurately as Firebase SMS verification, and allow the user to return to edit their registration identifier.

- [ ] **Step 5: Verify and commit.**
  - Run: `npm run lint && npm run build` from `frontend/web`.
  - Commit: `feat: dùng Firebase OTP trên form số điện thoại`.

## Task 5: Configure Firebase environments and document the operational setup

**Files:**
- Modify: `backend/Fookbase.Src/Main/appsettings.json` or the existing configuration example only if it contains non-secret option names
- Modify: `backend/Fookbase.Src/.env.example`
- Modify: `frontend/web/.env.example`
- Create: `docs/firebase-phone-auth.md`

- [ ] **Step 1: Register the production web app in Firebase Console.**
  - In Firebase project `fookbase-cd714`, register the `Fookbase Web` app and copy its web configuration into deployment environment variables, not source control.
  - Add `fookbase-light-web.pages.dev` and the development host as Firebase Authentication authorized domains. Keep Phone provider enabled.
  - Do not add billing or a payment method without the owner’s explicit confirmation; the current Spark quota is 10 SMS/day.

- [ ] **Step 2: Prepare server secrets safely.**
  - Create a least-privilege Firebase Admin service account/key only in the Firebase/Google Cloud console.
  - Store its JSON as the production secret `FirebaseAuthentication__ServiceAccountJson`, and set `FirebaseAuthentication__Enabled=true` plus `FirebaseAuthentication__ProjectId=fookbase-cd714` in production.
  - Do not print, commit, or paste the key into terminal commands, chat, docs, or `.env.example`.

- [ ] **Step 3: Document exact environment names and verification.**
  - Document frontend `VITE_FIREBASE_*` variables, backend Firebase variables, authorized-domain requirements, Spark quota/upgrade behavior, and the manual verification path for an actual Vietnamese number.
  - Document rollback: set `FirebaseAuthentication__Enabled=false` and hide/disable phone registration in frontend deployment while email registration remains available.

- [ ] **Step 4: Final verification and commit.**
  - Run: `dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj --no-restore`.
  - Run: `npm run lint && npm run build` from `frontend/web`.
  - Commit: `docs: hướng dẫn cấu hình Firebase Phone Auth`.

## Final acceptance checks

- [ ] Email registration still sends a local email OTP and provisions only after that code is verified.
- [ ] Phone registration sends Firebase SMS from the browser, and the API accepts only an Admin-verified token for exactly that normalized number.
- [ ] A Firebase token for another number cannot create or reset an account.
- [ ] Phone password reset invalidates old sessions; email password reset remains unchanged.
- [ ] Firebase public Vite configuration and backend service-account secret are not committed.
- [ ] All Identity integration tests, frontend lint, and frontend build pass.
