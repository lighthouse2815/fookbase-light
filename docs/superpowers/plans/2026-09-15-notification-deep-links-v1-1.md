# Notification Deep Links V1.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all notification types render correctly and open a precise, authorized destination for posts, comments, events, and account warnings.

**Architecture:** Keep `Notification` persistence unchanged. `NotificationService` enriches only safe outgoing `NotificationResponse` values with a nullable parent target resolved in two bounded batch queries. The web client gains a single typed presentation resolver and a `/posts/:postId` page that reuses `LivePostCard` and the existing post API.

**Tech Stack:** .NET 10 minimal APIs, EF Core, xUnit integration tests, React, TypeScript, React Router, Vite, Tailwind.

**Spec:** `docs/superpowers/specs/2026-09-15-notification-deep-links-v1-1-design.md`

## Global Constraints

- Do not add a Notification column, EF migration, or database schema change.
- Preserve `NotificationService.CanSurfaceAsync` and all existing post/comment/event/block authorization behavior as the authority for access.
- `ParentEntityId` is a safe response-only projection; it must not include moderator, reporter, or internal-note data.
- Frontend notification unions must include every current `NotificationType` and `NotificationEntityType` value; do not hide missed cases with `any` or a generic switch fallback.
- Both notification surfaces must use one presentation/destination helper.
- `/posts/:postId` must reuse `postsApi.getById` and `LivePostCard`; it must show `Nội dung này không còn khả dụng.` for 403/404.
- Keep localStorage refresh-token migration out of this work.
- Commit each completed task with a Vietnamese Conventional Commit message and push to `origin/main`.

---

## File structure

```text
backend/Fookbase.Src/Main/Code/
  Modules/Notifications/
    DTOs/Responses/NotificationPageResponse.cs      # adds response-only ParentEntityId
    Services/NotificationService.cs                 # batch-resolves Comment/Post and EventInvite/Event parents
backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/
  PostEndpointsTests.cs                             # asserts Comment notification parent projection
  EventEndpointsTests.cs                            # asserts EventInvite notification parent projection
frontend/web/src/
  api/notifications.ts                              # exhaustive notification unions + parentEntityId
  shared/notificationPresentation.ts                # typed Vietnamese copy, badge and route resolver
  pages/notifications/NotificationsPage.tsx         # consumes shared presentation resolver
  layout/TopNavbar.tsx                              # consumes shared presentation resolver
  pages/posts/PostDetailPage.tsx                    # existing-post API view and unavailable state
  pages/feed/components/LivePostCard.tsx            # opens discussion for a linked Comment target
  pages/feed/components/PostDiscussion.tsx          # anchors the target Comment element
  routes/index.tsx                                  # registers /posts/:postId
```

### Task 1: Add safe parent targets to notification responses

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Notifications/DTOs/Responses/NotificationPageResponse.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Notifications/Services/NotificationService.cs`
- Modify: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PostEndpointsTests.cs`
- Modify: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/EventEndpointsTests.cs`

**Interfaces:**
- Consumes: persisted `Notification.EntityType`, `EntityId`, `Comments(PostId)`, `EventInvitations(EventId)`, and `CanSurfaceAsync`.
- Produces: `NotificationResponse(..., Guid? ParentEntityId, ...)`, where Comment maps to its `PostId`, EventInvite maps to its `EventId`, and all other notification types return `null`.

- [ ] **Step 1: Write a failing Comment-notification projection test**

Add this test beside `Comments_and_comment_reactions_create_general_notifications` in `PostEndpointsTests.cs`:

```csharp
[Fact]
public async Task Comment_reaction_notification_includes_its_parent_post_id()
{
    var users = await CreateUserIdsAsync(3);
    using var author = CreateAuthenticatedClient(users[0]);
    using var commenter = CreateAuthenticatedClient(users[1]);
    using var reactor = CreateAuthenticatedClient(users[2]);
    var post = await CreatePostAsync(author, "notification comment target", "public");
    var comment = await ReadAsync<CommentResponse>(await commenter.PostAsJsonAsync(
        $"/api/posts/{post.Id}/comments", new { content = "comment", parentCommentId = (Guid?)null }));

    (await reactor.PutAsJsonAsync($"/api/posts/comments/{comment.Id}/reaction", new { type = "love" }))
        .EnsureSuccessStatusCode();
    var notifications = await ReadAsync<NotificationPageResponse>(
        await commenter.GetAsync("/api/notifications"));

    var notification = Assert.Single(notifications.Items.Where(item => item.Type == "CommentReaction"));
    Assert.Equal(comment.Id, notification.EntityId);
    Assert.Equal(post.Id, notification.ParentEntityId);
}
```

- [ ] **Step 2: Write a failing EventInvite projection test**

Add `using Fookbase.Api.Modules.Notifications.DTOs.Responses;` to `EventEndpointsTests.cs`, then add:

```csharp
[Fact]
public async Task Event_invite_notification_includes_its_event_id()
{
    var users = await CreateUsersAsync(2);
    using var owner = CreateAuthenticatedClient(users[0]);
    using var invitee = CreateAuthenticatedClient(users[1]);
    var item = await CreateEventAsync(owner, "public");
    var invitation = await ReadAsync<EventInvitationResponse>(await owner.PostAsJsonAsync(
        $"/api/events/{item.Id}/invites", new { userId = users[1] }));

    var notifications = await ReadAsync<NotificationPageResponse>(
        await invitee.GetAsync("/api/notifications"));

    var notification = Assert.Single(notifications.Items.Where(x => x.Type == "EventInvite"));
    Assert.Equal(invitation.Id, notification.EntityId);
    Assert.Equal(item.Id, notification.ParentEntityId);
}
```

- [ ] **Step 3: Run the focused tests to verify RED**

Run:

```bash
dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests --filter "FullyQualifiedName~Comment_reaction_notification_includes_its_parent_post_id|FullyQualifiedName~Event_invite_notification_includes_its_event_id"
```

Expected: compile failure because `NotificationResponse.ParentEntityId` does not exist.

- [ ] **Step 4: Extend the response contract and batch projection**

Add the nullable response field immediately after `EntityId`:

```csharp
public sealed record NotificationResponse(
    Guid Id, Guid RecipientUserId, Guid? ActorUserId, string? ActorUsername,
    string? ActorDisplayName, string Type, string? EntityType, Guid? EntityId,
    Guid? ParentEntityId, bool IsRead, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc);
```

At the start of `ToResponsesAsync`, gather distinct IDs for Comment notifications and `EventInvite` notifications. Query each source once, then project matching values through dictionaries:

```csharp
var commentPostIds = await dbContext.Comments.AsNoTracking()
    .Where(comment => commentIds.Contains(comment.Id) && comment.DeletedAtUtc == null)
    .Select(comment => new { comment.Id, comment.PostId })
    .ToDictionaryAsync(item => item.Id, item => item.PostId, cancellationToken);
var eventInviteEventIds = await dbContext.EventInvitations.AsNoTracking()
    .Where(invitation => eventInviteIds.Contains(invitation.Id))
    .Select(invitation => new { invitation.Id, invitation.EventId })
    .ToDictionaryAsync(item => item.Id, item => item.EventId, cancellationToken);
```

Set `parentEntityId` only for `NotificationEntityType.Comment`, and for `NotificationType.EventInvite`; pass it to the `NotificationResponse` constructor. Do not alter `QueueAsync`, entity constructors, mapping, or the surface-access switch.

- [ ] **Step 5: Run the focused tests to verify GREEN**

Run the Step 3 command again.

Expected: both tests pass; the response exposes the owning Post/Event ID while persisted notifications retain their original `EntityId`.

- [ ] **Step 6: Commit and push the backend slice**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Notifications/DTOs/Responses/NotificationPageResponse.cs \
  backend/Fookbase.Src/Main/Code/Modules/Notifications/Services/NotificationService.cs \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PostEndpointsTests.cs \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/EventEndpointsTests.cs
git commit -m "fix: bổ sung đích cha cho thông báo"
git push origin main
```

### Task 2: Centralize typed notification presentation and navigation

**Files:**
- Modify: `frontend/web/src/api/notifications.ts`
- Create: `frontend/web/src/shared/notificationPresentation.ts`
- Modify: `frontend/web/src/pages/notifications/NotificationsPage.tsx`
- Modify: `frontend/web/src/layout/TopNavbar.tsx`

**Interfaces:**
- Consumes: `AppNotification` with `type`, `entityType`, `entityId`, `parentEntityId`, and optional actor ID.
- Produces: `getNotificationDestination(notification): string`, `getNotificationMessage(notification): string`, and `getNotificationBadge(notification): { icon: string; className: string }`.

- [ ] **Step 1: Expand the frontend API contract exhaustively**

Update `AppNotificationType` to include `EventInvite`, `EventUpdated`, `EventCancelled`, and `AccountWarning`. Update `AppNotificationEntityType` to include `UserFollow` and `Event`. Add this property to `AppNotification`:

```ts
parentEntityId: string | null
```

- [ ] **Step 2: Add the shared resolver with explicit cases**

Create `frontend/web/src/shared/notificationPresentation.ts`. Use typed `Record<AppNotificationType, string>` objects for Vietnamese copy and badge values so TypeScript requires a value for every backend-emitted type. Include the four new messages:

```ts
EventInvite: 'đã mời bạn tham gia một sự kiện.',
EventUpdated: 'Sự kiện bạn quan tâm vừa được cập nhật.',
EventCancelled: 'Sự kiện đã bị hủy.',
AccountWarning: 'Tài khoản của bạn đã nhận một cảnh báo.',
```

Implement destinations with an exhaustive `switch (notification.type)` and no default branch:

```ts
case 'PostReaction':
case 'PostComment':
case 'PostShared':
case 'PostMention':
  return notification.entityId ? `/posts/${notification.entityId}` : '/notifications'
case 'CommentReaction':
case 'CommentMention':
  return notification.entityId && notification.parentEntityId
    ? `/posts/${notification.parentEntityId}#comment-${notification.entityId}`
    : '/notifications'
case 'EventInvite':
case 'EventUpdated':
case 'EventCancelled':
  {
    const eventId = notification.parentEntityId ?? notification.entityId
    return eventId ? `/events/${eventId}` : '/notifications'
  }
case 'AccountWarning':
  return '/settings/security'
```

Keep existing actor-profile, group, page, and Story destinations as explicit cases; if a precise target is absent, return `/notifications`, not `/feed`.

- [ ] **Step 3: Replace both duplicated UI implementations**

In `NotificationsPage.tsx`, remove local `destination`, `message`, and `badge` functions and import all three shared helpers. In `TopNavbar.tsx`, remove `notificationDestination`, `notificationText`, and `notificationBadge`; import the same helper functions. Preserve the existing mark-read behavior, visual layout, and dropdown closing behavior.

- [ ] **Step 4: Run frontend static validation**

Run:

```bash
cd frontend/web && npm run lint && npm run build
```

Expected: lint and TypeScript build pass, proving all notification enum cases are represented without `any`.

- [ ] **Step 5: Commit and push the centralized notification UI**

```bash
git add frontend/web/src/api/notifications.ts \
  frontend/web/src/shared/notificationPresentation.ts \
  frontend/web/src/pages/notifications/NotificationsPage.tsx \
  frontend/web/src/layout/TopNavbar.tsx
git commit -m "fix: đồng bộ hiển thị và điều hướng thông báo"
git push origin main
```

### Task 3: Add the authorized Post detail deep link

**Files:**
- Create: `frontend/web/src/pages/posts/PostDetailPage.tsx`
- Modify: `frontend/web/src/routes/index.tsx`
- Modify: `frontend/web/src/pages/feed/components/LivePostCard.tsx`
- Modify: `frontend/web/src/pages/feed/components/PostDiscussion.tsx`

**Interfaces:**
- Consumes: `postsApi.getById(postId)`, `ApiError`, `useAuth().session.user.id`, `LivePostCard`, and URL hash `#comment-{commentId}`.
- Produces: route `/posts/:postId`; `LivePostCard` optional prop `initialCommentId?: string`; comment DOM IDs `comment-{comment.id}`.

- [ ] **Step 1: Create the post-detail loading surface**

Create `PostDetailPage.tsx`. Read `postId` with `useParams`, load exactly one post through `postsApi.getById(postId)` in a cleanup-safe `useEffect`, and keep `Post | null`, loading, and error state. Extract a UUID from `window.location.hash` only when it matches `#comment-<id>`; pass that value as `initialCommentId`.

For 403 or 404, render exactly:

```tsx
<main className="mx-auto max-w-3xl p-5 text-center text-text-muted">
  Nội dung này không còn khả dụng.
</main>
```

For other API errors, render the existing `ApiError.message`. When loaded, render the existing card and keep updates local:

```tsx
<LivePostCard
  post={post}
  currentUserId={session.user.id}
  initialCommentId={initialCommentId}
  onPostUpdated={setPost}
  onPostDeleted={() => navigate('/feed', { replace: true })}
/>
```

- [ ] **Step 2: Open and anchor a Comment destination in the existing card**

Add optional `initialCommentId?: string` to `LivePostCardProps`. When it exists, use a guarded effect to call the existing `loadComments` path and open the existing comments dialog once. Pass `initialCommentId` to each `DiscussionList` invocation. Add `initialCommentId?: string` to `DiscussionListProps`; set `id={`comment-${comment.id}`}` on each comment root and, after the loaded list contains that ID, call `document.getElementById(...)?.scrollIntoView({ block: 'center' })`. Do not add a separate Comment renderer or client-side comment-to-post lookup.

- [ ] **Step 3: Register the route**

Add the lazy import and route next to the existing post-adjacent routes in `routes/index.tsx`:

```tsx
const postDetailPage = lazy(() => import('../pages/posts/PostDetailPage'))
// ... inside MainLayout children
{ path: 'posts/:postId', element: page(postDetailPage) },
```

- [ ] **Step 4: Validate the web app and manually smoke-test access behavior**

Run:

```bash
cd frontend/web && npm run lint && npm run build
```

Then, while authenticated as an allowed user, open a Post notification, Comment notification, Event notification, and AccountWarning notification from both the dropdown and `/notifications`. Confirm the Post card loads, the linked comment dialog opens and scrolls when its comment is in the loaded page, and a deleted/private Post URL renders the unavailable copy. Do not attempt to work around a 403/404.

- [ ] **Step 5: Run backend regression and commit the deep-link UI**

Run:

```bash
./scripts/test-backend.sh
```

Expected: all canonical backend projects pass without adding a migration.

Commit and push:

```bash
git add frontend/web/src/pages/posts/PostDetailPage.tsx \
  frontend/web/src/routes/index.tsx \
  frontend/web/src/pages/feed/components/LivePostCard.tsx \
  frontend/web/src/pages/feed/components/PostDiscussion.tsx
git commit -m "feat: mở bài viết từ thông báo"
git push origin main
```

## Plan self-review

- **Spec coverage:** Task 1 covers safe Comment/Event parent projection with no schema change; Task 2 covers exhaustive API types, Vietnamese display copy, shared resolver, and precise destinations; Task 3 covers the reused post detail surface, unavailable state, and Comment anchor behavior.
- **Placeholder scan:** No deferred implementation markers or unspecified test steps are present.
- **Type consistency:** `ParentEntityId` is spelled consistently in C# and maps to `parentEntityId` through the existing JSON camel-case convention. The helper names and `initialCommentId` prop match every consuming task.
