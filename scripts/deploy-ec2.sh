#!/usr/bin/env bash
set -Eeuo pipefail

app_dir="${APP_DIR:-/srv/fookbase-light}"
env_file="${ENV_FILE:-$app_dir/.env.production}"
compose_files=(
  -f "$app_dir/compose.yml"
  -f "$app_dir/compose.prod.yml"
  -f "$app_dir/compose.ec2.yml"
)

if [[ ! -r "$env_file" ]]; then
  echo "Missing production environment file: $env_file" >&2
  exit 1
fi

cd "$app_dir"

compose() {
  sudo docker compose --env-file "$env_file" "${compose_files[@]}" "$@"
}

compose config --quiet

if sudo docker image inspect fookbase-light-api:production >/dev/null 2>&1; then
  sudo docker tag fookbase-light-api:production fookbase-light-api:previous
fi

# Untagged images pin old build-cache layers on the small EC2 root disk.
sudo docker image prune --force
sudo docker buildx prune --all --force --max-used-space 1GB

sudo docker build -t fookbase-light-api:production -f backend/Fookbase.Src/Dockerfile .

set -a
# shellcheck disable=SC1090
source "$env_file"
set +a

database_connection="Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
migration_env_file="$(mktemp)"
trap 'shred -u -- "$migration_env_file"' EXIT
umask 077
printf '%s\n' "ConnectionStrings__FookbaseDatabase=$database_connection" > "$migration_env_file"

sudo docker run --rm --network fookbase-light_default --env-file "$migration_env_file" \
  -v "$app_dir:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -lc 'dotnet tool restore && dotnet restore backend/Fookbase.Src/Fookbase.Api.csproj && dotnet tool run dotnet-ef database update --project backend/Fookbase.Src --startup-project backend/Fookbase.Src'

compose up -d --no-deps --force-recreate api

host="${AllowedHosts%%[;,]*}"
if [[ -z "$host" ]]; then
  echo "AllowedHosts must contain the public host used for the health check." >&2
  exit 1
fi

for attempt in $(seq 1 30); do
  if curl --fail --silent --show-error --header "Host: $host" http://127.0.0.1:5000/health/ready >/dev/null; then
    echo "Deployment is ready."
    exit 0
  fi

  sleep 2
done

echo "API did not become ready after deployment." >&2
exit 1
