#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
migrations_directory="${repository_root}/backend/Fookbase.Src/Main/Code/Persistence/Migrations"
migration_time_zone="${MIGRATION_TIME_ZONE:-Asia/Ho_Chi_Minh}"
now="$(TZ="${migration_time_zone}" date +%Y%m%d%H%M%S)"

mapfile -t migration_ids < <(find "${migrations_directory}" -maxdepth 1 -type f \
  -name '*.cs' ! -name '*.Designer.cs' -printf '%f\n' | \
  sed -nE 's/^([0-9]{14})_.*/\1/p' | sort)

[[ ${#migration_ids[@]} -gt 0 ]] || { printf '%s\n' 'No EF migrations found.' >&2; exit 1; }

previous=""
for migration_id in "${migration_ids[@]}"; do
  if [[ "${migration_id}" == "${previous}" ]]; then
    printf 'Duplicate EF migration ID: %s\n' "${migration_id}" >&2
    exit 1
  fi
  if [[ "${migration_id}" > "${now}" ]]; then
    printf 'Future-dated EF migration ID: %s (current local time: %s)\n' "${migration_id}" "${now}" >&2
    exit 1
  fi
  previous="${migration_id}"
done

if [[ "${CHECK_EF_MODEL:-false}" == "true" ]]; then
  : "${ConnectionStrings__FookbaseDatabase:?Set ConnectionStrings__FookbaseDatabase for the EF model check.}"
  cd "${repository_root}"
  dotnet tool restore
  # Integration tests run in a container and can leave project.assets.json
  # pointing at its /workspace NuGet cache. Restore again on this runner so
  # dotnet-ef resolves packages from the current environment.
  dotnet restore backend/Fookbase.Src/Main/Fookbase.Api.csproj
  dotnet tool run dotnet-ef migrations has-pending-model-changes \
    --project backend/Fookbase.Src/Main/Fookbase.Api.csproj \
    --startup-project backend/Fookbase.Src/Main/Fookbase.Api.csproj
fi

printf 'EF migration IDs are unique, chronological, and not future-dated.\n'
