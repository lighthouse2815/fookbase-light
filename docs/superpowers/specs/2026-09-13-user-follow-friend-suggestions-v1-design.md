# Milestone 12: User Follow + Friend Suggestions V1

## Scope and decisions

Milestone 12 extends the existing ASP.NET Core modular monolith. The social
graph remains in `Modules/Friends` and uses the existing `FookbaseDbContext`
and PostgreSQL database. It introduces no new service, database, broker,
repository abstraction, or graph engine.

`UserFollow` is a directional edge with `FollowerUserId`, `FollowingUserId`,
and `FollowedAtUtc`. Its composite pair is unique. A user cannot follow themself,
an absent or ineligible account, or either side of a blocked relationship.
Follow and unfollow are idempotent.

The V1 unfriend policy retains follows: after unfriend, a remaining follow may
still make the former friend's Public content eligible, but never Friends-only
content. Blocking removes follows in both directions in the same Friends
transaction; unblocking does not restore them.

## Friends graph and APIs

`FriendsService` owns pair-locked mutations. Accepting a friend request creates
the friendship and both directional follows before the transaction commits.
It does not issue `UserFollowed` notifications for those automatic follows.
Its existing block flow removes both follow rows alongside friendship and pending
requests.

Manual non-friend follows create one general `UserFollowed` notification for
the followed user; a no-op follow creates none. This avoids friendship-acceptance
noise and self-notifications.

Authenticated endpoints add `POST` and `DELETE /api/users/{userId}/follow`,
plus follower and following list endpoints. List pages are opaque protected,
versioned, viewer-bound keysets ordered by `FollowedAtUtc` and user ID. Each
response exposes only safe public-profile fields and never includes blocked or
ineligible relations. Counts are projected in SQL and use the same visibility
filter. User profile and people-search projections include follow state,
friendship state, and counts in batched queries rather than per-card lookups.

## Migration and data model

The unified EF migration creates `UserFollows` with a unique
`(FollowerUserId, FollowingUserId)` index and reverse/keyset indexes used by
lists and feed membership. It backfills every existing normalized friendship in
both directions using conflict-safe SQL, so it is safe for existing production
data and fresh databases. The model snapshot is updated; old migrations remain
untouched.

## Feed behavior

Feed access snapshots gain followed user IDs while retaining friend and block
sets. On every page request, including an old cursor, eligibility is rebuilt.
Profile candidates are self plus followed users:

- followed friends may contribute Public and Friends posts/Reels;
- followed non-friends may contribute Public posts/Reels only;
- unfollowed friends no longer contribute organic profile content;
- groups, followed Pages, own content, and existing bounded Reel discovery
  retain their current roles.

Following mode uses Follow edges for profile content and never injects suggested
public Reels. Ranked home adds a configurable `FollowedNonFriendProfile`
affinity below a followed friend and above Group/Page/discovery. Existing stable
AsOfUtc, protected cursor, and ranking-version semantics remain in place.

## Friend suggestions

`FriendSuggestionService` produces bounded SQL candidates from friends of
friends, then shared active Groups and shared followed Pages. It excludes self,
friends, either pending request direction, blocks, absent accounts, and
ineligible accounts. Configuration in `FriendSuggestionOptions` supplies the
mutual-friend, shared-group, and shared-page weights and candidate bounds.

Suggestions are ranked deterministically by score then candidate user ID. Their
protected, versioned, viewer-bound cursor stores that key. Refreshed sessions
may reorder when the graph changes; unchanged graphs page stably. Results expose
safe profile previews and aggregate counts only, never private group names or
unrelated graph edges. Persistent dismissal is intentionally deferred; the web
client may remove a card locally for the current session.

## Web integration

The profile view displays separate friend and follow states, Follow/Following
controls, and follower/following counts. The Friends surface gains a bounded
People You May Know area with profile, mutual count, Add Friend (reusing the
existing request API), optional Follow, and local dismiss. Search result types
are extended without changing search relevance. Notification labels and icons
recognize `UserFollowed`.

## Validation

Integration coverage will exercise follow, block, friendship, migration-backfill,
feed, suggestions, cursor protection, and notification rules. Existing tests
remain part of the regression run. Validation also includes EF model/migration
checks, fresh migration and legacy import flows, Docker compose and production
image checks, health endpoints, and a real Data Protection cursor that survives
API container recreation with its configured persistent key-ring volume.

The two known `frontend/web` React warnings will be identified and fixed only
when they originate from application code; third-party harmless warnings will
be documented rather than refactored around. All three frontend applications
will run their prescribed install, build, and lint checks.
