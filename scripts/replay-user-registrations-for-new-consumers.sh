#!/bin/sh
set -eu

if [ "${1:-}" != "--confirm" ]; then
  echo "Development only: requeue all UserRegistered outbox messages."
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
        WHERE \"Type\" = '\''identity.user.registered.v1'\'';"'

echo "UserRegistered messages were requeued. Keep Identity, Users, Friends, Posts and RabbitMQ running until all projections catch up."
