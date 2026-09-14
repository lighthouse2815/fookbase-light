#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
database_container="fookbase-legacy-import-e2e-$$-${RANDOM}"
database_user="fookbase_e2e"
database_password="fookbase_e2e_password"
temporary_client_bin="$(mktemp -d)"
refusal_output="$(mktemp)"
host_user="$(id -u):$(id -g)"

cleanup() {
  docker rm --force "${database_container}" >/dev/null 2>&1 || true
  rm -rf "${temporary_client_bin}"
  rm -f "${refusal_output}"
}
trap cleanup EXIT

run_sql() {
  local database_name="$1"
  docker exec -i "${database_container}" psql \
    --set ON_ERROR_STOP=1 \
    --username "${database_user}" \
    --dbname "${database_name}"
}

create_database() {
  docker exec "${database_container}" psql \
    --set ON_ERROR_STOP=1 \
    --username "${database_user}" \
    --dbname postgres \
    --command "CREATE DATABASE \"$1\""
}

copy_table_schema() {
  local source_database="$1"
  local table_name="$2"

  docker exec "${database_container}" pg_dump \
    --username "${database_user}" \
    --dbname fookbase_db \
    --schema-only \
    --no-owner \
    --no-privileges \
    --table "public.\"${table_name}\"" |
    docker exec -i "${database_container}" psql \
      --set ON_ERROR_STOP=1 \
      --username "${database_user}" \
      --dbname "${source_database}"
}

copy_legacy_schema() {
  local source_database="$1"
  shift
  local table_name
  for table_name in "$@"; do
    copy_table_schema "${source_database}" "${table_name}"
  done
}

run_sdk() {
  docker run --rm \
    --network "container:${database_container}" \
    --user "${host_user}" \
    --env DOTNET_CLI_HOME=/tmp \
    --env NUGET_PACKAGES=/workspace/.nuget/packages \
    --volume "${repository_root}:/workspace" \
    --workdir /workspace \
    "$@"
}

printf '%s\n' 'Starting an isolated PostgreSQL server for the legacy import E2E test'
docker run --detach --rm \
  --name "${database_container}" \
  --env "POSTGRES_USER=${database_user}" \
  --env "POSTGRES_PASSWORD=${database_password}" \
  --env POSTGRES_DB=postgres \
  postgres:17-alpine >/dev/null

for attempt in $(seq 1 30); do
  if docker exec "${database_container}" pg_isready --username "${database_user}" --dbname postgres >/dev/null 2>&1; then
    break
  fi

  if [[ "${attempt}" == "30" ]]; then
    printf '%s\n' 'Timed out while waiting for the isolated PostgreSQL server.' >&2
    exit 1
  fi

  sleep 1
done

create_database fookbase_db
for database_name in legacy_identity legacy_users legacy_friends legacy_messages legacy_posts legacy_media; do
  create_database "${database_name}"
done

# The active schema now has trigram indexes for UserProfiles and Posts. pg_dump includes
# those indexes when it prepares the legacy fixtures, so enable the same PostgreSQL
# extension in the disposable source databases before copying their table schemas.
run_sql legacy_users <<SQL
CREATE EXTENSION IF NOT EXISTS pg_trgm;
SQL
run_sql legacy_posts <<SQL
CREATE EXTENSION IF NOT EXISTS pg_trgm;
SQL

target_npgsql_connection="Host=127.0.0.1;Port=5432;Database=fookbase_db;Username=${database_user};Password=${database_password}"
target_libpq_connection="host=127.0.0.1 port=5432 dbname=fookbase_db user=${database_user} password=${database_password}"

printf '%s\n' 'Applying the active FookbaseDbContext migration to the empty fookbase_db target'
run_sdk \
  --env "ConnectionStrings__FookbaseDatabase=${target_npgsql_connection}" \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c 'dotnet tool restore && dotnet tool run dotnet-ef database update --project backend/Fookbase.Src/Main --startup-project backend/Fookbase.Src/Main --context FookbaseDbContext'

copy_legacy_schema legacy_identity AspNetUsers AuthSessions RefreshTokens
copy_legacy_schema legacy_users UserProfiles
copy_legacy_schema legacy_friends Friendships BlockedUsers
copy_legacy_schema legacy_messages Stories Conversations ConversationReadCursors Messages
copy_legacy_schema legacy_posts Posts Comments PostReactions PostMedia
copy_legacy_schema legacy_media MediaAssets MediaReferences ProfileMediaReferences

timestamp='2024-01-02T03:04:05+00:00'
read_timestamp='2024-01-02T03:09:05+00:00'

run_sql legacy_identity <<SQL
INSERT INTO "AspNetUsers" (
  "Id", "CreatedAt", "IsActive", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
  "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber",
  "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount")
VALUES
  ('00000000-0000-0000-0000-000000000001', '${timestamp}', TRUE, 'legacy-alice', 'LEGACY-ALICE',
   'alice@example.test', 'ALICE@EXAMPLE.TEST', TRUE, '\$2a\$11\$legacy-password-hash-preserved',
   'legacy-security-stamp-1', 'legacy-concurrency-stamp-1', NULL, FALSE, FALSE, NULL, TRUE, 0),
  ('00000000-0000-0000-0000-000000000002', '${timestamp}', TRUE, 'legacy-bob', 'LEGACY-BOB',
   'bob@example.test', 'BOB@EXAMPLE.TEST', TRUE, '\$2a\$11\$legacy-password-hash-bob',
   'legacy-security-stamp-2', 'legacy-concurrency-stamp-2', NULL, FALSE, FALSE, NULL, TRUE, 0);
SQL

run_sql legacy_users <<SQL
INSERT INTO "UserProfiles" (
  "UserId", "Username", "DisplayName", "Bio", "AvatarUrl", "CoverUrl", "AvatarMediaId",
  "CoverMediaId", "DateOfBirth", "CurrentCity", "CreatedAt", "UpdatedAt")
VALUES
  ('00000000-0000-0000-0000-000000000001', 'legacy-alice', 'Legacy Alice', 'Imported profile',
   NULL, NULL, '00000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000022',
   NULL, 'Hanoi', '${timestamp}', '${timestamp}');
SQL

run_sql legacy_friends <<SQL
INSERT INTO "Friendships" ("Id", "UserId1", "UserId2", "CreatedAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000011',
  '00000000-0000-0000-0000-000000000001',
  '00000000-0000-0000-0000-000000000002',
  '${timestamp}');
INSERT INTO "BlockedUsers" ("BlockerUserId", "BlockedUserId", "CreatedAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000002',
  '00000000-0000-0000-0000-000000000001',
  '${timestamp}');
SQL

run_sql legacy_messages <<SQL
INSERT INTO "Conversations" ("Id", "UserId1", "UserId2", "CreatedAtUtc", "LastMessageAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000051',
  '00000000-0000-0000-0000-000000000001',
  '00000000-0000-0000-0000-000000000002',
  '${timestamp}', '${read_timestamp}');
INSERT INTO "Messages" ("Id", "ConversationId", "SenderUserId", "Content", "CreatedAtUtc", "ReadAtUtc")
VALUES
  ('00000000-0000-0000-0000-000000000061', '00000000-0000-0000-0000-000000000051',
   '00000000-0000-0000-0000-000000000001', 'legacy hello', '${timestamp}', NULL),
  ('00000000-0000-0000-0000-000000000062', '00000000-0000-0000-0000-000000000051',
   '00000000-0000-0000-0000-000000000002', 'legacy reply', '${read_timestamp}', '${read_timestamp}');
INSERT INTO "ConversationReadCursors" (
  "ConversationId", "UserId", "LastReadMessageId", "LastReadMessageCreatedAtUtc", "LastReadAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000051',
  '00000000-0000-0000-0000-000000000001',
  '00000000-0000-0000-0000-000000000062',
  '${read_timestamp}', '${read_timestamp}');
SQL

run_sql legacy_posts <<SQL
INSERT INTO "Posts" ("Id", "AuthorUserId", "Content", "Privacy", "CreatedAtUtc", "UpdatedAtUtc", "DeletedAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000031',
  '00000000-0000-0000-0000-000000000001',
  'legacy post', 0, '${timestamp}', NULL, NULL);
INSERT INTO "Comments" (
  "Id", "PostId", "AuthorUserId", "ParentCommentId", "Content", "CreatedAtUtc", "UpdatedAtUtc", "DeletedAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000041',
  '00000000-0000-0000-0000-000000000031',
  '00000000-0000-0000-0000-000000000002',
  NULL, 'legacy comment', '${timestamp}', NULL, NULL);
INSERT INTO "PostReactions" ("PostId", "UserId", "Type", "CreatedAtUtc", "UpdatedAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000031',
  '00000000-0000-0000-0000-000000000002',
  1, '${timestamp}', NULL);
INSERT INTO "PostMedia" ("PostId", "MediaId", "SortOrder")
VALUES (
  '00000000-0000-0000-0000-000000000031',
  '00000000-0000-0000-0000-000000000023',
  0);
SQL

run_sql legacy_media <<SQL
INSERT INTO "MediaAssets" (
  "Id", "OwnerUserId", "MediaType", "Status", "ObjectKey", "OriginalFileName", "ContentType",
  "DeclaredSizeBytes", "ActualSizeBytes", "CreatedAtUtc", "UploadExpiresAtUtc", "UploadedAtUtc", "DeletedAtUtc")
VALUES
  ('00000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000001',
   0, 1, 'legacy/avatar.png', 'avatar.png', 'image/png', 11, 11, '${timestamp}', NULL, '${timestamp}', NULL),
  ('00000000-0000-0000-0000-000000000022', '00000000-0000-0000-0000-000000000001',
   0, 1, 'legacy/cover.png', 'cover.png', 'image/png', 12, 12, '${timestamp}', NULL, '${timestamp}', NULL),
  ('00000000-0000-0000-0000-000000000023', '00000000-0000-0000-0000-000000000001',
   0, 1, 'legacy/post.png', 'post.png', 'image/png', 13, 13, '${timestamp}', NULL, '${timestamp}', NULL);
INSERT INTO "MediaReferences" ("MediaId", "PostId", "AttachedAtUtc")
VALUES (
  '00000000-0000-0000-0000-000000000023',
  '00000000-0000-0000-0000-000000000031',
  '${timestamp}');
INSERT INTO "ProfileMediaReferences" ("UserId", "Slot", "MediaId", "AttachedAtUtc")
VALUES
  ('00000000-0000-0000-0000-000000000001', 0, '00000000-0000-0000-0000-000000000021', '${timestamp}'),
  ('00000000-0000-0000-0000-000000000001', 1, '00000000-0000-0000-0000-000000000022', '${timestamp}');
SQL

printf '%s\n' 'Executing the production legacy import script against the disposable databases'
printf '%s\n' '#!/usr/bin/env bash' \
  'exec docker exec -i "${FOOKBASE_LEGACY_IMPORT_E2E_CONTAINER:?}" psql "$@"' \
  > "${temporary_client_bin}/psql"
printf '%s\n' '#!/usr/bin/env bash' \
  'exec docker exec -i "${FOOKBASE_LEGACY_IMPORT_E2E_CONTAINER:?}" pg_dump "$@"' \
  > "${temporary_client_bin}/pg_dump"
chmod +x "${temporary_client_bin}/psql" "${temporary_client_bin}/pg_dump"

export FOOKBASE_LEGACY_IMPORT_E2E_CONTAINER="${database_container}"
export PATH="${temporary_client_bin}:${PATH}"
export ConnectionStrings__FookbaseDatabase="${target_libpq_connection}"
export ConnectionStrings__IdentityDatabase="host=127.0.0.1 port=5432 dbname=legacy_identity user=${database_user} password=${database_password}"
export ConnectionStrings__UsersDatabase="host=127.0.0.1 port=5432 dbname=legacy_users user=${database_user} password=${database_password}"
export ConnectionStrings__FriendsDatabase="host=127.0.0.1 port=5432 dbname=legacy_friends user=${database_user} password=${database_password}"
export ConnectionStrings__MessagesDatabase="host=127.0.0.1 port=5432 dbname=legacy_messages user=${database_user} password=${database_password}"
export ConnectionStrings__PostsDatabase="host=127.0.0.1 port=5432 dbname=legacy_posts user=${database_user} password=${database_password}"
export ConnectionStrings__MediaDatabase="host=127.0.0.1 port=5432 dbname=legacy_media user=${database_user} password=${database_password}"

"${repository_root}/scripts/import-legacy-databases.sh"

printf '%s\n' 'Verifying imported data through FookbaseDbContext'
run_sdk \
  --env FOOKBASE_LEGACY_IMPORT_E2E=1 \
  --env "ConnectionStrings__FookbaseDatabase=${target_npgsql_connection}" \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test backend/Fookbase.Src/Tests/Identity/Fookbase.Identity.Api.IntegrationTests/Fookbase.Identity.Api.IntegrationTests.csproj \
    --filter FullyQualifiedName~LegacyImportE2eAssertionsTests \
    --logger 'console;verbosity=minimal'

if "${repository_root}/scripts/import-legacy-databases.sh" >"${refusal_output}" 2>&1; then
  printf '%s\n' 'The legacy import script unexpectedly accepted a non-empty fookbase_db.' >&2
  exit 1
fi
if ! grep -Fq 'fookbase_db already contains application data' "${refusal_output}"; then
  cat "${refusal_output}" >&2
  exit 1
fi

printf '%s\n' 'Legacy import E2E test passed'
