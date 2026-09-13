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
