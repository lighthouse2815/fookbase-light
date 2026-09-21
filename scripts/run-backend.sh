#!/usr/bin/env bash
set -eo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
env_file="${repository_root}/.env"

if [[ ! -f "${env_file}" ]]; then
  printf 'Không tìm thấy %s. Hãy tạo từ .env.example và điền cấu hình.\n' "${env_file}" >&2
  exit 1
fi

set -a
source "${env_file}"
set +a

export Database__ApplyMigrationsOnStartup=true
cd "${repository_root}"
exec dotnet run --project backend/Fookbase.Src/Fookbase.Api.csproj --launch-profile http "$@"
