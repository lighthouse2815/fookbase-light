# EF Core migration history

Only one migration source is active at runtime:

```text
backend/Fookbase.Src/Main/Code/Persistence/Migrations/
```

`FookbaseDbContext` uses the consolidated baseline migration
`20260910143327_InitialFookbase`, followed by
`20260910154744_AddNotifications`, `20260910162758_AddFeedPostIndex`, and
`20260910165428_AddGroups`. `AddFeedPostIndex` adds only the partial active-post keyset index
used by the read-only Feed query module; `AddGroups` adds Groups tables, the post container
columns/index, and a safe backfill of existing profile posts to their author container. New
runtime migrations must be created in that directory with
`FookbaseDbContext`; the baseline ID and its generated snapshot are not regenerated as part of
ordinary maintenance.

The older module-specific migration files remain in:

```text
backend/Fookbase.Src/Main/Code/Modules/<Module>/Data/Migrations/
```

They are audit and legacy-import history only. `Fookbase.Api.csproj` excludes those
source files from compilation, so EF Core cannot discover or apply them at runtime.
They must not be deleted or have their migration IDs rewritten: the import process
uses their table history to understand legacy database layouts.
