#!/usr/bin/env bash
set -euo pipefail

usage() {
  printf 'Usage: %s --source <directory> --target-bucket <bucket> --yes-restore\n' "$0" >&2
  exit 2
}

source_directory=""
target_bucket=""
confirmed=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --source) source_directory="${2:-}"; shift 2 ;;
    --target-bucket) target_bucket="${2:-}"; shift 2 ;;
    --yes-restore) confirmed=true; shift ;;
    *) usage ;;
  esac
done

[[ -n "${source_directory}" && -d "${source_directory}" && -n "${target_bucket}" && "${confirmed}" == true ]] || usage
: "${MINIO_ENDPOINT:?Set MINIO_ENDPOINT, for example http://minio:9000.}"
: "${MINIO_ACCESS_KEY:?Set MINIO_ACCESS_KEY.}"
: "${MINIO_SECRET_KEY:?Set MINIO_SECRET_KEY.}"

network="${MINIO_DOCKER_NETWORK:-host}"
docker run --rm \
  --network "${network}" \
  --volume "${source_directory}:/restore:ro" \
  --env "MINIO_ENDPOINT=${MINIO_ENDPOINT}" \
  --env "MINIO_ACCESS_KEY=${MINIO_ACCESS_KEY}" \
  --env "MINIO_SECRET_KEY=${MINIO_SECRET_KEY}" \
  --env "MINIO_TARGET_BUCKET=${target_bucket}" \
  minio/mc:RELEASE.2025-08-13T08-35-41Z \
  sh -c 'set -eu
    mc alias set target "$MINIO_ENDPOINT" "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" >/dev/null
    mc mb --ignore-existing "target/$MINIO_TARGET_BUCKET" >/dev/null
    mc mirror --overwrite /restore "target/$MINIO_TARGET_BUCKET"'

printf 'MinIO restore completed for bucket %s\n' "${target_bucket}"
