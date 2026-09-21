#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
database_container="fookbase-backend-tests-$$-${RANDOM}"
database_user="fookbase_test"
database_password="fookbase_test_password"
host_user="$(id -u):$(id -g)"

test_project="backend/Fookbase.Test/Fookbase.Test.csproj"
areas=(Identity Users Friends Messages Media Posts)

cleanup() {
  docker rm --force "${database_container}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

create_database() {
  docker exec "${database_container}" psql \
    --set ON_ERROR_STOP=1 \
    --username "${database_user}" \
    --dbname postgres \
    --command "CREATE DATABASE \"$1\"" >/dev/null
}

run_area() {
  local label="$1"
  local database_name="fookbase_test_${label,,}_$$_${RANDOM}"
  local connection_string="Host=127.0.0.1;Port=5432;Database=${database_name};Username=${database_user};Password=${database_password}"

  create_database "${database_name}"
  printf 'RUN  %s\n' "${label}"
  if ! docker run --rm \
    --network "container:${database_container}" \
    --user "${host_user}" \
    --env DOTNET_CLI_HOME=/tmp \
    --env NUGET_PACKAGES=/workspace/.nuget/packages \
    --env "ConnectionStrings__FookbaseDatabase=${connection_string}" \
    --volume "${repository_root}:/workspace" \
    --workdir /workspace \
    mcr.microsoft.com/dotnet/sdk:10.0 \
    dotnet test "${test_project}" \
      --filter "FullyQualifiedName~Fookbase.${label}.Api.IntegrationTests" \
      --logger 'console;verbosity=minimal'; then
    return 1
  fi
  printf 'PASS %s\n' "${label}"
}

printf '%s\n' 'Starting isolated PostgreSQL for sequential backend integration tests'
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
    printf '%s\n' 'Timed out waiting for isolated PostgreSQL.' >&2
    exit 1
  fi
  sleep 1
done

passed=0
for label in "${areas[@]}"; do
  if run_area "${label}"; then
    ((passed += 1))
  else
    printf 'FAIL %s\n' "${label}" >&2
    printf 'Backend integration summary: %s/%s areas passed.\n' "${passed}" "${#areas[@]}" >&2
    exit 1
  fi
done

printf 'Backend integration summary: %s/%s areas passed.\n' "${passed}" "${#areas[@]}"
