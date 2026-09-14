# Cloudinary Media Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chuyển hoàn toàn ảnh/video từ MinIO sang Cloudinary nhưng vẫn upload trực tiếp từ browser và giữ workflow media hiện có.

**Architecture:** API tạo `PendingUpload`, ký Cloudinary form intent cho public ID deterministic, rồi browser POST multipart trực tiếp tới Cloudinary. `MediaAsset`, PostgreSQL job và FFmpeg giữ nguyên; adapter Cloudinary đảm nhận xác minh source, authenticated delivery, upload output và deletion.

**Tech Stack:** ASP.NET Core/.NET 10, EF Core/PostgreSQL, CloudinaryDotNet 1.29.3, FFmpeg, React/TypeScript/Vite, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-15-cloudinary-media-migration-design.md`

## Global Constraints

- Cloudinary SDK dùng HTTPS và chữ ký SHA-256; không thêm SDK JavaScript.
- `Cloudinary__ApiSecret` chỉ ở API environment/secret store, không log, commit hay đưa vào `VITE_*`/browser.
- Source, processed MP4 và poster JPEG đều có Cloudinary delivery `type=authenticated`.
- Giữ schema `MediaAsset`/`ObjectDeletion`, public ID deterministic; không migration DB, copy MinIO hoặc xóa database/volume.
- Signed delivery URL không tự hết hạn trên tài khoản Cloudinary thường; access endpoint vẫn kiểm tra quyền trước khi trả URL.
- Backend, web và Zola Light deploy cùng release vì upload đổi từ `PUT` sang multipart `POST`.
- Theo yêu cầu người dùng, không tạo hoặc chạy automated test. Mỗi task vẫn phải chạy build/lint hiện có và không commit nếu thay đổi gây lỗi.
- Mỗi task là một commit Conventional Commit tiếng Việt và push `origin/main`.

---

### Task 1: Chuyển toàn bộ media backend sang Cloudinary

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Media/Config/CloudinaryOptions.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Media/Services/CloudinaryObjectStorage.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Media/HealthChecks/CloudinaryHealthCheck.cs`
- Modify: `backend/Fookbase.Src/Main/Fookbase.Api.csproj`
- Modify: `backend/Fookbase.Src/Main/appsettings.json`
- Modify: `backend/Fookbase.Src/Main/Program.cs`
- Modify: `backend/Fookbase.Src/Main/Code/ModuleServiceCollectionExtensions.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Shared/Config/ProductionConfigurationValidator.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/DependencyInjection.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/Services/IObjectStorage.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/DTOs/Responses/UploadIntentResponse.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/Services/MediaService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/Background/VideoProcessingWorker.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/Background/ObjectDeletionWorker.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/Background/PendingUploadCleanupWorker.cs`
- Delete: `backend/Fookbase.Src/Main/Code/Modules/Media/Config/MinioOptions.cs`
- Delete: `backend/Fookbase.Src/Main/Code/Modules/Media/Services/MinioObjectStorage.cs`
- Delete: `backend/Fookbase.Src/Main/Code/Modules/Media/Services/MinioPresignedUrlClient.cs`
- Delete: `backend/Fookbase.Src/Main/Code/Modules/Media/Background/MinioBucketInitializer.cs`
- Delete: `backend/Fookbase.Src/Main/Code/Modules/Media/HealthChecks/MinioBucketHealthCheck.cs`

**Interfaces:**
- Consumes: `MediaType`, `MediaAsset`, existing workers and `MediaReadUrlResponse`.
- Produces: Cloudinary-backed upload/read/delete workflow consumed by Task 2 and readiness name `cloudinary` consumed by Task 3.

- [ ] **Step 1: Replace MinIO package/configuration and register Cloudinary.**

  Replace `<PackageReference Include="Minio" Version="7.0.0" />` with `<PackageReference Include="CloudinaryDotNet" Version="1.29.3" />`. Create `CloudinaryOptions` and validate its three required values:

  ```csharp
  public sealed class CloudinaryOptions
  {
      public const string SectionName = "Cloudinary";
      public string CloudName { get; init; } = string.Empty;
      public string ApiKey { get; init; } = string.Empty;
      public string ApiSecret { get; init; } = string.Empty;

      public void Validate()
      {
          if (string.IsNullOrWhiteSpace(CloudName) || string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(ApiSecret))
              throw new InvalidOperationException("Cloudinary cloud name, API key and API secret are required.");
      }
  }
  ```

  Replace the MinIO appsettings section with empty Cloudinary defaults. Bind/validate it in `AddMediaModule` and production validation. In media DI register one HTTPS Cloudinary SDK client configured for SHA-256 signatures; preserve FFmpeg and existing workers but remove the bucket initializer. Replace the ready registration `minio` with `cloudinary`; `CloudinaryHealthCheck` performs a bounded credentialed Cloudinary ping and reports unhealthy for every exception.

- [ ] **Step 2: Replace the storage contract and implement Cloudinary operations.**

  Introduce a form intent and make each Cloudinary call resource-type aware:

  ```csharp
  public sealed record DirectUploadIntent(string UploadUrl, IReadOnlyDictionary<string, string> UploadParameters);

  Task<DirectUploadIntent> CreateDirectUploadIntentAsync(string publicId, MediaType mediaType, TimeSpan expiry, CancellationToken cancellationToken = default);
  Task<string> CreateSignedGetUrlAsync(string publicId, MediaType mediaType, CancellationToken cancellationToken = default);
  Task<StoredObjectInfo?> GetInfoAsync(string publicId, MediaType mediaType, CancellationToken cancellationToken = default);
  Task<byte[]> ReadPrefixAsync(string publicId, MediaType mediaType, int length, CancellationToken cancellationToken = default);
  Task DownloadToFileAsync(string publicId, MediaType mediaType, string destinationPath, CancellationToken cancellationToken = default);
  Task UploadFileAsync(string publicId, MediaType mediaType, string sourcePath, string contentType, CancellationToken cancellationToken = default);
  Task DeleteAsync(string publicId, MediaType mediaType, CancellationToken cancellationToken = default);
  ```

  `CloudinaryObjectStorage` creates `https://api.cloudinary.com/v1_1/{cloudName}/{resourceType}/upload` intents and signs `public_id`, `type=authenticated`, `overwrite=false`, and timestamp. Choose a timestamp within Cloudinary's one-hour validity interval so upload signature expiry is no later than `Media.UploadUrlExpiryMinutes`. Metadata must include bytes, resource type and delivery type; `GetInfoAsync` returns null only for not found. Build signed authenticated delivery URLs, use a signed stream/range read for prefix checking and download, upload worker outputs as authenticated, and delete with invalidation. Never return an unsigned `secure_url`; all non-not-found Cloudinary failures propagate.

- [ ] **Step 3: Update API response, completion checks and workers together.**

  Replace the response contract and use it from `MediaService`:

  ```csharp
  public sealed record UploadIntentResponse(
      Guid MediaId,
      string UploadUrl,
      string UploadMethod,
      IReadOnlyDictionary<string, string> UploadParameters,
      DateTimeOffset ExpiresAtUtc);
  ```

  `CreateUploadAsync` stores the existing pending asset then returns `POST` plus the direct form parameters. `CompleteAsync` must query typed metadata, reject wrong resource/delivery type, retain size/magic-byte checks, and pass `asset.MediaType` into read/prefix/download URL calls. The owner/read endpoint paths and `MediaReadUrlResponse` fields remain unchanged.

  In `VideoProcessingWorker`, download source/upload normalized MP4 as `MediaType.Video`, and upload poster as `MediaType.Image`. In `ObjectDeletionWorker`, load `MediaAsset` for `job.MediaId`, map source to `asset.MediaType`, processed key to `Video`, poster key to `Image`; for missing asset or unknown key, record a failed retry instead of guessing an image type. Keep durable retry, status changes and temp file cleanup intact. `PendingUploadCleanupWorker` only queues source deletion as it does today.

- [ ] **Step 4: Remove the MinIO implementation files and build the API image.**

  Delete the five MinIO-only files listed above only after all call sites compile against Cloudinary. Run:

  ```bash
  docker compose build api
  rg -n "Minio|IMinioClient|CreatePresignedPutUrlAsync|CreatePresignedGetUrlAsync" backend/Fookbase.Src/Main
  ```

  Expected: image build succeeds and the search produces no runtime source match.

- [ ] **Step 5: Commit and push the backend migration.**

  ```bash
  git add backend/Fookbase.Src/Main
  git commit -m "feat: chuyển media backend sang Cloudinary"
  git push origin main
  ```

### Task 2: Chuyển web và Zola Light sang multipart upload

**Files:**
- Modify: `frontend/web/src/api/media.ts`
- Modify: `frontend/zola-light/src/api.ts`

**Interfaces:**
- Consumes: `UploadIntentResponse` from Task 1 (`uploadMethod: "POST"`, `uploadParameters`).
- Produces: existing upload helpers work without changing page components.

- [ ] **Step 1: Update the web API type and transport while preserving progress.**

  Extend `UploadIntent` with `uploadMethod: 'POST'` and `uploadParameters: Record<string, string>`. In `uploadToStorage`, append all server parameters first and `file` last, use the server method/URL, and do not set multipart content type manually:

  ```ts
  const body = new FormData()
  for (const [name, value] of Object.entries(uploadIntent.uploadParameters)) body.append(name, value)
  body.append('file', file)
  request.open(uploadIntent.uploadMethod, uploadIntent.uploadUrl)
  request.send(body)
  ```

  Preserve XMLHttpRequest upload progress, error/abort branches, API completion and public `mediaApi.uploadFile*` signatures. Do not retain or expose Cloudinary upload response payload.

- [ ] **Step 2: Update Zola Light's equivalent compact upload function.**

  Type `mediaId`, `uploadUrl`, `uploadMethod`, and `uploadParameters`. Build the same `FormData`, call `fetch(intent.uploadUrl, { method: intent.uploadMethod, body })`, preserve `ApiError` on failure and then call the existing `/complete` endpoint. Do not add a Cloudinary browser dependency.

- [ ] **Step 3: Lint, build, commit and push the two clients.**

  Run:

  ```bash
  (cd frontend/web && npm run lint && npm run build)
  (cd frontend/zola-light && npm run lint && npm run build)
  ```

  Then:

  ```bash
  git add frontend/web/src/api/media.ts frontend/zola-light/src/api.ts
  git commit -m "feat: tải media trực tiếp lên Cloudinary"
  git push origin main
  ```

### Task 3: Bỏ MinIO khỏi Compose và hướng dẫn vận hành

**Files:**
- Modify: `.env.example`
- Modify: `compose.yml`
- Modify: `compose.prod.yml`
- Modify: `README.md`
- Modify: `docs/production-deployment.md`
- Delete: `infrastructure/minio/nginx.conf`

**Interfaces:**
- Consumes: Cloudinary environment names/readiness from Task 1.
- Produces: local and production Compose with only API/PostgreSQL plus Cloudinary credentials supplied externally.

- [ ] **Step 1: Replace environment variables and Compose services.**

  Replace all `MINIO_*`/`Minio__*` variables with empty example values only:

  ```dotenv
  Cloudinary__CloudName=
  Cloudinary__ApiKey=
  Cloudinary__ApiSecret=
  ```

  Remove `minio`, `minio-cors`, `minio-bootstrap`, their `depends_on`, ports, mounts and `minio-data` Compose declaration. Delete the Nginx CORS file once unreferenced. Do not execute `docker volume rm`; a pre-existing MinIO volume stays untouched.

- [ ] **Step 2: Update README and production runbook.**

  Replace MinIO architecture/startup/bucket/backup/capacity instructions with Cloudinary signed direct upload, authenticated delivery, server-side secret configuration, account-side browser origin allow-list and Cloudinary media retention. State that standard signed delivery links can be shared and do not auto-expire; legacy MinIO media is unavailable after cutover; rollback after Cloudinary uploads requires a Cloudinary-compatible release.

- [ ] **Step 3: Validate runtime configuration and all frontend projects.**

  Run:

  ```bash
  docker compose config --quiet
  docker compose -f compose.yml -f compose.prod.yml config --quiet
  docker compose build api
  for app in web zola-light admin; do (cd "frontend/$app" && npm run lint && npm run build); done
  git diff --check
  rg -n "Minio|MINIO_|minio-cors|minio-bootstrap" compose.yml compose.prod.yml backend/Fookbase.Src/Main .env.example
  ```

  Expected: both Compose documents parse, image and all frontend builds pass, diff whitespace is clean, and search has no runtime MinIO reference.

- [ ] **Step 4: Commit and push deployment cleanup.**

  ```bash
  git add .env.example compose.yml compose.prod.yml README.md docs/production-deployment.md infrastructure/minio/nginx.conf
  git commit -m "chore: cấu hình Cloudinary thay MinIO"
  git push origin main
  ```

### Task 4: Smoke release bằng Cloudinary test environment

**Files:**
- Modify: none unless a concrete build/lint defect requires a focused fix.

**Interfaces:**
- Consumes: Tasks 1–3 deployed together and a separate Cloudinary test environment.
- Produces: manual evidence for the release; no empty commit.

- [ ] **Step 1: Set test credentials and browser origins outside Git.**

  Add `Cloudinary__CloudName`, `Cloudinary__ApiKey`, and `Cloudinary__ApiSecret` using deployment secrets and allow the three frontend HTTPS origins in Cloudinary. Never put actual values in tracked files, browser config, logs or request transcripts.

- [ ] **Step 2: Run the manual release checklist.**

  Upload JPEG/PNG/WebP and verify ready rendering; upload MP4/WebM and verify processed MP4/poster plus Story/Reel creation; request an asset as owner and unauthorized user; delete an unreferenced asset and verify eventual Cloudinary removal; try an expired intent; check `/health/live` and `/health/ready`. Record only result and request ID, never signed URL/credential.

- [ ] **Step 3: Fix only a confirmed smoke defect.**

  If a smoke check finds a code defect, make the smallest scoped change, rerun its build/lint command, then create/push a Vietnamese Conventional Commit. Otherwise leave Git history unchanged.
