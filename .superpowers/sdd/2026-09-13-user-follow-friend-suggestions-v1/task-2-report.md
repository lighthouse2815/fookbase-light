# Task 2 report: directed user follow and friendship backfill

Date: 2026-09-13 (Asia/Ho_Chi_Minh)

## Delivered scope

- Added `UserFollow` with private construction and `Create(followerUserId,
  followingUserId, followedAtUtc)`. The factory rejects self-follows.
- Registered `DbSet<UserFollow> UserFollows` in the existing, unified
  `FookbaseDbContext`; no new context, broker, or repository was introduced.
- Configured a composite primary key/unique directed pair, inverse lookup index,
  and both timestamp/keyset list indexes. The database check constraint also
  prevents self-follows outside the factory.
- Added unified migration `20260913203000_AddUserFollowV1`. Its two insert-select
  statements copy every canonical friendship as `A -> B` and `B -> A`, retain
  `CreatedAtUtc` as `FollowedAtUtc`, and use `ON CONFLICT DO NOTHING`.
- Added integration coverage which rolls down to the immediately preceding
  migration, inserts a real normalized friendship, migrates up, asserts the two
  directed follows and timestamp, then migrates again and asserts there are
  still exactly two. A focused factory test covers self-follow rejection.

Legacy per-module Friends migrations were not modified.

## TDD evidence

The tests were added before the entity, configuration, DbSet, migration, and
snapshot changes. The production change that the backfill test catches is a
missing direction, wrong `FollowedAtUtc`, absent migration/table, or duplicate
backfill; the self-follow test catches removal of the factory invariant.

### RED

The prescribed focused command was run immediately after writing the tests:

```bash
dotnet test backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests \
  --filter "FullyQualifiedName~Friendship_backfill"
```

It failed before compiling the newly written test because the environment's
default NuGet configuration directory is root-owned:

```text
error : Failed to read NuGet.Config due to unauthorized access.
Path: '/home/lighthouse2815/.nuget/NuGet/NuGet.Config'.
error : Access to the path '/home/lighthouse2815/.nuget/NuGet' is denied.
```

The no-restore forced rebuild then confirmed the second environment blocker
before source compilation could reach the intentionally absent `UserFollow`:

```text
error NETSDK1064: Package Microsoft.EntityFrameworkCore.Analyzers,
version 10.0.11 was not found.
```

Thus the test was genuinely red before implementation, but its exact intended
missing-model assertion could not execute in this host.

### GREEN compilation

After implementation, restore was made reproducible using the repository's
existing package cache rather than the inaccessible home cache:

```bash
HOME=/tmp/fookbase-task2-dotnet-home \
NUGET_PACKAGES=/home/lighthouse2815/Projects/light-meta/fookbase-light/.nuget/packages \
dotnet restore backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests/Fookbase.Friends.Api.IntegrationTests.csproj \
  -p:AllowMissingPrunePackageData=true --disable-parallel -v minimal

HOME=/tmp/fookbase-task2-dotnet-home \
NUGET_PACKAGES=/home/lighthouse2815/Projects/light-meta/fookbase-light/.nuget/packages \
dotnet build backend/Fookbase.Src/Tests/Friends/Fookbase.Friends.Api.IntegrationTests/Fookbase.Friends.Api.IntegrationTests.csproj \
  --no-restore -v minimal
```

Exact successful build summary:

```text
Fookbase.Api -> .../Main/bin/Debug/net10.0/Fookbase.Api.dll
Fookbase.Friends.Api.IntegrationTests -> .../Fookbase.Friends.Api.IntegrationTests.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Focused runtime and model checks

The requested focused runtime test was then rerun with the same cache. It built
both projects, but the test host cannot start because this environment contains
only `Microsoft.NETCore.App 10.0.12`, not the required shared framework
`Microsoft.AspNetCore.App 10.0.0`:

```text
Testhost process ... exited with error: You must install or update .NET ...
Framework: 'Microsoft.AspNetCore.App', version '10.0.0' (x64)
No frameworks were found.
Test Run Aborted.
```

The EF pending-model check was attempted as requested:

```bash
HOME=/tmp/fookbase-task2-dotnet-home \
NUGET_PACKAGES=/home/lighthouse2815/Projects/light-meta/fookbase-light/.nuget/packages \
dotnet ef migrations has-pending-model-changes --project Fookbase.Api.csproj --no-build
```

It is blocked by that same missing `Microsoft.AspNetCore.App 10.0.0` runtime;
therefore there is no claim that the runtime migration or pending-model check
passed in this host.

## Migration generation command

The normal unified generation command was attempted from
`backend/Fookbase.Src/Main`:

```bash
HOME=/tmp/fookbase-task2-dotnet-home \
NUGET_PACKAGES=/home/lighthouse2815/Projects/light-meta/fookbase-light/.nuget/packages \
dotnet ef migrations add AddUserFollowV1 --project Fookbase.Api.csproj
```

`dotnet tool restore` first restored `dotnet-ef 10.0.11` successfully. The
generation command could not retrieve project metadata because the repository
package cache was missing `Microsoft.CodeAnalysis.Analyzers 3.11.0`. The
migration and snapshot were consequently expressed in the same unified
attribute-based convention already used by
`20260912100000_AddGlobalSearchIndexes.cs`; this avoided changing legacy module
migrations and retained a normal migration ID/order.

## Self-review

- The directed composite key makes duplicate follows impossible; the inverse
  pair index supports reverse checks; both list orders have keyset indexes.
- Both backfill directions use quoted PostgreSQL identifiers and separate
  conflict-safe inserts, so a partially populated target table is safe.
- `Down` removes only `UserFollows`; no friendship data or historic migration
  was changed.
- `git diff --cached --check` completed with no diagnostics before commit.
- The unrelated pre-existing `README.md` modification was neither staged nor
  committed. No build artifacts, secrets, environment files, or node modules
  were staged. No push was attempted per task instruction.

## Commit

- `836a06f feat: thêm mô hình theo dõi người dùng` (local only)

## Remaining concern

Install the matching `Microsoft.AspNetCore.App 10.0.0` shared runtime (and make
the analyzer package available) in the verification environment, then rerun
the focused integration test and `has-pending-model-changes` command above.
