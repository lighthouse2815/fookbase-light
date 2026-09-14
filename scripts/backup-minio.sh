#!/usr/bin/env bash
set -euo pipefail

: "${MINIO_ENDPOINT:?Set MINIO_ENDPOINT, for example http://minio:9000.}"
: "${MINIO_ACCESS_KEY:?Set MINIO_ACCESS_KEY.}"
: "${MINIO_SECRET_KEY:?Set MINIO_SECRET_KEY.}"
: "${MINIO_BUCKET:?Set MINIO_BUCKET.}"
: "${MINIO_BACKUP_DIR:?Set MINIO_BACKUP_DIR outside the repository.}"

mkdir -p "${MINIO_BACKUP_DIR}"
network="${MINIO_DOCKER_NETWORK:-host}"
docker run --rm \
  --network "${network}" \
  --volume "${MINIO_BACKUP_DIR}:/backup" \
  --env "MINIO_ENDPOINT=${MINIO_ENDPOINT}" \
  --env "MINIO_ACCESS_KEY=${MINIO_ACCESS_KEY}" \
  --env "MINIO_SECRET_KEY=${MINIO_SECRET_KEY}" \
  --env "MINIO_BUCKET=${MINIO_BUCKET}" \
  minio/mc:RELEASE.2025-08-13T08-35-41Z \
  sh -c 'set -eu
    mc alias set source "$MINIO_ENDPOINT" "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null
    mc mirror --overwrite "source/$MINIO_BUCKET" "/backup/$MINIO_BUCKET"'

printf 'MinIO bucket %s mirrored to %s\n' "${MINIO_BUCKET}" "${MINIO_BACKUP_DIR}"
