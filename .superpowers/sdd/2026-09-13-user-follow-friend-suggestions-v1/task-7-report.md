# Task 7 — Web follow, suggestions and notifications

## Delivered

- Extended web API contracts for profile follow state/counts, follow/unfollow, follower/following cursor pages, friend-suggestion cursor pages, people-search state, and notification type/entity unions including `UserFollowed`.
- Added a distinct Follow/Following control to viewed profiles. It is hidden for blocked relationships, retains the existing friendship state switch, waits for the mutation to succeed, then reloads profile relationship state and counts.
- Added follower/following counts and the `Follows you` viewer state to viewed profiles.
- Added a 12-item bounded People You May Know page to the personal profile's Friends tab. Cards use only the suggestion safe profile fields, mutual-friend count, the existing friend-request API, optional follow/unfollow, and a local-only Remove action. A successful Add Friend removes the card locally.
- Added non-mutating friend/follow status and counts to people search cards.
- Localized the `UserFollowed` notification sentence while preserving its existing profile destination.
- Updated the GroupDetailPage fallback profile with neutral values for the newly required typed follow fields.

## Validation

Executed in `frontend/web`:

```text
npm ci
```

Result: completed successfully; 77 packages installed and audit reported 0 vulnerabilities.

```text
npm run build
```

The first build correctly failed with `TS2739` because `GroupDetailPage.toAuthor` constructed a `UserProfile` without the newly required follow fields. The fallback was updated with `0` counts and `null` viewer state. The rerun completed successfully: TypeScript build and Vite production build both passed.

```text
npm run lint
```

Result: passed (`oxlint`).

```text
git diff --check -- frontend/web/src
```

Result: passed with no whitespace errors in Task 7 source files.

## Self-review

- Follow and friendship remain separate controls and state sources; a friendship is never presented as a follow unless `isFollowing` says so.
- Blocked viewed profiles do not render a follow control; suggestion candidates rely on server-side eligibility and do not provide block actions.
- Suggestion dismissal performs no network mutation. Send-request success removes only the local card as required; a later refresh lets backend pending-request exclusion determine the new page.
- Search only renders supplied relationship state and does not mutate state, reorder results, or alter pagination.
- No API pagination behavior was changed. Existing Friends offset pagination remains intact; new follow/suggestion pages use their server cursor contracts.
- Existing timer-based React effect cleanup patterns in profile/search-related code were preserved.
- Repository-wide `git diff --check` still reports the pre-existing trailing whitespace in `README.md`; that file was neither edited nor staged by this task.

## Scope and handoff

Only `frontend/web/src` and this Task 7 report are intended for the focused commit. `README.md` remains an unrelated unstaged pre-existing change. No push was performed because Task 7 requested a local commit only.

## Review follow-up

- A successful block now immediately clears the viewed profile, relationship-bound post state, and cached follow control. The blocked state keeps an Unblock action without displaying a stale profile; profile requests also use a generation guard so an older response cannot restore that state.
- Follow and unfollow now preserve loaded post pages and offsets. They update the completed mutation locally, then refresh only profile, relationship, and mutual-count state. Independent refresh failures do not overwrite the successful follow state with an update error.
- Suggestion requests now pass an abort signal and use an incrementing request generation. Entering the tab, Refresh, a local Remove, Add Friend, and Follow/Unfollow cancel older loads; only the current mounted request may set cards, loading, or its error. The Friends Refresh button reloads suggestions, and a visible per-suggestion retry is shown on failure instead of an empty-state message.
- Notification API unions now cover every current server enum value: `PostShared`, `PageRoleInvite`, `Page`, and `PageRoleInvitation`. The navbar renders a text and destination for Page-role invites and a user-visible generic text fallback for future runtime values.

### Review validation

Executed in `frontend/web` after the review fixes:

```text
npm ci && npm run lint && npm run build
```

Result: `npm ci` installed 77 packages and reported 0 vulnerabilities; `npm run lint` passed (`oxlint`); TypeScript and the Vite production build passed.

```text
git diff --check -- src
```

Result: passed with no whitespace errors in the changed web source files.
