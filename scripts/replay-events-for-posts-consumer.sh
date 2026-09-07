#!/bin/sh
set -eu

if [ "${1:-}" != "--confirm" ]; then
  echo "Development only: requeue registration and relationship events for Posts."
  echo "Usage: $0 --confirm"
  exit 1
fi

docker compose exec -T postgres sh -lc '
  psql -v ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname identity_db \
    -c "UPDATE \"OutboxMessages\"
        SET \"ProcessedAtUtc\" = NULL,
            \"RetryCount\" = 0,
            \"LastError\" = NULL
        WHERE \"Type\" = '\''identity.user.registered.v1'\'';"

  psql -v ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname friends_db \
    -c "UPDATE \"OutboxMessages\"
        SET \"ProcessedAtUtc\" = NULL,
            \"RetryCount\" = 0,
            \"LastError\" = NULL
        WHERE \"Type\" IN (
            '\''friends.request.accepted.v1'\'',
            '\''friends.friendship.removed.v1'\'',
            '\''friends.user.blocked.v1'\'',
            '\''friends.user.unblocked.v1'\'');"'

echo "Posts source events were requeued. Keep Identity, Friends, Posts and RabbitMQ running until projections catch up."
