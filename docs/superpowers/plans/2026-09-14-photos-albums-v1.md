# Photos & Albums V1 Implementation Plan

> Cập nhật trạng thái 2026-09-15: chỉ các bước triển khai/tài liệu có mã nguồn hoặc tài liệu hiện hữu được đánh dấu; test, migration validation và smoke-test chưa được chạy trong phạm vi hiện tại.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users organize existing ready image MediaAssets into private custom and lazy system albums, then browse them safely from the web client.

**Architecture:** Add a focused `Modules/Photos` module backed by the shared `FookbaseDbContext`. `PhotoAccessService` centralizes privacy and block checks; `PhotosService` owns album mutations, query projections, and system-album integration points called from existing profile and post services. The web client consumes Photos endpoints, reuses `mediaApi` uploading, and renders a cursor-paged grid plus a reusable lightbox.

**Tech Stack:** .NET 10 minimal APIs, EF Core/PostgreSQL, existing MinIO `MediaService`, React/TypeScript/Vite/Tailwind, xUnit integration tests.

**Spec:** `docs/superpowers/specs/2026-09-14-photos-albums-v1-design.md`

## Global Constraints

- Keep the modular monolith, shared `FookbaseDbContext`, existing MinIO storage and Media upload flow.
- Do not add a DbContext, storage entity, database, queue, repository, MediatR, photo/album search, feed entries, notifications, tags, recognition, or sync.
- `MediaAsset` remains physical-object truth; AlbumMedia is only a reference and must never copy an object.
- At most one active Custom album membership per image; system album and Post references remain independently allowed.
- Enforce privacy and bilateral block state for every Photos read and signed URL; only owners manage custom albums.
- Use protected keyset cursors, projected/batched reads, and no N+1 loops.
- Add 4–6 high-value integration tests only; preserve existing regression behavior.
- Do not modify historical migrations or stage unrelated files, including user-owned README changes.

---

## File structure

```text
backend/Fookbase.Src/Main/Code/
  Modules/Photos/
    Entities/PhotoAlbum.cs
    Entities/AlbumMedia.cs
    Data/Configurations/PhotoAlbumConfiguration.cs
    Data/Configurations/AlbumMediaConfiguration.cs
    DTOs/Requests/PhotoAlbumRequests.cs
    DTOs/Responses/PhotoAlbumResponses.cs
    Services/PhotoAccessService.cs
    Services/PhotosService.cs
    Endpoints/PhotoAlbumEndpoints.cs
    DependencyInjection.cs
  Persistence/FookbaseDbContext.cs
  Persistence/Migrations/<timestamp>_AddPhotosAlbumsV1.cs
  Persistence/Migrations/<timestamp>_AddPhotosAlbumsV1.Designer.cs
  Persistence/Migrations/FookbaseDbContextModelSnapshot.cs
  Modules/Media/Services/MediaService.cs
  Modules/Users/Services/UserProfileService.cs
  Modules/Posts/Services/PostsService.cs
  Program.cs
backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/
  PhotoAlbumEndpointsTests.cs
frontend/web/src/
  api/photos.ts
  pages/photos/PhotosPage.tsx
  pages/photos/AlbumDetailPage.tsx
  pages/photos/PhotoViewer.tsx
  pages/profile/UserProfilePage.tsx
  routes/index.tsx
docs/photos-albums-v1.md
```

### Task 1: Persist the Photos aggregate and register its module

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/Entities/PhotoAlbum.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/Entities/AlbumMedia.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/Data/Configurations/PhotoAlbumConfiguration.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/Data/Configurations/AlbumMediaConfiguration.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/DependencyInjection.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Persistence/FookbaseDbContext.cs`
- Modify: `backend/Fookbase.Src/Main/Program.cs`
- Create: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/<timestamp>_AddPhotosAlbumsV1.cs`
- Create: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/<timestamp>_AddPhotosAlbumsV1.Designer.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/FookbaseDbContextModelSnapshot.cs`
- Test: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs`

**Interfaces:**
- Consumes: `MediaAsset`, `UserProfile`, the `ApplyConfigurationsFromAssembly` convention in `FookbaseDbContext`.
- Produces: `PhotoAlbum`, `AlbumMedia`, `PhotoAlbumType`, `PhotoAlbumPrivacy`, `db.PhotoAlbums`, `db.AlbumMedia`, and `AddPhotosModule()`.

- [ ] **Step 1: Write a failing migration-backed system-album test**

```csharp
[Fact]
public async Task Avatar_change_creates_one_profile_pictures_system_album()
{
    var owner = (await CreateUsersAsync(1))[0];
    var mediaId = await SeedReadyImageAsync(owner);
    using var client = CreateAuthenticatedClient(owner);

    var response = await client.PatchAsJsonAsync("/api/users/me", new { avatarMediaId = mediaId });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FookbaseDbContext>();
    Assert.Single(await db.PhotoAlbums.Where(x => x.OwnerUserId == owner &&
        x.AlbumType == PhotoAlbumType.ProfilePictures).ToListAsync());
}
```

- [ ] **Step 2: Run the test and verify RED**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter FullyQualifiedName~PhotoAlbumEndpointsTests.Avatar_change_creates_one_profile_pictures_system_album`

Expected: compilation fails because `PhotoAlbum` and `PhotoAlbumType` do not exist.

- [x] **Step 3: Add the minimal aggregate, mapping, and EF migration**

```csharp
public enum PhotoAlbumType { Custom, ProfilePictures, CoverPhotos, TimelinePhotos }
public enum PhotoAlbumPrivacy { Public, Friends, OnlyMe }

public sealed class PhotoAlbum
{
    public const int MaximumNameLength = 160;
    public const int MaximumDescriptionLength = 2_000;
    // CreateCustom, CreateSystem, UpdateCustom, SoftDelete and guarded normalization.
}

public sealed class AlbumMedia
{
    public const int MaximumCaptionLength = 1_000;
    // Create(albumId, mediaId, sortOrder, now), UpdateCaption(caption), with private setters.
}
```

Configure a filtered unique index for active non-custom system albums, `(OwnerUserId, AlbumType)`, plus unique `(AlbumId, MediaId)`, `(AlbumId, SortOrder, MediaId)`, and `(OwnerUserId, CreatedAtUtc, Id)` indexes. Add the DbSets/import, `builder.Services.AddPhotosModule()`, and invoke `app.MapPhotoAlbumEndpoints()` later in Task 2. Generate a normal unified migration with the existing EF command, without editing prior migrations.

- [ ] **Step 4: Run the failing test again and build the backend**

Run: `dotnet build FookbaseLight.sln --no-restore`

Expected: the project builds; the focused test remains RED until Task 4 adds profile integration.

- [ ] **Step 5: Commit the persistence slice**

```bash
git add backend/Fookbase.Src/Main/Code/Persistence \
  backend/Fookbase.Src/Main/Code/Modules/Photos \
  backend/Fookbase.Src/Main/Program.cs \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs
git commit -m "feat: thêm mô hình album ảnh"
git push origin main
```

### Task 2: Implement photo authorization, read projections, and read endpoints

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/Services/PhotoAccessService.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/Services/PhotosService.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/DTOs/Requests/PhotoAlbumRequests.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/DTOs/Responses/PhotoAlbumResponses.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Photos/Endpoints/PhotoAlbumEndpoints.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Photos/DependencyInjection.cs`
- Modify: `backend/Fookbase.Src/Main/Program.cs`
- Modify: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs`

**Interfaces:**
- Consumes: `PhotoAlbum`, `AlbumMedia`, `Friendships`, `BlockedUsers`, `MediaService.CreateReadUrlAsync`, current JWT claim convention.
- Produces: `PhotosService.GetAlbumAsync(Guid, Guid?)`, `GetAlbumMediaAsync(Guid, Guid?, string?, int)`, `GetUserAlbumsAsync(Guid, Guid?, string?, int)`, and `GetPhotoAsync(Guid, Guid, Guid?)`.

- [ ] **Step 1: Write failing privacy/block tests**

```csharp
[Fact]
public async Task Friends_and_only_me_album_media_are_hidden_from_unrelated_or_blocked_viewers()
{
    var users = await CreateUsersAsync(3);
    var owner = users[0];
    var friend = users[1];
    var stranger = users[2];
    var album = await CreateAlbumAsync(owner, "friends");
    await AddReadyImageAsync(owner, album.Id);

    await CreateFriendshipAsync(owner, friend);
    Assert.Equal(HttpStatusCode.OK, (await CreateAuthenticatedClient(friend).GetAsync($"/api/albums/{album.Id}")).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await CreateAuthenticatedClient(stranger).GetAsync($"/api/albums/{album.Id}")).StatusCode);
    await BlockAsync(owner, friend);
    Assert.Equal(HttpStatusCode.NotFound, (await CreateAuthenticatedClient(friend).GetAsync($"/api/albums/{album.Id}/media")).StatusCode);
}
```

- [ ] **Step 2: Run the privacy test and verify RED**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter FullyQualifiedName~PhotoAlbumEndpointsTests.Friends_and_only_me_album_media_are_hidden_from_unrelated_or_blocked_viewers`

Expected: 404 because `/api/albums` is not mapped.

- [x] **Step 3: Implement central access and projection-only reads**

```csharp
public async Task<bool> CanViewAsync(PhotoAlbum album, Guid? viewerUserId, CancellationToken ct)
{
    if (viewerUserId == album.OwnerUserId) return true;
    if (viewerUserId is null || await IsBlockedAsync(album.OwnerUserId, viewerUserId.Value, ct)) return false;
    return album.Privacy == PhotoAlbumPrivacy.Public ||
        album.Privacy == PhotoAlbumPrivacy.Friends && await AreFriendsAsync(album.OwnerUserId, viewerUserId.Value, ct);
}
```

Map anonymous read endpoints and authorization-required mutation endpoints. Parse the optional principal into `Guid?`; return NotFound for inaccessible resources. Build signed photo response only after `CanViewAsync`, calling `MediaService.CreateReadUrlAsync`. Use one media/owner projection query and a keyset predicate over `(SortOrder, MediaId)` rather than materializing all album rows.

- [ ] **Step 4: Run the privacy test and verify GREEN**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter FullyQualifiedName~PhotoAlbumEndpointsTests.Friends_and_only_me_album_media_are_hidden_from_unrelated_or_blocked_viewers`

Expected: PASS.

- [ ] **Step 5: Commit the safe read slice**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Photos \
  backend/Fookbase.Src/Main/Program.cs \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs
git commit -m "feat: thêm truy cập riêng tư album ảnh"
git push origin main
```

### Task 3: Implement custom album and membership mutations with media deletion protection

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Photos/Services/PhotosService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Photos/Endpoints/PhotoAlbumEndpoints.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Media/Services/MediaService.cs`
- Modify: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs`

**Interfaces:**
- Consumes: Task 2 authorization and `MediaAsset` lifecycle states.
- Produces: `CreateCustomAsync`, `UpdateCustomAsync`, `DeleteCustomAsync`, `AddMediaAsync`, `UpdateCaptionAsync`, `RemoveMediaAsync`, and Media deletion guard against `db.AlbumMedia`.

- [ ] **Step 1: Write failing ownership/reference safety test**

```csharp
[Fact]
public async Task Foreign_image_is_rejected_and_album_referenced_image_cannot_be_deleted()
{
    var users = await CreateUsersAsync(2);
    var album = await CreateAlbumAsync(users[0], "onlyme");
    var foreignImage = await SeedReadyImageAsync(users[1]);
    using var ownerClient = CreateAuthenticatedClient(users[0]);

    Assert.Equal(HttpStatusCode.Forbidden, (await ownerClient.PostAsJsonAsync(
        $"/api/albums/{album.Id}/media", new { mediaId = foreignImage })).StatusCode);
    var ownedImage = await SeedReadyImageAsync(users[0]);
    (await ownerClient.PostAsJsonAsync($"/api/albums/{album.Id}/media", new { mediaId = ownedImage })).EnsureSuccessStatusCode();
    Assert.Equal(HttpStatusCode.Conflict, (await ownerClient.DeleteAsync($"/api/media/{ownedImage}")).StatusCode);
}
```

- [ ] **Step 2: Run the ownership/reference test and verify RED**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter FullyQualifiedName~PhotoAlbumEndpointsTests.Foreign_image_is_rejected_and_album_referenced_image_cannot_be_deleted`

Expected: foreign media is not yet rejected or deletion succeeds because album references are not checked.

- [x] **Step 3: Implement the smallest mutation surface**

```csharp
public async Task<ApplicationResult> AddMediaAsync(Guid actorId, Guid albumId, Guid mediaId, CancellationToken ct)
{
    var album = await FindOwnedActiveAlbumAsync(actorId, albumId, ct);
    var media = await FindOwnedReadyImageAsync(actorId, mediaId, ct);
    RejectSecondCustomMembershipIfNeeded(album, media.Id, ct);
    dbContext.AlbumMedia.Add(AlbumMedia.Create(album.Id, media.Id, await NextSortOrderAsync(album.Id, ct), timeProvider.GetUtcNow()));
    await dbContext.SaveChangesAsync(ct);
    return ApplicationResult.Success();
}
```

Apply `Custom`-only ownership/update/delete rules. Reject system album deletion and direct add/remove mutation unless the operation is the internal system integration. Remove a membership without touching `MediaAsset`; reject removal if it is the active user avatar or cover. Add one `AnyAsync` for `AlbumMedia.MediaId == mediaId` to `MediaService.DeleteAsync` and include it in the existing `media_is_referenced` condition.

- [ ] **Step 4: Run the mutation test and verify GREEN**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter FullyQualifiedName~PhotoAlbumEndpointsTests.Foreign_image_is_rejected_and_album_referenced_image_cannot_be_deleted`

Expected: PASS.

- [ ] **Step 5: Commit the mutation slice**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Photos \
  backend/Fookbase.Src/Main/Code/Modules/Media/Services/MediaService.cs \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs
git commit -m "feat: quản lý ảnh trong album"
git push origin main
```

### Task 4: Integrate avatar, cover, and profile timeline photos

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Photos/Services/PhotosService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Users/Services/UserProfileService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/DependencyInjection.cs`
- Modify: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs`

**Interfaces:**
- Consumes: `UserProfileService.UpdateAsync`, `PostsService.CreatePostInContainerCoreAsync`, `PhotoAlbumType`.
- Produces: `PhotosService.AddSystemMediaAsync(Guid ownerId, PhotoAlbumType type, Guid mediaId, CancellationToken)`.

- [ ] **Step 1: Write failing system-integration tests**

```csharp
[Fact]
public async Task Profile_standard_post_images_enter_timeline_but_group_page_and_event_images_do_not()
{
    var user = (await CreateUsersAsync(1))[0];
    var profileImage = await SeedReadyImageAsync(user);
    using var client = CreateAuthenticatedClient(user);
    (await client.PostAsJsonAsync("/api/posts", new { content = "photo", privacy = "public", mediaIds = new[] { profileImage } })).EnsureSuccessStatusCode();

    Assert.True(await HasSystemMembershipAsync(user, PhotoAlbumType.TimelinePhotos, profileImage));
    // Seed accessible Group, Page and Event and create a post with one owned ready image in each.
    Assert.False(await HasSystemMembershipAsync(user, PhotoAlbumType.TimelinePhotos, groupImage));
    Assert.False(await HasSystemMembershipAsync(user, PhotoAlbumType.TimelinePhotos, pageImage));
    Assert.False(await HasSystemMembershipAsync(user, PhotoAlbumType.TimelinePhotos, eventImage));
}
```

- [ ] **Step 2: Run the system-integration test and verify RED**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter FullyQualifiedName~PhotoAlbumEndpointsTests.Profile_standard_post_images_enter_timeline_but_group_page_and_event_images_do_not`

Expected: Profile media is absent from TimelinePhotos.

- [x] **Step 3: Add idempotent, concurrency-safe system membership calls**

```csharp
public async Task AddSystemMediaAsync(Guid ownerId, PhotoAlbumType albumType, Guid mediaId, CancellationToken ct)
{
    var album = await GetOrCreateSystemAlbumAsync(ownerId, albumType, ct);
    if (!await dbContext.AlbumMedia.AnyAsync(x => x.AlbumId == album.Id && x.MediaId == mediaId, ct))
        dbContext.AlbumMedia.Add(AlbumMedia.Create(album.Id, mediaId, await NextSortOrderAsync(album.Id, ct), timeProvider.GetUtcNow()));
}
```

Inject `PhotosService` into `UserProfileService`; inside the existing transaction, only add a newly supplied avatar or cover after profile-media validation and synchronization succeed. Inject it into `PostsService`; after creating the post, add only ready image IDs for `PostContainerType.Profile` and `PostType.Standard`. Do not call the integration for Group, Page, Event, Story, Reel, or Messenger flows. Catch only the expected filtered-index uniqueness exception in system-album creation, clear tracking, then re-read the active row.

- [ ] **Step 4: Run the system-integration tests and verify GREEN**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter FullyQualifiedName~PhotoAlbumEndpointsTests`

Expected: all focused Photos tests pass, including active avatar/cover removal rejection.

- [ ] **Step 5: Commit system integrations**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Photos \
  backend/Fookbase.Src/Main/Code/Modules/Users/Services/UserProfileService.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsService.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/DependencyInjection.cs \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PhotoAlbumEndpointsTests.cs
git commit -m "feat: lưu lịch sử ảnh hồ sơ"
git push origin main
```

### Task 5: Add the web Photos experience and reusable viewer

**Files:**
- Create: `frontend/web/src/api/photos.ts`
- Create: `frontend/web/src/pages/photos/PhotosPage.tsx`
- Create: `frontend/web/src/pages/photos/AlbumDetailPage.tsx`
- Create: `frontend/web/src/pages/photos/PhotoViewer.tsx`
- Modify: `frontend/web/src/routes/index.tsx`
- Modify: `frontend/web/src/pages/profile/UserProfilePage.tsx`
- Modify: `frontend/web/src/layout/Sidebar.tsx`

**Interfaces:**
- Consumes: `/api/albums`, `/api/users/{userId}/albums`, `mediaApi.uploadFiles`, existing `apiRequest` and auth layout.
- Produces: `photosApi`, routes `/photos` and `/albums/:albumId`, and `PhotoViewer` props `{ items, initialIndex, onClose }`.

- [x] **Step 1: Define type-safe API client contracts before UI use**

```ts
export interface PhotoAlbumSummary {
  id: string; name: string; albumType: 'custom' | 'profilePictures' | 'coverPhotos' | 'timelinePhotos'
  privacy: 'public' | 'friends' | 'onlyMe'; photoCount: number; previewUrl: string | null; createdAtUtc: string
}
export const photosApi = {
  create: (value: CreateAlbum) => apiRequest<PhotoAlbum>('/api/albums', { method: 'POST', body: JSON.stringify(value) }),
  get: (albumId: string) => apiRequest<PhotoAlbum>(`/api/albums/${albumId}`),
  media: (albumId: string, cursor?: string) => apiRequest<CursorPage<AlbumPhoto>>(`/api/albums/${albumId}/media?${cursorQuery(cursor)}`),
  addMedia: (albumId: string, mediaId: string) => apiRequest<void>(`/api/albums/${albumId}/media`, { method: 'POST', body: JSON.stringify({ mediaId }) }),
}
```

- [x] **Step 2: Build the photos page and album detail with bounded loading**

Create form state for Custom name, description, privacy, and a maximum selection constant of 20 image files. On submit call `mediaApi.uploadFiles(files)` then `photosApi.addMedia` in sequence; display a request error and retain no base64 data. Render album summary cards, photos grid, and a Load more button only when the cursor is present.

- [x] **Step 3: Implement the reusable accessible viewer**

```tsx
useEffect(() => {
  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') onClose()
    if (event.key === 'ArrowLeft') setIndex((value) => Math.max(0, value - 1))
    if (event.key === 'ArrowRight') setIndex((value) => Math.min(items.length - 1, value + 1))
  }
  window.addEventListener('keydown', onKeyDown)
  return () => window.removeEventListener('keydown', onKeyDown)
}, [items.length, onClose])
```

Use a dialog-style fixed overlay, image alt based on caption, previous/next disabled at page bounds, caption, owner profile link, album link, and Close control. Do not manufacture download controls or preload every album image.

- [x] **Step 4: Wire routes and profile navigation**

Add lazy imports and routes in `routes/index.tsx`, a Photos item in the existing sidebar, and profile links/album previews by calling `photosApi.userAlbums(userId)`. Resolve only endpoint-provided signed/redirect URLs; do not call owner-only Media `/access` for another profile.

- [x] **Step 5: Verify the frontend production checks**

Run:

```bash
cd frontend/web && npm run build && npm run lint
```

Expected: both commands exit 0.

- [ ] **Step 6: Commit the frontend slice**

```bash
git add frontend/web/src/api/photos.ts frontend/web/src/pages/photos \
  frontend/web/src/routes/index.tsx frontend/web/src/pages/profile/UserProfilePage.tsx \
  frontend/web/src/layout/Sidebar.tsx
git commit -m "feat: thêm giao diện ảnh và album"
git push origin main
```

### Task 6: Document, migrate, and validate the completed milestone

**Files:**
- Create: `docs/photos-albums-v1.md`
- Modify: only files found necessary by validation; do not alter unrelated source.

**Interfaces:**
- Consumes: all completed Photos APIs, migration, frontend routes, and established Docker test workflow.
- Produces: concise product/maintenance documentation and verified M14 handoff evidence.

- [x] **Step 1: Write concise product documentation**

Document exact album types/privacy, lazy system albums, one-Custom-membership policy, `MediaAsset` references, avatar/cover/timeline conditions, public API routes, cursor behavior, deletion safety, and deferred tags/search/recognition/feed/notifications.

- [ ] **Step 2: Run backend focused tests and full regression once**

Run the focused `PhotoAlbumEndpointsTests`, then the established sequential Docker/PostgreSQL integration workflow once for Identity, Friends, Users, Messages, Posts, and Media. Do not change test infrastructure for any existing parallel-DB issue.

- [ ] **Step 3: Verify schema and operational paths**

Run EF `migrations has-pending-model-changes`, create/apply the migration in a fresh disposable PostgreSQL database, run `./scripts/test-legacy-import-e2e.sh`, build `docker compose build api`, and run a uniquely named/ported disposable API+PostgreSQL+MinIO health smoke against `/health/live` and `/health/ready`.

- [ ] **Step 4: Run all frontend regression checks once**

Run:

```bash
cd frontend/web && npm run build && npm run lint
cd ../messenger && npm run build && npm run lint
cd ../admin && npm run build && npm run lint
```

Expected: all six commands exit 0.

- [ ] **Step 5: Review the requirement checklist and working tree**

Confirm each spec constraint: no new storage or search, each privacy surface uses PhotoAccessService, blocks deny access, no permanent MinIO URLs, no N+1 projection loop, only 4–6 focused tests, and generated files/secrets/README are not staged. Run `git diff --check`, inspect `git status --short`, and inspect the final staged diff.

- [ ] **Step 6: Commit documentation/verification-only changes and push**

```bash
git add docs/photos-albums-v1.md
git commit -m "docs: bổ sung hướng dẫn ảnh và album v1"
git push origin main
```

If validation requires a focused fix, add only the directly affected files to a separate Vietnamese Conventional Commit after rerunning the failed command.
