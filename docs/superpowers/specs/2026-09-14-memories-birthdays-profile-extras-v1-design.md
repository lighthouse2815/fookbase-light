# Milestone 15: Memories, Birthdays, and Profile Extras V1

## Goal and scope

Milestone 15 adds three small, independent product surfaces without refactoring the existing modular monolith:

1. private, read-only "On this day" memories for the signed-in user;
2. privacy-aware birthday discovery for friends; and
3. a small set of optional profile-intro fields.

The milestone reuses Users, Friends, Posts, Media, the existing profile endpoints, and the existing web post cards. It does not change feed ranking, global search, Photos/Albums, or notifications infrastructure.

## Date and time policy

Calendar calculations use the application server's configured local timezone. A shared date helper obtains the current server-local date so memory matching, birthday-today, and birthday-upcoming agree. Tests control the clock/date source already used by the application where available.

`DateOfBirth` is a date-only value, mapped to PostgreSQL `date`; it is never stored as a UTC timestamp. Feb 29 birthdays occur on Feb 29 in leap years and on Feb 28 in non-leap years. The same occurrence rule is used by both today and upcoming queries.

## Memories

`GET /api/memories/today` is authenticated and always derives the owner from the access token. No route accepts another user's ID. An optional `GET /api/memories?date=MM-DD` is included only if it can share the exact same validated query path without adding calendar navigation.

The response returns `date` and groups, ordered newest original year first. Each group contains `year`, `yearsAgo`, and existing post-shaped items. There are no `MemoryPost`, `MemoryPhoto`, `MemoryMedia`, or `MemoryShare` tables/entities.

Eligibility is SQL-filtered to the actor's own, non-deleted, Standard posts in the Profile container whose server-local creation month/day match the requested date and whose year is earlier than the current year. Reels and group, page, event, story, and Messenger content are excluded by those predicates. Existing post/media response loading is reused so deleted, inaccessible, or broken media is omitted under normal media rules. A bounded maximum protects the daily endpoint; no pagination is required.

The owner may create a normal new Profile post/share from a selected memory. The original post is never mutated and new content follows normal post privacy and share rules. If the existing share flow cannot safely represent the original item, the client creates a normal Profile post without a new Memory-specific entity.

## Birthday data and privacy

`UserProfile` already has nullable `DateOfBirth` and `CurrentCity`; M15 completes their privacy-aware behavior and adds the remaining nullable fields:

- `DateOfBirth` (`DateOnly?`, existing)
- `CurrentCity` (100 chars, existing)
- `Hometown` (100 chars)
- `Workplace` (150 chars)
- `Education` (150 chars)
- `Website` (a bounded URL string)

It also gains `BirthdayVisibility`: `OnlyMe`, `Friends`, or `Public`, defaulting to `OnlyMe`. Existing rows retain their existing nullable birth dates and receive `OnlyMe` through one new migration. Profile update validation rejects future birth dates and malformed/out-of-range values, enforces the string limits, and accepts website URLs only with `https` or `http` schemes. The server never fetches a submitted URL.

Profile response mapping is viewer-aware. The owner can receive their full `DateOfBirth`; all other viewers receive only a safe birthday presentation when visibility permits (month/day and a relevant occurrence indicator), never the birth year or age. `OnlyMe` is owner-only. `Friends` requires an active friendship. `Public` requires an authenticated, non-blocked viewer under the existing profile-access rules. Intro fields follow the existing profile/block visibility rules and do not gain per-field audiences.

## Birthday APIs

`GET /api/birthdays/today` returns safe previews only for active, current friends whose visibility permits the actor to see today's occurrence. `GET /api/birthdays/upcoming?days=7` uses 7 by default, rejects invalid values, caps the window at 30, and returns the same preview ordered by the next effective birthday occurrence.

Queries begin from the actor's friendship candidates, exclude blocked relationships in both directions, join the required active profiles, and filter occurrences in SQL as far as provider support permits. They do not load all users into memory. No scheduled job or automatic birthday notification is introduced.

## Web experience

The web application adds:

- `/memories`, titled "Ngày này năm xưa", using the existing post card/media rendering, original timestamps, group labels such as "1 năm trước", a normal empty state, and an optional normal-post/share action;
- `/birthdays`, with Today and Upcoming sections showing avatar, display name, month/day, relationship state, and an optional link into existing Messenger; and
- an unobtrusive, bounded Home/sidebar birthday summary when the current layout has room.

The current profile page gains an Intro/About section that keeps the existing bio and conditionally shows lives in, hometown, workplace, education, and website. Website links use `rel="noopener noreferrer"`. The current owner profile editor receives accessible native controls for all new fields and birthday visibility. No second profile-settings application or profile-page redesign is introduced.

## Persistence, performance, and compatibility

One normal migration updates the unified database model and snapshot; previous migrations remain untouched. Existing profiles remain valid with null optional values and private birthdays. Legacy import continues to work because the added columns have safe defaults/nullability.

Before adding indexes, the implementation will inspect the query shape and existing post indexes (`AuthorUserId`/post type/created time and container indexes). A new index is added only if needed for the final SQL query; no speculative duplicate index is created.

## Testing and validation

The milestone keeps the lightweight policy with approximately 4–6 integration tests covering:

1. an owner receives only eligible prior-year own Profile posts;
2. a deleted historical post is absent;
3. birthday visibility and profile DTO privacy do not expose a full birth date to an unauthorized viewer;
4. blocks suppress birthday results;
5. upcoming birthdays span December to January; and
6. the Feb 29 policy where practical within the focused suite.

After focused tests, validation runs the established backend regression once, pending-model and fresh-migration checks, legacy import when applicable, Docker health checks, and web/messenger/admin build plus lint. No new frontend automated suite or repeated full integration runs are required.

## Explicitly deferred

Automatic memory/birthday notifications, AI memories, album-wide automatic photo resurfacing, custom birthday audiences, relationship/family data, employment or education history, birthday commerce, marketplace, ads, and recommendation systems are outside M15.
