# Social Interactions Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete post reaction discovery, friend-request actions, comment reactions, and the associated documentation.

**Architecture:** Posts keeps ownership of the reaction-list endpoint and calls a bounded Friends projection to attach relationship status without per-row HTTP requests. The web reaction dialog consumes the existing Friends mutation API and the offset-page contract. Comment reaction controls reuse the six reaction types and existing protected comment-reaction endpoints.

**Tech Stack:** ASP.NET Core 10 Minimal API, EF Core/Npgsql, React 19, TypeScript, Vite, Tailwind CSS.

**Spec:** `docs/superpowers/specs/2026-09-15-social-interactions-completion-design.md`

## Global Constraints

- Reaction-list reads must retain existing post visibility checks and return no relationship data to an anonymous caller.
- Relationship data is loaded in bounded queries; do not call an endpoint or database query once per reactor.
- `Thêm bạn bè` sends the existing friend request and never bypasses block or request validation.
- Reactions are exactly `like`, `love`, `haha`, `wow`, `sad`, and `angry`.
- Keep the modular monolith and the shared `FookbaseDbContext`; add no dependency.
- Do not add or run automated tests for this implementation, at the user's request; retain build and lint validation.
- Each finished task is validated, committed with a Vietnamese Conventional Commit, and pushed to `origin/main`.

---

## File Structure

- `backend/.../Friends/Services/FriendsService.cs`: provides batched relationship statuses for a viewer and a bounded set of user IDs.
- `backend/.../Posts/DTOs/Responses/PostReactionResponse.cs`: exposes a reactor's relationship status alongside the existing safe profile fields.
- `backend/.../Posts/Services/{PostsUseCase,PostsService}.cs`: requests the relationship projection after post visibility is authorized.
- `frontend/web/src/api/posts.ts`: adds typed comment reaction mutations and reaction-list page access.
- `frontend/web/src/pages/feed/components/{LivePostCard,PostDiscussion}.tsx`: renders paged post reactors, sends friend requests, and provides comment reaction controls.
- `README.md`: documents post reaction-list and comment reaction endpoints.
- `docs/superpowers/plans/*.md`: records only plan items with source/test/commit evidence as completed.

### Task 1: Return batched relationship status with post reactors

**Files:**
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Friends/Services/FriendsService.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/DTOs/Responses/PostReactionResponse.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsUseCase.cs`
- Modify: `backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsService.cs`

**Interfaces:**
- Produces `FriendsService.GetStatusesAsync(Guid actorUserId, IReadOnlyCollection<Guid> otherUserIds, CancellationToken)` returning `IReadOnlyDictionary<Guid, RelationshipStatusResponse>`.
- Extends `PostReactionResponse` with `string RelationshipStatus` and `Guid? RelationshipRequestId`.
- Consumes the existing `GET /api/posts/{postId}/reactions?type=&offset=&limit=` contract.

- [ ] **Step 1: Add the minimal batched relationship projection**

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

- [ ] **Step 2: Build the backend image**

Run: `docker compose build api`.

Expected: the API image builds without warnings/errors.

- [ ] **Step 3: Commit and push the backend slice**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Friends/Services/FriendsService.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/DTOs/Responses/PostReactionResponse.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsUseCase.cs \
  backend/Fookbase.Src/Main/Code/Modules/Posts/Services/PostsService.cs
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

- [ ] **Step 1: Implement the typed dialog state and UI**

Use `limit=20`, clear rows when changing filter, append rows on `Xem thêm`, disable its button while loading, and preserve the displayed rows on a load-more failure. Render `Thêm bạn bè` only for `relationshipStatus === 'none'`; after `friendsApi.sendRequest`, update that row to `request_sent` without closing the dialog.

- [ ] **Step 2: Run the frontend checks**

Run: `npm run lint && npm run build` from `frontend/web`.

Expected: both commands pass with no lint warnings.

- [ ] **Step 3: Commit and push the web reaction dialog slice**

```bash
git add frontend/web/src/api/posts.ts frontend/web/src/pages/feed/components/LivePostCard.tsx
git commit -m "feat: thêm kết bạn và phân trang danh sách cảm xúc"
git push origin HEAD
```

### Task 3: Add comment reaction controls using existing endpoints

**Files:**
- Modify: `frontend/web/src/api/posts.ts`
- Modify: `frontend/web/src/pages/feed/components/PostDiscussion.tsx`
- Modify: `frontend/web/src/pages/feed/components/LivePostCard.tsx`

**Interfaces:**
- Produces `postsApi.setCommentReaction(commentId, type)` and `postsApi.removeCommentReaction(commentId)`.
- Extends `CommentResponse` and the web `Comment` type with `reactionCounts: Record<string, number>` and `viewerReaction: string | null`.
- Consumes the existing `PUT|DELETE /api/posts/comments/{commentId}/reaction` endpoints.

- [ ] **Step 1: Add minimal backend summary mapping and frontend controls**

```ts
setCommentReaction: (commentId: string, type: string) =>
  apiRequest<Comment>(`/api/posts/comments/${commentId}/reaction`, {
    method: 'PUT', body: JSON.stringify({ type }),
  }),
removeCommentReaction: (commentId: string) =>
  apiRequest<Comment>(`/api/posts/comments/${commentId}/reaction`, { method: 'DELETE' }),
```

Use the same six-choice emoji picker on each comment. The selected reaction is the compact action label; clicking it removes it, and selecting another updates it. Update the in-memory comment item only from the server response.

- [ ] **Step 2: Run backend and frontend validation**

Run: `docker compose build api`, then `npm run lint && npm run build` from `frontend/web`.

Expected: the API image, lint, and web build all pass.

- [ ] **Step 3: Commit and push comment reactions**

```bash
git add backend/Fookbase.Src/Main/Code/Modules/Posts \
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
