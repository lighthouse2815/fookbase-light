#!/usr/bin/env bash
set -euo pipefail

usage() {
  printf 'Usage: %s --file <backup.dump> --target-database <database> --yes-restore\n' "$0" >&2
  exit 2
}

backup_file=""
target_database=""
confirmed=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --file) backup_file="${2:-}"; shift 2 ;;
    --target-database) target_database="${2:-}"; shift 2 ;;
    --yes-restore) confirmed=true; shift ;;
    *) usage ;;
  esac
done

[[ -n "${backup_file}" && -f "${backup_file}" && -n "${target_database}" && "${confirmed}" == true ]] || usage
: "${POSTGRES_USER:?Set POSTGRES_USER.}"

printf 'Restoring %s into PostgreSQL database %s.\n' "${backup_file}" "${target_database}"
docker compose exec -T postgres pg_restore \
  --username "${POSTGRES_USER}" \
  --dbname "${target_database}" \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges < "${backup_file}"

printf 'PostgreSQL restore completed for %s\n' "${target_database}"
