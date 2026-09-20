# Modular-monolith architecture decision

## Scope

Fookbase runs as one ASP.NET Core process, `Fookbase.Api`, on port 5000. Identity, Users, Friends, Feed, Groups, Messages, Notifications, Posts, and Media remain independent code modules, but they are not independently deployed services.

`Fookbase.Api` is the sole composition root and the only backend project. It registers `FookbaseDbContext` once, then registers each module through `AddIdentityModule`, `AddUsersModule`, `AddFriendsModule`, `AddFeedModule`, `AddGroupsModule`, `AddMessagesModule`, `AddNotificationsModule`, `AddPostsModule`, and `AddMediaModule`. Module code is organized under `backend/Fookbase.Src/Main/Code/Modules/<Module>`; HTTP endpoints and entity configurations remain in those module folders. The runtime migration and snapshot live under `Code/Persistence/Migrations`.

External local dependencies are PostgreSQL and MinIO. There is no API gateway, RabbitMQ, service discovery, distributed transaction, or HTTP communication between application modules.

## Module boundaries and communication

Each module keeps feature-local `Entities`, `Data`, `Services`, and `Endpoints` folders inside the single API project. All services use the same scoped `FookbaseDbContext`; module ownership remains a code organization boundary, not a database boundary. User reports verify an Identity user exists, and Media verifies active profile-media references before deletion.

Cross-module coordination is explicit and synchronous. The coordinator remains inside the module
that owns the endpoint: `Modules/Identity/Services/RegistrationUseCase`,
`Modules/Posts/Services/PostsUseCase`, and
`Modules/Admin/Services/AdministrationUseCase`. `RegistrationUseCase` commits the
authentication account, refresh token, and user profile in one transaction. `PostsUseCase`
obtains the current Friends relationship snapshot for privacy checks and commits posts with Media
attachment references in one transaction. Profile image/reference changes use one transaction as
well. Messages uses `FriendsService` for direct-conversation access checks and SignalR at
`/hubs/messages` for client updates. Notifications is a feature-local service sharing the same
context: Friends and Posts queue the durable notification in their existing save operation, then
best-effort publish it to `/hubs/notifications` after commit. These are in-process C# calls, not
HTTP requests.

Feed is a read-only query/use-case module. It takes the current Friends relationship snapshot,
uses the same `PostVisibility` rules as direct post access, then batch-loads author, media and
engagement data for a cursor page. It owns no entity, table, cache or background fan-out.

Groups owns groups, membership roles, join requests, invites and rules. It shares the same
scoped context with Posts, Media and Notifications: group posts use the existing `Posts` table
through `Post.ContainerType`/`ContainerId`; `GroupPostAccessService` is the central group
membership/privacy check used by direct post, comments, reactions and media access. Group cover
references are held in the current Media lifecycle; invitations and approved private join requests
are durable Notifications published best-effort after commit.

## No messaging projections

There is no messaging layer, integration-event contract, outbox, inbox, event publisher, projection worker, or hosted outbox service. Posts reads current relationship data through `FriendsService`; Media remains the owner of attachment metadata and references. The direct calls are intentionally simple for this single-process application.

## Persistence

`fookbase_db` is the only runtime PostgreSQL database and `FookbaseDbContext` is the only
runtime EF Core context. It maps Identity (`AspNet*`, `RefreshTokens`), Users
(`UserProfiles`), Friends, Groups, Messages, Notifications, Posts, and Media tables without cosmetic table renames. Feed has no persistence table.
There are no active table-name collisions. The consolidated schema has one
`__EFMigrationsHistory` table and no Inbox/Outbox tables.

`Code/Persistence/Migrations/20260910143327_InitialFookbase.cs` initializes a complete fresh
database; `20260910154744_AddNotifications.cs` adds `Notifications` and
`CommentReactions`; `20260910162758_AddFeedPostIndex.cs` adds the partial active-post
keyset index used by Feed; `20260910165428_AddGroups.cs` adds Groups tables and backfills every
existing profile post with `ContainerType=Profile` and `ContainerId=AuthorUserId`. All later
migrations and the current snapshot remain in the same runtime migration directory. The migration
policy is documented in [migration-history.md](migration-history.md).

There are no outbox or inbox implementations in the running application. The only retained
durable workflow is `ObjectDeletions`, a Media table consumed by `ObjectDeletionWorker`; it
records retryable MinIO deletion work and is unrelated to inter-module coordination.

## Compatibility

All public routes remain under `/api/auth`, `/api/users`, `/api/friends`, `/api/feed`, `/api/groups`, `/api/messages`, `/api/notifications`, `/api/posts`, and `/api/media`, served by `http://localhost:5000`; SignalR hubs are served at `/hubs/messages` and `/hubs/notifications`. JWT and ASP.NET Core Identity are unchanged; endpoints continue to take the actor identifier from the JWT `sub` claim.

Media remains private in MinIO. Clients upload with presigned PUT URLs; PostsUseCase obtains signed read URLs through the in-process Media service, and ownership/reference/lifecycle/signature validation remain in Media.
