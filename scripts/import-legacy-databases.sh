#!/usr/bin/env bash
set -euo pipefail

required_variables=(
  ConnectionStrings__FookbaseDatabase
  ConnectionStrings__IdentityDatabase
  ConnectionStrings__UsersDatabase
  ConnectionStrings__FriendsDatabase
  ConnectionStrings__MessagesDatabase
  ConnectionStrings__PostsDatabase
  ConnectionStrings__MediaDatabase
)

for variable_name in "${required_variables[@]}"; do
  if [[ -z "${!variable_name:-}" ]]; then
    printf 'Missing required environment variable: %s\n' "$variable_name" >&2
    exit 1
  fi
done

target_connection_string="$ConnectionStrings__FookbaseDatabase"
source_connection_strings=(
  "$ConnectionStrings__IdentityDatabase"
  "$ConnectionStrings__UsersDatabase"
  "$ConnectionStrings__FriendsDatabase"
  "$ConnectionStrings__MessagesDatabase"
  "$ConnectionStrings__PostsDatabase"
  "$ConnectionStrings__MediaDatabase"
)

target_database="$(psql "$target_connection_string" --tuples-only --no-align --quiet \
  --command 'SELECT current_database()')"
if [[ "$target_database" != "fookbase_db" ]]; then
  printf 'The target database must be fookbase_db; received %s.\n' "$target_database" >&2
  exit 1
fi

target_rows="$(psql "$target_connection_string" --tuples-only --no-align --quiet --command "
  SELECT COUNT(*)
  FROM \"AspNetUsers\"
  UNION ALL SELECT COUNT(*) FROM \"UserProfiles\"
  UNION ALL SELECT COUNT(*) FROM \"FriendRequests\"
  UNION ALL SELECT COUNT(*) FROM \"Conversations\"
  UNION ALL SELECT COUNT(*) FROM \"Posts\"
  UNION ALL SELECT COUNT(*) FROM \"MediaAssets\";")"
if [[ "$(printf '%s\n' "$target_rows" | awk '{ total += $1 } END { print total }')" != "0" ]]; then
  printf 'fookbase_db already contains application data; import refuses to overwrite it.\n' >&2
  exit 1
fi

printf '%s\n' 'Preflight: source row counts'
for source_connection_string in "${source_connection_strings[@]}"; do
  psql "$source_connection_string" --tuples-only --no-align --quiet --command "
    SELECT current_database() || ': ' ||
      COALESCE(SUM(reltuples)::bigint, 0)::text
    FROM pg_class
    WHERE relkind = 'r'
      AND relnamespace = 'public'::regnamespace
      AND relname <> '__EFMigrationsHistory';"
done

printf '%s\n' 'Importing legacy data into fookbase_db'
{
  printf '%s\n' 'BEGIN;'
  for source_connection_string in "${source_connection_strings[@]}"; do
    pg_dump "$source_connection_string" \
      --data-only \
      --no-owner \
      --no-privileges \
      --exclude-table=public.__EFMigrationsHistory
  done
  printf '%s\n' "UPDATE public.\"Posts\" SET \"ContainerType\" = 0, \"ContainerId\" = \"AuthorUserId\" WHERE \"ContainerId\" = '00000000-0000-0000-0000-000000000000';"
  printf '%s\n' 'COMMIT;'
} | psql "$target_connection_string" --set ON_ERROR_STOP=1 --quiet

printf '%s\n' 'Legacy import completed. Verify row counts before switching the application connection string.'
