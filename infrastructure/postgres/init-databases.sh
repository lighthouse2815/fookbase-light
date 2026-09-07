#!/bin/sh
set -eu

psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set=db_owner="$POSTGRES_USER" <<-'EOSQL'
SELECT format('CREATE DATABASE users_db OWNER %I', :'db_owner')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'users_db')\gexec
SELECT format('CREATE DATABASE friends_db OWNER %I', :'db_owner')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'friends_db')\gexec
SELECT format('CREATE DATABASE posts_db OWNER %I', :'db_owner')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'posts_db')\gexec
SELECT format('CREATE DATABASE media_db OWNER %I', :'db_owner')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'media_db')\gexec
EOSQL
