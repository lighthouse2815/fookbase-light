# Task 4 — Profile and people-search follow projections

## Delivered

- Extended `UserProfileResponse` and `SearchPersonResponse` with `FollowerCount`,
  `FollowingCount`, nullable `IsFollowing`/`IsFollowedBy`, and nullable
  `FriendshipState`.
- `/api/users/{userId}` and `/api/users/search` now optionally read the JWT
  viewer without requiring authentication. Anonymous responses keep viewer state
  null; authenticated requests receive directed follow and friendship state.
- Profile and local people-search queries use correlated SQL `Count`/`Any`
  projections. Global people search uses equivalent fields in its existing
  ranked SQL projection, so relevance and cursor ordering remain unchanged and
  no per-card follow query is issued.
- Counts exclude inactive/profile-less accounts and follow edges where either
  side has blocked the other. A profile or local people-search result is hidden
  from an authenticated viewer when either direction is blocked; global search
  retains its existing two-way block visibility via the access snapshot.
- Added integration coverage for profile/local-search count and state fields,
  anonymous null viewer state, block invisibility, global people-search
  follow/friend fields, zero-state fields, and preserved ordering.

## RED/GREEN evidence

1. The new integration assertions were added before the production changes.
2. Initial focused test attempt:
   `DOTNET_CLI_HOME=/tmp/fookbase-task4-cli NUGET_PACKAGES="$PWD/.nuget/packages" dotnet test FookbaseLight.sln --filter "FullyQualifiedName~UserProfileEndpointsTests|FullyQualifiedName~Search" --no-restore -p:AllowMissingPrunePackageData=true`
   could not reach compilation/assertions because unrelated projects had stale
   restore assets referencing a removed package cache.
3. After restoring the two affected projects, the first build exposed three
   expression-tree pattern-matching errors and two nullable viewer-ID compile
   errors in the new projection/endpoint code. Those were corrected before the
   GREEN builds below.

## Verification

- `DOTNET_CLI_HOME=/tmp/fookbase-task4-cli NUGET_PACKAGES="$PWD/.nuget/packages" dotnet restore backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj -p:AllowMissingPrunePackageData=true`
  — succeeded.
- `DOTNET_CLI_HOME=/tmp/fookbase-task4-cli NUGET_PACKAGES="$PWD/.nuget/packages" dotnet restore backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj -p:AllowMissingPrunePackageData=true`
  — succeeded.
- `DOTNET_CLI_HOME=/tmp/fookbase-task4-cli NUGET_PACKAGES="$PWD/.nuget/packages" dotnet build backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --no-restore -p:AllowMissingPrunePackageData=true -v:minimal`
  — succeeded, 0 warnings, 0 errors.
- `DOTNET_CLI_HOME=/tmp/fookbase-task4-cli NUGET_PACKAGES="$PWD/.nuget/packages" dotnet build backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj --no-restore -p:AllowMissingPrunePackageData=true -v:minimal`
  — succeeded, 0 warnings, 0 errors.
- Focused executions for `UserProfileEndpointsTests` and `SearchEndpointsTests`
  were both attempted with `dotnet test --no-restore --filter ...`. Both
  compiled then aborted before executing an assertion because testhost requires
  `Microsoft.AspNetCore.App 10.0.0`, which is not installed in this environment.
- `git diff --check` for all Task 4 source and test files — clean.

The current integration-test infrastructure has no SQL-command-count collector,
so no query-count assertion was added. The review instead verifies each profile
card is projected by the endpoint query itself: profile is one query; local
people search remains a total plus page query; global people search remains the
access snapshot queries plus one ranked people query, independent of people-row
count.

## Self-review

- Checked anonymous `/api/users` contracts keep nullable viewer fields rather
  than making JWT mandatory.
- Preserved the old public `UserProfileService.GetAsync` and `SearchAsync`
  overloads while adding viewer-aware overloads.
- Checked state strings match existing relationship status vocabulary:
  `self`, `friends`, `request_sent`, `request_received`, and `none`.
- Did not alter search rank/order/cursor predicates, legacy migrations,
  frontend code, README, environment files, or key material.
- Remaining blocker: install the ASP.NET Core 10 shared runtime to execute the
  integration assertions.

## Review correction — EF composability and viewer-visible counts

### RED evidence

1. Strengthened the profile/local-search fixture before changing production
   code: it now creates a mutually-followed unrelated account that the viewer
   has blocked. Authenticated profile and local-search counts are expected to
   exclude that account, while anonymous local search retains it and still has
   null viewer-state fields. This also preserves coverage of a block between
   the profile owner and another follow edge.
2. Changed global people search to follow the `Beta` result (rather than the
   naturally first `Alpha` result), with only the `viewer -> Beta` direction.
   The assertion therefore catches a follow-rank boost and verifies the
   asymmetric `IsFollowing = true` / `IsFollowedBy = false` contract. Its
   additional viewer-blocked unrelated mutual edge must not affect Beta's
   counts.
3. Focused RED test invocation for the user test built both API and test DLLs,
   but aborted before executing the new assertion because the host lacks
   `Microsoft.AspNetCore.App 10.0.0`.

### GREEN implementation and checks

- Replaced the positional `UserProfileProjection` record construction with an
  EF-composable member-init projection. `GetAsync`, `/me`, local search
  filtering/order/paging, and the post-update read can now continue to use
  projected members in SQL rather than requiring positional-record member
  translation.
- Profile/local-search and global people-search follower/following counts now
  exclude an eligible follow edge when its other account is blocked in either
  direction with the authenticated viewer, in addition to the existing
  owner/other-account block exclusion. Anonymous profile/local-search keeps
  owner-edge visibility only, because there is no viewer relation to apply.
- `dotnet build backend/Fookbase.Src/Tests/Users/Fookbase.Users.Api.IntegrationTests/Fookbase.Users.Api.IntegrationTests.csproj --no-restore -p:AllowMissingPrunePackageData=true -v:minimal`
  — succeeded, 0 warnings, 0 errors.
- `dotnet build backend/Fookbase.Src/Tests/Posts/Fookbase.Posts.Api.IntegrationTests/Fookbase.Posts.Api.IntegrationTests.csproj --no-restore -p:AllowMissingPrunePackageData=true -v:minimal`
  — succeeded, 0 warnings, 0 errors.
- Focused user and global-search `dotnet test --no-restore --filter ...`
  commands both build successfully, then abort before assertions for the same
  missing ASP.NET Core 10 shared runtime. `Microsoft.NETCore.App 10.0.12` is
  installed, but `Microsoft.AspNetCore.App 10.0.0` is not.
