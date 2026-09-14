# Memories, Birthdays, and Profile Extras V1 Implementation Plan

> Cập nhật trạng thái 2026-09-15: chỉ các bước triển khai/tài liệu có mã nguồn hoặc tài liệu hiện hữu được đánh dấu; test và migration validation chưa được chạy trong phạm vi hiện tại.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Deliver private on-this-day memories, privacy-aware friend birthday views, and optional profile Intro fields.

**Architecture:** Keep profile persistence and birthday projection in the Users module, where current profile/block/friendship projections live. Add a small query-only Memories module that selects existing profile posts and sends them to PostsService.LoadResponsesAsync. The React app adds typed API clients and route pages that reuse PostCard and the current profile editor/layout.

**Tech Stack:** .NET 10 minimal APIs, EF Core/Npgsql/PostgreSQL, xUnit integration tests, React, TypeScript, Vite, Tailwind utility classes.

**Spec:** docs/superpowers/specs/2026-09-14-memories-birthdays-profile-extras-v1-design.md

## Global Constraints

- Use the application server's local timezone consistently for memory and birthday calendar dates.
- Store birthday data as DateOnly/PostgreSQL date; use Feb 28 for Feb 29 birthdays in non-leap years.
- Memories are owner-only projections of existing non-deleted Standard Profile posts in prior years; create no Memory entities/tables.
- Birthday results are limited to active current friends, enforce both-direction block exclusion, and expose no birth year/age to non-owners.
- Keep existing profile/block behavior, feed ranking, Search, Photos/Albums, Messenger, and notifications architecture unchanged.
- Website accepts only https/http; external browser links use rel="noopener noreferrer".
- Add only 4–6 high-value backend tests and run the full backend regression once at the end.
- Use small logical commits, push origin main, and never commit credentials, build outputs, or unrelated work.

---

## File structure

| Path | Responsibility |
| --- | --- |
| backend/.../Modules/Users/{Entities,Services,Endpoints,DTOs} | Profile extras, safe profile projection, birthday APIs. |
| backend/.../Modules/Memories/* | Read-only memory API, grouped contracts, service, DI. |
| backend/.../Persistence/Migrations/*AddMemoriesBirthdaysProfileExtrasV1* | Unified migration and model snapshot. |
| backend/.../Tests/Users/.../UserProfileEndpointsTests.cs | Birthday visibility, block, date-boundary, profile privacy tests. |
| backend/.../Tests/Posts/.../MemoryEndpointsTests.cs | Memory owner/deleted-content tests. |
| frontend/web/src/api/{users,memories}.ts | Typed profile/birthday/memory contracts. |
| frontend/web/src/pages/{memories,birthdays}/* | Route pages using existing layout/cards. |
| frontend/web/src/pages/profile/{ProfilePage,UserProfilePage}.tsx | Intro display and owner editing fields. |
| frontend/web/src/{routes,index,layout,pages/feed} | Routes, navigation, Home birthday widget. |
| docs/memories-birthdays-profile-v1.md | Product behavior and deferred scope. |

### Task 1: Complete profile data contract and viewer-safe projection

**Files:**
- Modify: backend/Fookbase.Src/Main/Code/Modules/Users/Entities/UserProfile.cs
- Modify: backend/Fookbase.Src/Main/Code/Modules/Users/Data/Configurations/UserProfileConfiguration.cs
- Modify: backend/Fookbase.Src/Main/Code/Modules/Users/DTOs/{Requests/UpdateUserProfileRequest.cs,Responses/UserProfileResponse.cs}
- Modify: backend/Fookbase.Src/Main/Code/Modules/Users/Services/UserProfileService.cs
- Test: backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/UserProfileEndpointsTests.cs

**Interfaces:** Produces BirthdayVisibility { OnlyMe, Friends, Public }, update fields Hometown, Workplace, Education, Website, and a profile response where dateOfBirth is owner-only and birthday is a safe month/day response.

- [ ] **Step 1: Write the failing profile-privacy integration test**

~~~csharp
[Fact]
public async Task Profile_hides_full_birth_date_from_non_owner_and_respects_visibility()
{
    // Seed target + viewer; set target DOB and Friends visibility.
    // Target sees DateOfBirth, friend sees Birthday but DateOfBirth is null.
    // A non-friend sees neither.
}
~~~

- [ ] **Step 2: Run test to verify it fails**

Run: dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Profile_hides_full_birth_date"

Expected: FAIL on the current unconditional DateOfBirth response.

- [x] **Step 3: Write minimal implementation**

~~~csharp
public enum BirthdayVisibility { OnlyMe, Friends, Public }
public sealed record BirthdayResponse(int Month, int Day);
// UserProfileResponse includes DateOnly? DateOfBirth and BirthdayResponse? Birthday.
// ToResponse(profile, viewerUserId) returns DateOfBirth only for the owner.
~~~

Add nullable profile extras, BirthdayVisibility.OnlyMe, configuration limits, trimmed update behavior, and validation: 100/100/150/150 limits, bounded HTTP(S) URL, and no future birth date using timeProvider.GetLocalNow().Date.

- [ ] **Step 4: Run test to verify it passes**

Run: dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~UserProfileEndpointsTests"

Expected: PASS.

- [ ] **Step 5: Commit**

~~~bash
git add backend/Fookbase.Src/Main/Code/Modules/Users backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/UserProfileEndpointsTests.cs
git commit -m "feat: bổ sung quyền riêng tư ngày sinh và thông tin hồ sơ"
git push origin main
~~~

### Task 2: Add privacy-filtered friend birthday APIs

**Files:**
- Create: backend/Fookbase.Src/Main/Code/Modules/Users/DTOs/Responses/BirthdayResponses.cs
- Modify: backend/Fookbase.Src/Main/Code/Modules/Users/Services/UserProfileService.cs
- Modify: backend/Fookbase.Src/Main/Code/Modules/Users/Endpoints/UserProfileEndpoints.cs
- Test: backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/UserProfileEndpointsTests.cs

**Interfaces:** Consumes BirthdayVisibility, active User, Friendship, and BlockedUser rows. Produces GET /api/birthdays/today and GET /api/birthdays/upcoming?days=7 returning BirthdayFriendResponse(Guid UserId, string DisplayName, string Username, string? AvatarUrl, int Month, int Day, string FriendshipState).

- [ ] **Step 1: Write failing birthday tests**

~~~csharp
[Fact]
public async Task Birthday_lists_include_visible_friends_but_exclude_blocks()
{
    // Seed a Friends-visible friend whose birthday is today and a blocked friend.
    // GET /api/birthdays/today contains only the allowed friend.
}

[Fact]
public async Task Upcoming_birthdays_cross_the_year_boundary_in_next_occurrence_order()
{
    // Use the occurrence helper with December/January DateOnly values.
    // Assert inside-window dates are ordered by their next occurrence.
}
~~~

- [ ] **Step 2: Run tests to verify endpoint-not-found failures**

Run: dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Birthday_"

Expected: FAIL with 404 or missing response contracts.

- [x] **Step 3: Write minimal implementation**

~~~csharp
public Task<IReadOnlyList<BirthdayFriendResponse>> GetTodaysBirthdaysAsync(Guid actor, CancellationToken ct);
public Task<ApplicationResult<IReadOnlyList<BirthdayFriendResponse>>> GetUpcomingBirthdaysAsync(Guid actor, int days, CancellationToken ct);

group.MapGet("/today", GetTodaysBirthdaysAsync).RequireAuthorization();
group.MapGet("/upcoming", GetUpcomingBirthdaysAsync).RequireAuthorization();
~~~

Start from Friendships containing the actor, join active Users and UserProfiles, exclude either BlockedUsers direction, then apply visibility. Calculate effective month/day with one shared Feb-29 helper and order by next occurrence; cap days at 30 and return invalid_days for values outside 1–30.

- [ ] **Step 4: Run tests to verify they pass**

Run: dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Birthday_"

Expected: PASS.

- [ ] **Step 5: Commit**

~~~bash
git add backend/Fookbase.Src/Main/Code/Modules/Users backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/UserProfileEndpointsTests.cs
git commit -m "feat: thêm danh sách sinh nhật bạn bè"
git push origin main
~~~

### Task 3: Add owner-only memory projection API

**Files:**
- Create: backend/Fookbase.Src/Main/Code/Modules/Memories/{DTOs/Responses/MemoryTodayResponse.cs,Services/MemoriesService.cs,Endpoints/MemoryEndpoints.cs,DependencyInjection.cs}
- Modify: backend/Fookbase.Src/Main/Program.cs
- Create: backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/MemoryEndpointsTests.cs

**Interfaces:** Consumes FookbaseDbContext.Posts, PostsService.LoadResponsesAsync, TimeProvider and token subject. Produces authenticated GET /api/memories/today returning MemoryTodayResponse(string Date, IReadOnlyList<MemoryYearResponse> Years), where MemoryYearResponse(int Year, int YearsAgo, IReadOnlyList<PostResponse> Items).

- [ ] **Step 1: Write failing owner and deletion tests**

~~~csharp
[Fact]
public async Task Today_returns_only_the_actor_prior_year_profile_standard_posts()
{
    // Seed same month/day prior-year posts for actor and another user plus
    // current-year/reel/group cases; only actor's eligible post is returned.
}

[Fact]
public async Task Today_omits_deleted_historical_posts()
{
    // Soft-delete an otherwise eligible actor post and assert its id is absent.
}
~~~

- [ ] **Step 2: Run tests to verify endpoint-not-found failures**

Run: dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj --filter "FullyQualifiedName~MemoryEndpointsTests"

Expected: FAIL with 404.

- [x] **Step 3: Write minimal implementation**

~~~csharp
public Task<MemoryTodayResponse> GetTodayAsync(Guid ownerUserId, CancellationToken ct = default);

var posts = await dbContext.Posts.AsNoTracking()
    .Where(post => post.AuthorUserId == ownerUserId &&
        post.ContainerType == PostContainerType.Profile &&
        post.PostType == PostType.Standard &&
        post.DeletedAtUtc == null && post.CreatedAtUtc.Year < today.Year)
    .OrderByDescending(post => post.CreatedAtUtc).Take(MaximumItems).ToListAsync(ct);
~~~

Apply same server-local month/day matching in the translated query path, group by original local year, and pass rows into PostsService.LoadResponsesAsync(posts, ownerUserId, ct). Do not add an author parameter to the endpoint and do not create persistence.

- [ ] **Step 4: Run tests to verify they pass**

Run: dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj --filter "FullyQualifiedName~MemoryEndpointsTests"

Expected: PASS.

- [ ] **Step 5: Commit**

~~~bash
git add backend/Fookbase.Src/Main/Code/Modules/Memories backend/Fookbase.Src/Main/Program.cs backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/MemoryEndpointsTests.cs
git commit -m "feat: thêm kỷ niệm ngày này năm xưa"
git push origin main
~~~

### Task 4: Generate and verify the unified schema migration

**Files:**
- Create: backend/Fookbase.Src/Main/Code/Persistence/Migrations/*_AddMemoriesBirthdaysProfileExtrasV1.cs
- Create: backend/Fookbase.Src/Main/Code/Persistence/Migrations/*_AddMemoriesBirthdaysProfileExtrasV1.Designer.cs
- Modify: backend/Fookbase.Src/Main/Code/Persistence/Migrations/FookbaseDbContextModelSnapshot.cs

**Interfaces:** Consumes completed UserProfile EF configuration. Produces the only M15 schema change: BirthdayVisibility, Hometown, Workplace, Education, and Website, with OnlyMe default and existing DateOfBirth/CurrentCity left intact.

- [ ] **Step 1: Verify the model reports pending changes**

Run: dotnet ef migrations has-pending-model-changes --project backend/Fookbase.Src/Main/Fookbase.Api.csproj --startup-project backend/Fookbase.Src/Main/Fookbase.Api.csproj

Expected: reports Task 1 changes.

- [ ] **Step 2: Generate the normal EF migration**

~~~bash
dotnet ef migrations add AddMemoriesBirthdaysProfileExtrasV1 \
  --project backend/Fookbase.Src/Main/Fookbase.Api.csproj \
  --startup-project backend/Fookbase.Src/Main/Fookbase.Api.csproj \
  --output-dir Code/Persistence/Migrations
~~~

- [ ] **Step 3: Apply migration through the focused integration test**

Run: dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Profile_hides_full_birth_date"

Expected: PASS.

- [ ] **Step 4: Confirm no model changes remain**

Run: dotnet ef migrations has-pending-model-changes --project backend/Fookbase.Src/Main/Fookbase.Api.csproj --startup-project backend/Fookbase.Src/Main/Fookbase.Api.csproj

Expected: exit 0 with no pending changes.

- [ ] **Step 5: Commit**

~~~bash
git add backend/Fookbase.Src/Main/Code/Persistence/Migrations
git commit -m "chore: thêm migration profile extras v1"
git push origin main
~~~

### Task 5: Add typed web clients, routes, and navigation

**Files:**
- Create: frontend/web/src/api/memories.ts
- Modify: frontend/web/src/api/users.ts
- Modify: frontend/web/src/routes/index.tsx
- Modify: frontend/web/src/layout/Sidebar.tsx
- Create: frontend/web/src/pages/memories/MemoriesPage.tsx
- Create: frontend/web/src/pages/birthdays/BirthdaysPage.tsx

**Interfaces:** Consumes memory, post, birthday friend, and UserProfile JSON contracts. Produces /memories and /birthdays, with memoriesApi.getToday(), birthdaysApi.getToday(), and birthdaysApi.getUpcoming(days?: number).

- [x] **Step 1: Add type-first client contracts**

~~~ts
export interface MemoryYear { year: number; yearsAgo: number; items: Post[] }
export interface MemoryToday { date: string; years: MemoryYear[] }
export const memoriesApi = { getToday: () => apiRequest<MemoryToday>('/api/memories/today') }
export const birthdaysApi = {
  getToday: () => apiRequest<BirthdayFriend[]>('/api/birthdays/today'),
  getUpcoming: (days = 7) => apiRequest<BirthdayFriend[]>('/api/birthdays/upcoming?days=' + days),
}
~~~

- [x] **Step 2: Implement route pages with existing UI components**

~~~tsx
// MemoriesPage maps each MemoryYear to a heading and <PostCard post={post} />.
// BirthdaysPage fetches today/upcoming in one effect and renders loading,
// error, and empty states with profile links; no greeting system is created.
~~~

Add lazy routes and two sidebar links. Link birthday users to /profile/:userId and use existing Messenger navigation only when it supports direct conversation.

- [ ] **Step 3: Build and lint**

Run: npm run build && npm run lint

Workdir: frontend/web

Expected: PASS.

- [ ] **Step 4: Commit**

~~~bash
git add frontend/web/src/api frontend/web/src/routes/index.tsx frontend/web/src/layout/Sidebar.tsx frontend/web/src/pages/memories frontend/web/src/pages/birthdays
git commit -m "feat: thêm trang kỷ niệm và sinh nhật"
git push origin main
~~~

### Task 6: Extend profile Intro/editor and add a bounded Home birthday widget

**Files:**
- Modify: frontend/web/src/api/users.ts
- Modify: frontend/web/src/pages/profile/ProfilePage.tsx
- Modify: frontend/web/src/pages/profile/UserProfilePage.tsx
- Modify: frontend/web/src/pages/feed/FeedPage.tsx

**Interfaces:** Consumes profile birthday, birthdayVisibility, hometown, workplace, education, website and birthdaysApi.getToday(). Produces owner-only fields in the existing edit form, viewer-safe Intro rendering, and a small Home summary link.

- [x] **Step 1: Extend existing profile draft and save payload**

~~~tsx
const [birthdayDraft, setBirthdayDraft] = useState('')
const [birthdayVisibilityDraft, setBirthdayVisibilityDraft] = useState<BirthdayVisibility>('onlyMe')
// Include dateOfBirth, birthdayVisibility, hometown, workplace, education, website
// in usersApi.updateCurrent(...) from the existing save handler.
~~~

Use input type=date, select, and ordinary text/URL controls with labels; retain existing image fields and profile form behavior.

- [x] **Step 2: Render conditional Intro section**

~~~tsx
{profile.website && <a href={profile.website} target="_blank" rel="noopener noreferrer">{profile.website}</a>}
{profile.currentCity && <p>Lives in {profile.currentCity}</p>}
~~~

Use only viewer-safe birthday presentation, never calculate/render age. Preserve block behavior by rendering only the loaded profile response.

- [x] **Step 3: Add Home widget and verify web gates**

~~~tsx
// FeedPage loads birthdaysApi.getToday() once; render a compact link only when count > 0.
<Link to="/birthdays">{items.length} bạn có sinh nhật hôm nay</Link>
~~~

Run: npm run build && npm run lint

Workdir: frontend/web

Expected: PASS.

- [ ] **Step 4: Commit**

~~~bash
git add frontend/web/src/api/users.ts frontend/web/src/pages/profile frontend/web/src/pages/feed/FeedPage.tsx
git commit -m "feat: thêm intro hồ sơ và tiện ích sinh nhật"
git push origin main
~~~

### Task 7: Document behavior and run the single milestone regression/production validation

**Files:**
- Create: docs/memories-birthdays-profile-v1.md

**Interfaces:** Documents endpoint behavior, owner-only memory rule, birthday visibility, Feb 29=Feb 28 behavior, date-window cap, profile extras, and deferred scope.

- [x] **Step 1: Write concise product documentation**

~~~markdown
## Leap-day policy

Birthdays on 29 February appear on 28 February in non-leap years.

## Deferred

No automatic notifications, birthday greetings, custom audiences, or memory copies are created in V1.
~~~

- [ ] **Step 2: Run focused M15 tests once more**

Run: dotnet test backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --filter "FullyQualifiedName~(Birthday_|Profile_hides_full_birth_date)" && dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj --filter "FullyQualifiedName~MemoryEndpointsTests"

Expected: PASS.

- [ ] **Step 3: Run established final validation once**

Run the repository's documented backend regression workflow once, then dotnet restore, dotnet build, pending-model check, fresh migration/legacy import checks when available, production Docker build, and /health/live plus /health/ready. Run npm run build && npm run lint in each of frontend/web, frontend/messenger, and frontend/admin.

Expected: all applicable commands pass; document unrelated pre-existing infrastructure failures without broadening M15.

- [ ] **Step 4: Commit**

~~~bash
git add docs/memories-birthdays-profile-v1.md
git commit -m "docs: bổ sung hướng dẫn memories birthdays v1"
git push origin main
git status --short --branch
~~~
