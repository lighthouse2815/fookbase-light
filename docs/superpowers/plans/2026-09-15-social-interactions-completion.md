# Social Interactions Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete post reaction discovery, friend-request actions, comment reactions, and the associated documentation.

**Architecture:** Posts keeps ownership of the reaction-list endpoint and calls a bounded Friends projection to attach relationship status without per-row HTTP requests. The web reaction dialog consumes the existing Friends mutation API and the offset-page contract. Comment reaction controls reuse the six reaction types and existing protected comment-reaction endpoints.

**Tech Stack:** ASP.NET Core 10 Minimal API, EF Core/Npgsql, xUnit integration tests, React 19, TypeScript, Vite, Tailwind CSS.

**Spec:** `docs/superpowers/specs/2026-09-15-social-interactions-completion-design.md`

## Global Constraints

- Reaction-list reads must retain existing post visibility checks and return no relationship data to an anonymous caller.
- Relationship data is loaded in bounded queries; do not call an endpoint or database query once per reactor.
- `Thêm bạn bè` sends the existing friend request and never bypasses block or request validation.
- Reactions are exactly `like`, `love`, `haha`, `wow`, `sad`, and `angry`.
- Keep the modular monolith and the shared `FookbaseDbContext`; add no dependency.
- Each finished task is validated, committed with a Vietnamese Conventional Commit, and pushed to `origin/main`.

---

## File Structure

- `backend/.../Friends/Services/FriendsService.cs`: provides batched relationship statuses for a viewer and a bounded set of user IDs.
- `backend/.../Posts/DTOs/Responses/PostReactionResponse.cs`: exposes a reactor's relationship status alongside the existing safe profile fields.
- `backend/.../Posts/Services/{PostsUseCase,PostsService}.cs`: requests the relationship projection after post visibility is authorized.
- `backend/.../Posts/Api.IntegrationTests/PostEndpointsTests.cs`: proves relationship data and reaction list pagination/filtering.
- `frontend/web/src/api/posts.ts`: adds typed comment reaction mutations and reaction-list page access.
- `frontend/web/src/pages/feed/components/{LivePostCard,PostDiscussion}.tsx`: renders paged post reactors, sends friend requests, and provides comment reaction controls.
- `frontend/web/src/pages/feed/components/reactionDialogState.{ts,test.ts}`: keeps reaction-page merging deterministic and independently tested.
- `frontend/web/package.json`: adds the `test` script and Vitest development dependency used for the new pure UI-state test.
- `README.md`: documents post reaction-list and comment reaction endpoints.
- `docs/superpowers/plans/*.md`: records only plan items with source/test/commit evidence as completed.

### Task 1: Return batched relationship status with post reactors

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/Services/FriendsService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/DTOs/Responses/PostReactionResponse.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsUseCase.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsService.cs`
- Test: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PostEndpointsTests.cs`

**Interfaces:**
- Produces `FriendsService.GetStatusesAsync(Guid actorUserId, IReadOnlyCollection<Guid> otherUserIds, CancellationToken)` returning `IReadOnlyDictionary<Guid, RelationshipStatusResponse>`.
- Extends `PostReactionResponse` with `string RelationshipStatus` and `Guid? RelationshipRequestId`.
- Consumes the existing `GET /api/posts/{postId}/reactions?type=&offset=&limit=` contract.

- [ ] **Step 1: Write the failing integration test**

```csharp
[Fact]
public async Task Reaction_list_returns_each_reactors_relationship_status()
{
    var users = await CreateUserIdsAsync(3);
    using var author = CreateAuthenticatedClient(users[0]);
    using var reactor = CreateAuthenticatedClient(users[1]);
    using var viewer = CreateAuthenticatedClient(users[2]);
    var post = await CreatePostAsync(author, "relationship reaction", "public");

    await reactor.PutAsJsonAsync($"/api/posts/{post.Id}/reaction", new { type = "love" });
    var page = await ReadAsync<PagedResponse<PostReactionResponse>>(
        await viewer.GetAsync($"/api/posts/{post.Id}/reactions"));

    var item = Assert.Single(page.Items);
    Assert.Equal(users[1], item.UserId);
    Assert.Equal("none", item.RelationshipStatus);
    Assert.Null(item.RelationshipRequestId);
}
```

- [ ] **Step 2: Run the test and verify RED**

Run: `bash scripts/test-backend.sh` after temporarily limiting `projects` to the Posts project, or run the Posts test project in the SDK 10 Docker image with its isolated PostgreSQL dependency.

Expected: compilation failure because `PostReactionResponse` does not yet expose `RelationshipStatus`.

- [ ] **Step 3: Add the minimal batched relationship projection**

```csharp
public async Task<IReadOnlyDictionary<Guid, RelationshipStatusResponse>> GetStatusesAsync(
    Guid actorUserId,
    IReadOnlyCollection<Guid> otherUserIds,
    CancellationToken cancellationToken = default)
{
    var ids = otherUserIds.Where(id => id != actorUserId).Distinct().ToArray();
    // Query blocks, accepted friendships, and pending requests once each.
    // Precedence is blocked, friends, pending request, then none.
}
```

Call the method from `PostsUseCase.GetReactionsAsync`, pass its result to `PostsService.GetReactionsAsync`, and map each reactor to the returned status/request ID. Keep the current post authorization before reading reactor identities.

- [ ] **Step 4: Run the focused test and backend build**

Run: `bash scripts/test-backend.sh` with the Posts project selected, then `docker compose build api`.

Expected: test passes and the API image builds without warnings/errors.

- [ ] **Step 5: Commit and push the backend slice**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Friends/Services/FriendsService.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/DTOs/Responses/PostReactionResponse.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsUseCase.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsService.cs \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PostEndpointsTests.cs
git commit -m "feat: trả trạng thái bạn bè trong danh sách cảm xúc"
git push origin HEAD
```

### Task 2: Add friend requests and load-more behavior to the reaction dialog

**Files:**
- Modify: `frontend/web/src/api/posts.ts`
- Modify: `frontend/web/src/pages/feed/components/LivePostCard.tsx`

**Interfaces:**
- Consumes `PostReaction.relationshipStatus`, `PostReaction.relationshipRequestId`, `postsApi.getReactions(postId, type, offset, limit)`, and `friendsApi.sendRequest(userId)`.
- Produces `ReactionDialog` behavior with `offset`, `total`, `isLoadingMore`, and a per-user pending state.

- [ ] **Step 1: Add Vitest and write the failing pure paging-state test**

Install the current maintained Vitest release as a dev dependency, add `"test": "vitest run"` to `frontend/web/package.json`, then create `frontend/web/src/pages/feed/components/reactionDialogState.test.ts`:

```ts
import { appendReactionPage } from './reactionDialogState'

it('appends unique reactors and reports more rows from the page total', () => {
  const state = appendReactionPage([], [{ userId: 'a' }, { userId: 'b' }], 2, 3)
  expect(state.items).toHaveLength(2)
  expect(state.hasMore).toBe(true)
})
```

- [ ] **Step 2: Run the test and verify RED**

Run: `npm test -- reactionDialogState.test.ts` from `frontend/web` after adding the project test runner configuration.

Expected: FAIL because `appendReactionPage` is not exported.

- [ ] **Step 3: Implement the smallest typed dialog state and UI**

```ts
export function appendReactionPage<T extends { userId: string }>(
  current: readonly T[], incoming: readonly T[], offset: number, total: number,
) {
  const ids = new Set(current.map((item) => item.userId))
  const items = [...current, ...incoming.filter((item) => !ids.has(item.userId))]
  return { items, nextOffset: offset + incoming.length, hasMore: offset + incoming.length < total }
}
```

Use `limit=20`, clear rows when changing filter, append rows on `Xem thêm`, disable its button while loading, and preserve the displayed rows on a load-more failure. Render `Thêm bạn bè` only for `relationshipStatus === 'none'`; after `friendsApi.sendRequest`, update that row to `request_sent` without closing the dialog.

- [ ] **Step 4: Run the frontend checks**

Run: `npm run lint && npm run build` from `frontend/web`.

Expected: both commands pass with no lint warnings.

- [ ] **Step 5: Commit and push the web reaction dialog slice**

```bash
git add frontend/web/src/api/posts.ts frontend/web/src/pages/feed/components/LivePostCard.tsx \
  frontend/web/src/pages/feed/components/reactionDialogState.ts \
  frontend/web/src/pages/feed/components/reactionDialogState.test.ts frontend/web/package.json
git commit -m "feat: thêm kết bạn và phân trang danh sách cảm xúc"
git push origin HEAD
```

### Task 3: Add comment reaction controls using existing endpoints

**Files:**
- Modify: `frontend/web/src/api/posts.ts`
- Modify: `frontend/web/src/pages/feed/components/PostDiscussion.tsx`
- Modify: `frontend/web/src/pages/feed/components/LivePostCard.tsx`
- Test: `backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PostEndpointsTests.cs`

**Interfaces:**
- Produces `postsApi.setCommentReaction(commentId, type)` and `postsApi.removeCommentReaction(commentId)`.
- Extends `CommentResponse` and the web `Comment` type with `reactionCounts: Record<string, number>` and `viewerReaction: string | null`.
- Consumes the existing `PUT|DELETE /api/posts/comments/{commentId}/reaction` endpoints.

- [ ] **Step 1: Write the failing comment summary test**

```csharp
[Fact]
public async Task Comment_reaction_returns_updated_comment_summary()
{
    // Create a public post and comment, set a love reaction, then GET comments.
    // Assert the list item has reactionCounts["love"] == 1 and viewerReaction == "love".
}
```

- [ ] **Step 2: Run the test and verify RED**

Run: `dotnet test backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj --filter "FullyQualifiedName~Comment_reaction_returns_updated_comment_summary"` in the project’s SDK Docker test environment.

Expected: FAIL because comments do not return a reaction summary.

- [ ] **Step 3: Add minimal backend summary mapping and frontend controls**

```ts
setCommentReaction: (commentId: string, type: string) =>
  apiRequest<Comment>(`/api/posts/comments/${commentId}/reaction`, {
    method: 'PUT', body: JSON.stringify({ type }),
  }),
removeCommentReaction: (commentId: string) =>
  apiRequest<Comment>(`/api/posts/comments/${commentId}/reaction`, { method: 'DELETE' }),
```

Use the same six-choice emoji picker on each comment. The selected reaction is the compact action label; clicking it removes it, and selecting another updates it. Update the in-memory comment item only from the server response.

- [ ] **Step 4: Run focused backend and frontend validation**

Run: `bash scripts/test-backend.sh` with Posts selected, then `npm run lint && npm run build` from `frontend/web`.

Expected: test, lint, and build all pass.

- [ ] **Step 5: Commit and push comment reactions**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Posts \
  backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/PostEndpointsTests.cs \
  frontend/web/src/api/posts.ts frontend/web/src/pages/feed/components/PostDiscussion.tsx \
  frontend/web/src/pages/feed/components/LivePostCard.tsx
git commit -m "feat: thêm cảm xúc cho bình luận"
git push origin HEAD
```

### Task 4: Refresh documentation and plan status from evidence

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/plans/2026-09-13-user-follow-friend-suggestions-v1.md`
- Modify: `docs/superpowers/plans/2026-09-14-memories-birthdays-profile-extras-v1.md`
- Modify: `docs/superpowers/plans/2026-09-14-photos-albums-v1.md`

**Interfaces:**
- Documents `GET /api/posts/{postId}/reactions?type=&offset=&limit=` as Bearer JWT and its relation to post visibility.
- Documents the existing post/comment reaction mutation routes and supported reaction types.

- [ ] **Step 1: Audit evidence for each existing plan task**

Run:

```bash
git log --oneline -- docs/superpowers/plans
rg -n "MapMemoryEndpoints|MapPhotoAlbumEndpoints|GetSuggestions|follow|PhotoAlbum" backend/Fookbase.Src/Main frontend/web/src
```

Record only tasks whose listed deliverables exist in source and whose tests/build evidence is available from current CI or commit history.

- [ ] **Step 2: Update the API reference and checked plan items**

Add the following README row in the Posts table:

```markdown
| GET | `/api/posts/{postId}/reactions?type=&offset=&limit=` | Bearer JWT, theo quyền xem bài viết |
```

Change only supported plan task checkboxes from `- [ ]` to `- [x]`; do not mark smoke, migration, or full-regression steps as complete without recorded evidence.

- [ ] **Step 3: Verify documentation changes**

Run: `git diff --check && rg -n "posts/\{postId\}/reactions|^- \[x\]" README.md docs/superpowers/plans`

Expected: no whitespace errors; API row and only evidence-backed checklist updates appear.

- [ ] **Step 4: Commit and push documentation**

```bash
git add README.md docs/superpowers/plans
git commit -m "docs: cập nhật trạng thái tương tác và kế hoạch"
git push origin HEAD
```

## Plan Self-Review

- Spec coverage: Task 1 implements safe relationship projection; Task 2 implements friend request and pagination; Task 3 implements comment reactions; Task 4 updates API/docs and evidence-backed checklists.
- Placeholder scan: no temporary implementation labels or unassigned tasks remain.
- Type consistency: `RelationshipStatusResponse`, `PostReactionResponse`, `postsApi.getReactions`, and the six reaction type strings use the existing project naming and routes.
