# User Follow + Friend Suggestions V1 Implementation Plan

> Cập nhật trạng thái 2026-09-15: chỉ các bước triển khai có mã nguồn hiện hữu được đánh dấu; test, migration validation và smoke-test chưa được chạy trong phạm vi hiện tại.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a safe directional user-follow graph, follow-aware feeds, and deterministic People You May Know to the existing Fookbase modular monolith.

**Architecture:** Keep social graph mutations and reads in `Modules/Friends` using the unified `FookbaseDbContext`. Feed will take follow IDs in the already revalidated relationship snapshot; suggestion ranking will be bounded, SQL-projected, deterministic, and cursor-protected. Profile/search projections and the web UI consume these APIs without merging friendship and following semantics.

**Tech Stack:** ASP.NET Core Minimal APIs, EF Core/Npgsql/PostgreSQL, ASP.NET Core Data Protection, xUnit integration tests, React/TypeScript/Vite.

**Spec:** `docs/superpowers/specs/2026-09-13-user-follow-friend-suggestions-v1-design.md`

## Global Constraints

- Preserve the one ASP.NET Core monolith, `FookbaseDbContext`, and PostgreSQL `fookbase_db`.
- Do not add a new database, DbContext, broker, MediatR, generic repository, graph engine, or broad refactor.
- Never stage, modify, or commit the pre-existing `README.md` change.
- Keep existing cursor Data Protection purposes stable; new cursors must be versioned, opaque, and viewer-bound.
- Unfriend retains existing follows; block atomically removes both directed follows; unblock restores none.
- Friendship acceptance atomically creates both directed follows but creates no `UserFollowed` notification.
- Run the relevant test/check cycle before every focused Vietnamese Conventional Commit. Push only after full validation succeeds.

---

## File Structure

- `Modules/Friends/Entities/UserFollow.cs` and `Data/Configurations/UserFollowConfiguration.cs`: directed graph edge and database indexes.
- `Modules/Friends/Services/FriendsService.cs`: pair-locked follow mutations, mutual automatic follows, block cleanup, and relationship snapshot.
- `Modules/Friends/Services/FriendSuggestionService.cs` and `Config/FriendSuggestionOptions.cs`: bounded candidate query, score projection, and protected cursor.
- `Modules/Friends/DTOs/Responses/*`: safe follow list, suggestion, and cursor page contracts.
- `Modules/Friends/Endpoints/FriendEndpoints.cs` and `Modules/Users/Endpoints/UserProfileEndpoints.cs`: authenticated friend-suggestion and user-follow routes.
- `Modules/Users/*` and `Modules/Search/*`: batched viewer-state/count profile and people projections.
- `Modules/Feed/*`: follow-aware profile/Reel candidate eligibility and ranking weight.
- `Code/Persistence/Migrations/*`: normal unified migration and model snapshot update with friendship backfill.
- `frontend/web/src/api/{friends,users,notifications}.ts`, profile/search/friends UI, and preferences: new API contracts and controls.
- Friends, Users, and Posts integration test projects: regression coverage for graph, cursor, feed, notification, and migration behavior.

### Task 1: Close M11 runtime validation gaps before feature code

**Files:**
- Inspect: `compose.yml`, `docs/deployment.md`, `docs/social-interactions-v1.md`, `frontend/web/package.json`, `frontend/messenger/package.json`, `frontend/admin/package.json`
- Test: disposable compose environment and browser-build commands; no source edit unless a warning is application code

**Produces:** A recorded real-cursor Data Protection recreation result and an exact classification of the two React warnings before M12 implementation begins.

- [ ] **Step 1: Start the documented disposable compose stack with an isolated project name and wait for readiness**

Run: `docker compose -p fookbase-m11-smoke up -d --build && until curl -fsS http://localhost:5000/health/ready; do sleep 2; done`

Expected: API, PostgreSQL, and MinIO are healthy; the configured `data-protection-keys` volume is present.

- [ ] **Step 2: Authenticate a disposable user and obtain a real protected cursor**

Use the existing registration/login API, create enough saved, hashtag, search, or feed data to receive `nextCursor`, and save only the opaque cursor value in the shell session (never log or write key files).

Expected: a non-empty protected cursor returned by an authenticated endpoint.

- [ ] **Step 3: Recreate only the API container and reuse the cursor**

Run: `docker compose -p fookbase-m11-smoke up -d --force-recreate api` and repeat the original cursor request after `/health/ready` succeeds.

Expected: old cursor returns a normal page, not a cursor/protection error; a fresh authenticated request also succeeds.

- [ ] **Step 4: Audit each web warning at its origin**

Run the existing web lint/build commands, capture the two warnings, and use source locations/stack traces to determine whether each originates in Fookbase code or a dependency.

Expected: project-code warnings receive a narrow fix in the task that owns the file; harmless dependency warnings are documented for the final report.

- [ ] **Step 5: Tear down only the disposable smoke project**

Run: `docker compose -p fookbase-m11-smoke down -v`

Expected: no production-like data, key material, or untracked artifacts remain.

### Task 2: Add the directed follow model, unified migration, and friendship backfill

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Friends/Entities/UserFollow.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Friends/Data/Configurations/UserFollowConfiguration.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Persistence/FookbaseDbContext.cs`
- Create: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/<timestamp>_AddUserFollowV1.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Persistence/Migrations/FookbaseDbContextModelSnapshot.cs`
- Test: `backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests/FriendEndpointsTests.cs`

**Interfaces:**
- Produces `UserFollow.Create(Guid followerUserId, Guid followingUserId, DateTimeOffset followedAtUtc)` and `DbSet<UserFollow> UserFollows`.
- Produces unique `(FollowerUserId, FollowingUserId)`, reverse `(FollowingUserId, FollowerUserId)`, and list-keyset indexes.

- [ ] **Step 1: Write migration/backfill expectations in a failing integration test**

Add a test that inserts an existing normalized `Friendship`, applies the migration/fresh database path used by the suite, and asserts exactly `A→B` and `B→A` follows. Also assert a repeated migration/backfill does not duplicate them.

- [ ] **Step 2: Run the focused test to verify the model/table is absent**

Run: `dotnet test backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests --filter "FullyQualifiedName~Friendship_backfill"`

Expected: FAIL because `UserFollows` and its migration do not exist.

- [x] **Step 3: Implement the entity and configuration**

Use private construction and a static factory matching `BlockedUser`; map both GUIDs and `FollowedAtUtc`, reject equal IDs in the factory, and add the required indexes. Add the DbSet to `FookbaseDbContext`.

- [ ] **Step 4: Generate the normal unified migration and add conflict-safe friendship SQL**

Run EF migrations from `backend/Fookbase.Src/Main`; amend generated `Up` with `INSERT ... SELECT ... ON CONFLICT DO NOTHING` in both friendship directions. Do not edit old module migrations.

- [ ] **Step 5: Run focused migration/model checks**

Run: `dotnet test backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests --filter "FullyQualifiedName~Friendship_backfill"` and `dotnet ef migrations has-pending-model-changes --project Fookbase.Api.csproj`

Expected: PASS and no pending model changes.

- [ ] **Step 6: Commit the model and migration**

Run: `git add backend/Fookbase.Src/Main/Code/Modules/Friends/Entities/UserFollow.cs backend/Fookbase.Src/Main/Code/Modules/Friends/Data/Configurations/UserFollowConfiguration.cs backend/Fookbase.Src/Main/Code/Persistence && git commit -m "feat: thêm mô hình theo dõi người dùng"`

### Task 3: Implement atomic follow lifecycle, API lists, and notifications

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/Services/FriendsService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/Endpoints/FriendEndpoints.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Friends/DTOs/Responses/UserFollowResponse.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Friends/DTOs/Responses/CursorPageResponse.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Notifications/Entities/Notification.cs`
- Modify: notification response/UI contract files that serialize `NotificationType`
- Test: `backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests/FriendEndpointsTests.cs`

**Interfaces:**
- Produces `FollowAsync`, `UnfollowAsync`, `GetFollowersAsync`, and `GetFollowingAsync` on `FriendsService`.
- Produces authenticated `POST|DELETE /api/users/{userId}/follow` and `GET /api/users/{userId}/{followers|following}?cursor=&limit=`.
- Produces `NotificationType.UserFollowed` only for a newly created manual non-friend follow.

- [ ] **Step 1: Write failing endpoint tests**

Cover one-way manual follow, repeated follow/unfollow idempotency, self rejection, nonexistent/ineligible target rejection, both-direction block rejection, follower/following cursor traversal, invalid cursor `400`, hidden blocked relation, counts, manual notification, accepted-friend mutual follows, and no friendship-follow notification.

- [ ] **Step 2: Run the focused follow tests**

Run: `dotnet test backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests --filter "FullyQualifiedName~Follow"`

Expected: FAIL because API/service contracts do not exist.

- [x] **Step 3: Implement mutations within the current pair lock and transaction**

Validate target profile/account eligibility and bidirectional blocks. Insert only if absent; delete only matching direction if present. In `AcceptRequestCoreAsync`, add both follows before `SaveChangesAsync`. In `BlockCoreAsync`, delete both directed pair rows before the transaction commits. Leave `UnfriendCoreAsync` follow rows untouched.

- [x] **Step 4: Implement safe cursor projections and endpoint handlers**

Use a Data Protection purpose including cursor version, viewer ID, list direction, and target ID. Order by descending follow time then GUID. Join/profile-project safe fields in one query, filter block relations in both directions, and return no private Identity fields.

- [x] **Step 5: Implement general notification policy**

Add `UserFollowed` to the normal notification enum and mappings. Queue/publish only when a manual insert succeeds and actors are distinct and not friends; do not route automatic friendship rows through it.

- [ ] **Step 6: Run focused graph tests**

Run: `dotnet test backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests --filter "FullyQualifiedName~Follow|FullyQualifiedName~Block|FullyQualifiedName~Accept"`

Expected: PASS, including block cleanup and unblock non-restoration.

- [ ] **Step 7: Commit the follow lifecycle**

Run: `git add backend/Fookbase.Src/Main/Code/Modules/Friends backend/Fookbase.Src/Main/Code/Modules/Notifications backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests && git commit -m "feat: thêm API theo dõi người dùng"`

### Task 4: Add batched profile and people-search follow state/count projections

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Users/DTOs/Responses/UserProfileResponse.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Users/Services/UserProfileService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Users/Endpoints/UserProfileEndpoints.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Search/DTOs/Responses/SearchResponses.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Search/Services/SearchService.cs`
- Test: `backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/UserProfileEndpointsTests.cs`

**Interfaces:**
- Extends safe user/profile/person contracts with `FollowerCount`, `FollowingCount`, `IsFollowing`, and `IsFollowedBy` where a viewer is authenticated.
- Keeps anonymous profile/search compatibility by returning nullable/false viewer-state rather than requiring JWT.

- [ ] **Step 1: Write failing profile/search tests**

Assert a viewer sees both directed states and SQL-derived counts, a blocked relation does not contribute, and a people global-search item reports follow/friend state without changing search ordering.

- [ ] **Step 2: Run focused profile/search tests**

Run: `dotnet test FookbaseLight.sln --filter "FullyQualifiedName~UserProfileEndpointsTests|FullyQualifiedName~Search"`

Expected: FAIL because response fields and authenticated viewer projection are absent.

- [x] **Step 3: Add optional JWT viewer extraction and query projections**

Pass optional viewer ID through profile/search read paths. Build counts with correlated SQL `Count` and state with `Any`; project list pages as a set, never issue one follow query per result. Reuse existing block visibility rules.

- [ ] **Step 4: Run focused projection tests and inspect generated logs for N+1 patterns**

Run: `dotnet test FookbaseLight.sln --filter "FullyQualifiedName~UserProfileEndpointsTests|FullyQualifiedName~GlobalSearch"`

Expected: PASS; each endpoint produces bounded query count independent of returned people rows.

- [ ] **Step 5: Commit profile/search integration**

Run: `git add backend/Fookbase.Src/Main/Code/Modules/{Users,Search} backend/Fookbase.Src/Tests && git commit -m "feat: hiển thị trạng thái theo dõi trên hồ sơ"`

### Task 5: Make organic feed eligibility follow-aware

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/Services/FriendsService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/Common/RelationshipAccessSnapshot.cs` (or its current declaration)
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Feed/Config/FeedRankingOptions.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Feed/Services/FeedService.cs`
- Modify: `backend/Fookbase.Src/Main/appsettings.json` if ranking options are explicitly configured
- Test: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/MixedFeedEndpointsTests.cs`

**Interfaces:**
- Extends `RelationshipAccessSnapshot` with `FollowedUserIds`.
- Adds `FeedRankingOptions.FollowedNonFriendProfile` and preserves stable rank hashing/cursor semantics.

- [ ] **Step 1: Write failing mixed-feed tests**

Cover followed friend profile content, unfollowed friend disappearance in a new session, followed non-friend Public inclusion, Friends-only exclusion, followed non-friend organic Reel inclusion, old-cursor authorization recheck, Following mode follow relation, and no suggested Reel in Following mode.

- [ ] **Step 2: Run focused feed tests**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter "FullyQualifiedName~MixedFeed"`

Expected: FAIL under friend-only profile eligibility.

- [x] **Step 3: Load follow IDs in the relationship snapshot and separate profile candidate sets**

Query followed users once with blocks; feed profile candidates become self, followed friends, and followed non-friends. Apply Friends privacy only to the friend subset and Public only to the non-friend subset. Do not change group/page/share discovery rules except their existing block filters.

- [x] **Step 4: Rank sources without changing cursor stability**

Add source windows for followed friends at `FriendAffinity` and non-friends at `FollowedNonFriendProfile`; use score zero in Following mode. Preserve `AsOfUtc`, ranking-version purpose, lookahead, and per-request relationship rebuild.

- [ ] **Step 5: Run feed regression tests**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter "FullyQualifiedName~MixedFeed"`

Expected: PASS, including old cursor and reel distinctions.

- [ ] **Step 6: Commit feed eligibility**

Run: `git add backend/Fookbase.Src/Main/Code/Modules/{Friends,Feed} backend/Fookbase.Src/Main/appsettings.json backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests && git commit -m "feat: áp dụng theo dõi cho bảng tin"`

### Task 6: Build deterministic, privacy-safe Friend Suggestions API

**Files:**
- Create: `backend/Fookbase.Src/Main/Code/Modules/Friends/Config/FriendSuggestionOptions.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Friends/Services/FriendSuggestionService.cs`
- Create: `backend/Fookbase.Src/Main/Code/Modules/Friends/DTOs/Responses/FriendSuggestionResponse.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/DependencyInjection.cs`
- Modify: `backend/Fookbase.Src/Main/Code/ModuleServiceCollectionExtensions.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/Endpoints/FriendEndpoints.cs`
- Modify: `backend/Fookbase.Src/Main/appsettings.json`
- Test: `backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests/FriendEndpointsTests.cs`

**Interfaces:**
- Produces `GET /api/friends/suggestions?cursor=&limit=` with default 20 and maximum 50.
- Produces `FriendSuggestionResponse(SafeProfile, MutualFriendCount, SharedGroupCount, SharedPageCount, RelationshipStatus, IsFollowing)`.

- [ ] **Step 1: Write failing suggestion tests**

Create a graph proving friend-of-friend, mutual count, shared Group weight, and shared Page weight. Assert self/friends/pending both directions/blocks are excluded, equal scores use user ID order, unchanged graph cursor pages stably, and malformed or viewer-mismatched cursor returns `400`.

- [ ] **Step 2: Run focused suggestion tests**

Run: `dotnet test backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests --filter "FullyQualifiedName~Suggestion"`

Expected: FAIL because endpoint/service/options do not exist.

- [x] **Step 3: Implement bounded candidate source queries and exclusion predicate**

Generate candidates via two-hop friendship query and active shared group/page membership, each bounded by options. Union IDs in SQL, apply all exclusions before scoring, and join `UserProfiles` only after eligibility. Never materialize all users or names of groups/pages.

- [x] **Step 4: Implement configurable deterministic score and opaque cursor**

Compute `mutual * MutualFriendWeight + groups * SharedGroupWeight + pages * SharedPageWeight`; order score descending then candidate GUID. Protect cursor JSON `{version, viewerId, score, userId}` with a purpose unique to suggestions and validate its viewer/version on decode.

- [x] **Step 5: Register service/options and map the endpoint**

Bind/validate `FriendSuggestionOptions` in the existing module registration style; use the same JWT subject parsing/error handling as other Friends endpoints.

- [ ] **Step 6: Run focused API and query-count tests**

Run: `dotnet test backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests --filter "FullyQualifiedName~Suggestion"`

Expected: PASS with no loop that issues a query per candidate.

- [ ] **Step 7: Commit suggestions backend**

Run: `git add backend/Fookbase.Src/Main/Code/Modules/Friends backend/Fookbase.Src/Main/Code/ModuleServiceCollectionExtensions.cs backend/Fookbase.Src/Main/appsettings.json backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests && git commit -m "feat: thêm gợi ý kết bạn v1"`

### Task 7: Integrate follow, suggestions, and notifications in the web client

**Files:**
- Modify: `frontend/web/src/api/friends.ts`
- Modify: `frontend/web/src/api/users.ts`
- Modify: `frontend/web/src/api/notifications.ts`
- Modify: `frontend/web/src/pages/profile/UserProfilePage.tsx`
- Modify: `frontend/web/src/pages/profile/ProfilePage.tsx`
- Modify: `frontend/web/src/pages/search/SearchPage.tsx`
- Modify: `frontend/web/src/layout/TopNavbar.tsx`
- Modify: `frontend/web/src/shared/components/NotificationModal.tsx`
- Modify: `frontend/web/src/preferences/PreferencesProvider.tsx`

**Interfaces:**
- Adds TypeScript contracts for cursor pages, follows, suggestion cards, profile counts/states, and `UserFollowed`.
- Adds local-only suggestion dismissal and reuses `friendsApi.sendRequest` for Add Friend.

- [x] **Step 1: Extend typed API clients before rendering new controls**

Add `follow`, `unfollow`, follower/following cursor readers, and `getSuggestions`. Extend profile and notification unions to mirror server responses exactly; retain existing Friends offset API contracts unchanged.

- [x] **Step 2: Add separate follow controls and counts to the viewed profile**

Keep the existing friend-request status switch intact. Render a distinct Follow/Following button, optimistic only after a successful mutation, and refresh the profile state/counts. Do not label a friend automatically as “Following” unless the profile contract says so.

- [x] **Step 3: Add People You May Know to the Friends profile tab**

Load one bounded suggestion cursor page with existing relationship data. Render avatar/name, mutual count, Add Friend, optional Follow, and Remove; Remove changes component state only. After Add Friend, remove the card locally and rely on backend pending-request exclusion after refresh.

- [x] **Step 4: Update search and notification display mappings**

Show non-invasive person relationship/follow state in existing search rows, and map `UserFollowed` to a localized notification label/action without changing search order or notification pagination.

- [ ] **Step 5: Fix only confirmed web React warnings**

If Task 1 located application-code warning sources, make minimal dependency-array/key/render fixes in those named files. Do not alter third-party code or globally suppress warnings.

- [ ] **Step 6: Run web validation**

Run: `npm ci && npm run build && npm run lint` in `frontend/web`.

Expected: build/lint pass; warning audit outcome is documented.

- [ ] **Step 7: Commit frontend integration**

Run: `git add frontend/web/src && git commit -m "feat: thêm theo dõi và gợi ý bạn bè trên web"`

### Task 8: Run full regression, migration, Docker, and deployment validation

**Files:**
- Modify if needed: `docs/social-interactions-v1.md` or `docs/deployment.md` only to record the executed recreate procedure and warning classification
- Test: all backend suites, three frontends, fresh migration, legacy import, Docker, compose health, Data Protection recreation

**Produces:** Evidence that M12 is buildable, migration-safe, health-ready, cursor-compatible across API recreation, and free of unintended README staging.

- [ ] **Step 1: Run backend restore/build and all integration suites**

Run: `dotnet restore FookbaseLight.sln && dotnet build FookbaseLight.sln --no-restore && dotnet test FookbaseLight.sln --no-build`

Expected: all projects compile and all tests pass.

- [ ] **Step 2: Verify EF model and both database paths**

Run: `dotnet ef migrations has-pending-model-changes --project backend/Fookbase.Src/Main/Fookbase.Api.csproj`; then execute the repository fresh-database migration and legacy-import E2E scripts/tests.

Expected: no pending model changes; both paths finish with friendship-derived follow rows.

- [ ] **Step 3: Validate every frontend package**

Run `npm ci && npm run build && npm run lint` separately in `frontend/web`, `frontend/messenger`, and `frontend/admin`.

Expected: all builds/lints pass; only documented non-application warnings remain, if any.

- [ ] **Step 4: Build and smoke-test production compose**

Run: `docker build -t fookbase-light-m12 -f backend/Fookbase.Src/Main/Dockerfile . && docker compose config --quiet`; start the isolated compose project and run `curl -fsS http://localhost:5000/health/live` and `/health/ready`.

Expected: image builds, compose is valid, and both health endpoints return success.

- [ ] **Step 5: Repeat the real protected-cursor API recreation test after M12**

Use the Task 1 procedure against the final image and preserve the configured key-ring volume. Verify the saved cursor works after API recreation and a normal authenticated request remains healthy.

- [ ] **Step 6: Update narrowly scoped operational documentation if validation revealed a missing reproducible command**

Document the exact non-secret procedure and final React warning classification. Do not edit README.

- [ ] **Step 7: Commit final documentation only if it changed**

Run: `git add docs/social-interactions-v1.md docs/deployment.md && git commit -m "docs: bổ sung kiểm tra triển khai theo dõi v1"`

- [ ] **Step 8: Confirm clean scoped diff and push after all validation**

Run: `git status --short`, ensure `README.md` is the sole pre-existing unstaged file, then `git push origin main`.

Expected: all M12 commits push successfully while README remains local and unstaged.
