# Photos & Albums V1 Design

## Goal

Add private, user-owned photo albums that organize existing ready image `MediaAsset` records without introducing a new binary-storage model, while retaining avatar, cover, and eligible timeline-photo history.

## Scope and constraints

- Keep the modular monolith, shared `FookbaseDbContext`, existing MinIO storage, existing Media upload flow, and protected cursor infrastructure.
- Do not add a DbContext, database, queue, repository abstraction, MediatR, search indexing, photo tags, recognition, feed entries, or notifications.
- Preserve existing avatar, profile cover, post, and media lifecycle behavior.
- Use a concise Photos module and one unified EF migration. Do not modify existing migrations.
- Add only 4–6 high-value backend integration tests; no exhaustive CRUD or frontend automated tests.

## Data model

Create `Modules/Photos` with these aggregates:

```text
PhotoAlbum
  Id
  OwnerUserId
  Name
  Description?
  Privacy (Public | Friends | OnlyMe)
  AlbumType (Custom | ProfilePictures | CoverPhotos | TimelinePhotos)
  CreatedAtUtc
  UpdatedAtUtc
  DeletedAtUtc?

AlbumMedia
  AlbumId
  MediaId
  Caption?
  SortOrder
  AddedAtUtc
```

`MediaAsset` remains the source of truth for the object and image metadata. `AlbumMedia` is only a logical reference and never copies a MinIO object.

Database constraints and indexes:

- unique active system album for `(OwnerUserId, AlbumType)` when `AlbumType != Custom`;
- unique `(AlbumId, MediaId)`;
- index `(AlbumId, SortOrder, MediaId)` for deterministic keyset album media paging;
- index `(OwnerUserId, CreatedAtUtc, Id)` for user album paging.

PostgreSQL's filtered unique index enforces the active-system-album rule, including concurrent lazy creation. The service catches a uniqueness race, clears EF tracking, and reads the winner.

## Album and membership policy

`Custom` albums are owner-created, editable, and soft-deletable. System albums are lazy-created and cannot be deleted through the normal album delete operation; their name/type stays system-controlled, while their viewer privacy follows the safe V1 default `Public`.

One image may be placed in at most one active `Custom` album. A request to put it in a second custom album is rejected. The same `MediaAsset` may independently be referenced by a post and by any system album, so historical avatar, cover, and timeline photos do not require object duplication.

Photos append by increasing `SortOrder`; the API returns deterministic newest-first ordering through a protected keyset cursor. Caption is plain text, trimmed and length-limited to 1,000 characters; HTML is neither stored nor rendered.

Removing an `AlbumMedia` only removes the album reference. It never deletes the `MediaAsset` or its storage object. Removing an entry that is the owner’s active avatar or profile cover is rejected; the existing profile-media change flow remains responsible for changing the active image.

## Access control

`PhotoAccessService` is the sole authorization rule for album, album-media, photo-detail, and photo-read URL endpoints. It evaluates:

1. owner access;
2. target profile existence;
3. bilateral block state, which always denies non-owner access;
4. album privacy: `Public`, `Friends` (accepted friendship), or `OnlyMe`.

Anonymous viewers can see only Public albums and their photos. Friend-only access requires an authenticated viewer. The owner-only `/api/media/{id}/access` endpoint remains unchanged, so it cannot be used by another user to bypass album privacy. Photo APIs create short-lived signed URLs only after album authorization and do not expose permanent MinIO URLs.

## Backend APIs

Create routes under `/api/albums` and `/api/users` consistent with the existing endpoint style:

```text
POST   /api/albums                         create Custom album
GET    /api/albums/{albumId}               accessible album detail
PATCH  /api/albums/{albumId}               owner updates Custom metadata
DELETE /api/albums/{albumId}               owner soft-deletes Custom album
POST   /api/albums/{albumId}/media         owner adds owned Ready Image
GET    /api/albums/{albumId}/media         accessible keyset-paged media
PATCH  /api/albums/{albumId}/media/{id}    owner updates caption
DELETE /api/albums/{albumId}/media/{id}    owner removes membership safely
GET    /api/albums/{albumId}/media/{id}    accessible photo detail with signed path
GET    /api/users/{userId}/albums          accessible keyset-paged album summaries
```

Album summaries are projected in batches with count and first/current first image preview. Album detail/media queries project owner and media metadata without per-image queries.

Adding media requires that it belongs to the actor, is `Ready`, has image type, and is not deleted. Media deletion is extended to reject an asset while any `AlbumMedia` reference exists, in addition to existing references.

## Existing-flow integrations

`UserProfileService` calls the Photos service only after its existing profile-media validation succeeds:

- When `AvatarMediaId` changes, lazily get/create `ProfilePictures` and add the image if missing.
- When `CoverMediaId` changes, lazily get/create `CoverPhotos` and add the image if missing.

Historical entries stay in those albums. This applies only to user-profile avatar and cover, never Page, Group, or Event covers.

`PostsService` calls the Photos service as part of creating a successful post. It adds owned ready image media to `TimelinePhotos` only when `ContainerType == Profile` and `PostType == Standard`. Group, Page, Event, Story, Reel, and Messenger media are excluded. A deleted post does not remove an existing album membership.

## Frontend

Add `api/photos.ts`, photo and album pages, and route entries for `/photos` and `/albums/:albumId`. Add accessible Photos/Albums entry points to a user profile.

The photos page displays recent accessible albums; album detail uses cursor-driven load-more instead of preloading a large collection. The create-album flow reuses `mediaApi.uploadFiles`, filters to images, uploads/completes first, then posts resulting media IDs to the album endpoint with a reasonable V1 selection limit.

A reusable lightbox receives the current page of photo detail data and supports image view, caption, owner/profile link, album link, previous/next controls, arrow keys, Escape, and close. It resolves the authorized short-lived image URL through the Photos API. No separate uploader or download policy is added.

## Testing and validation

Add 4–6 focused integration tests, covering:

1. OnlyMe/Friends privacy and block denial for album/photo access.
2. Foreign-owned image rejection and Media deletion rejection while AlbumMedia exists.
3. Avatar change creates/populates the ProfilePictures system album and active entry cannot be removed.
4. Profile Standard Post images populate TimelinePhotos while Group/Page/Event images do not.
5. Cover history is populated without disrupting the current cover.

Before final handoff, run the focused tests, backend regression once using the established Docker/PostgreSQL workflow, EF pending-model and fresh-migration checks, legacy import E2E, production Docker build and health endpoints, and web/messenger/admin build plus lint.

## Deferred items

- Face/object recognition, photo person tags, tag review, and coordinates.
- Album/photo search, cloud backup/sync, Memories, feed entries, and notifications.
- Drag-and-drop reordering, explicit album covers, and multiple Custom album membership.
