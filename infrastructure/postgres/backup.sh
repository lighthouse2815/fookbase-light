#!/bin/sh
set -eu

: "${BACKUP_DIR:?Set BACKUP_DIR to a directory outside the repository.}"

timestamp=$(date -u +%Y%m%dT%H%M%SZ)
databases="${BACKUP_DATABASES:-fookbase_db}"

mkdir -p "$BACKUP_DIR"
for database in $databases; do
  output="$BACKUP_DIR/${database}-${timestamp}.dump"
  docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" --format=custom "$database" > "$output"
  printf 'Backed up %s to %s\n' "$database" "$output"
done
