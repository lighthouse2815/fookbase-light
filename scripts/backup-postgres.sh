#!/usr/bin/env bash
set -euo pipefail

: "${BACKUP_DIR:?Set BACKUP_DIR to a directory outside the repository.}"
: "${POSTGRES_USER:?Set POSTGRES_USER.}"
: "${POSTGRES_DB:?Set POSTGRES_DB.}"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_file="${BACKUP_DIR%/}/${POSTGRES_DB}-${timestamp}.dump"

mkdir -p "${BACKUP_DIR}"
umask 077
docker compose exec -T postgres pg_dump \
  --username "${POSTGRES_USER}" \
  --format=custom \
  --no-owner \
  --no-privileges \
  "${POSTGRES_DB}" > "${backup_file}"

printf 'PostgreSQL backup written to %s\n' "${backup_file}"
