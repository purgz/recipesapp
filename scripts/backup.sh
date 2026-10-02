#!/bin/sh
# Nightly backup of the RecipeApp data (SQLite + uploaded images).
# Runs on the VM host. Wire into cron / systemd timers:
#   crontab -e
#   5 3 * * * /opt/recipeapp/scripts/backup.sh >> /opt/recipeapp/backups/backup.log 2>&1
# Optional Azure Blob upload: set AZURE_STORAGE_ACCOUNT/AZURE_STORAGE_KEY or
# CONNECTION_STRING in the environment (or in /opt/recipeapp/.backup-env).
set -eu
cd "$(dirname "$0")/.."

mkdir -p backups
STAMP="$(date +%F)"

# SQLite: copy while running (WAL safe enough for nightly household backups.
# VACUUM INTO produces a single consistent file when sqlite3 exists in image;
# plain file copy otherwise.)
docker compose exec -T api sh -c \
  'command -v sqlite3 >/dev/null 2>&1 && sqlite3 /app/App_Data/app.db "VACUUM INTO /tmp/app.db" || cp /app/App_Data/app.db /tmp/app.db' \
  2>/dev/null || echo "warn: db copy fallback ran"
docker compose cp api:/tmp/app.db "backups/app-$STAMP.db"
docker compose exec -T api rm -f /tmp/app.db

# Uploaded images
docker compose cp api:/app/App_Data/images "backups/images-$STAMP" 2>/dev/null || echo "warn: no images dir"

tar -czf "backups/recipe-app-$STAMP.tar.gz" -C backups "."
rm -rf "backups/app-$STAMP.db" "backups/images-$STAMP"

# Keep the last 14 days
find backups -name 'recipe-app-*.tar.gz' -mtime +14 -delete 2>/dev/null || true

# Optional: push to Azure Blob (az cli + env vars)
if [ -f .backup-env ]; then
  . ./.backup-env
fi
if [ -n "${AZURE_STORAGE_CONNECTION_STRING:-}" ] && command -v az >/dev/null 2>&1; then
  az storage blob upload \
    --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
    --container-name recipeapp-backups \
    --name "recipe-app-$STAMP.tar.gz" \
    --file "backups/recipe-app-$STAMP.tar.gz" \
    --overwrite >/dev/null
fi

echo "backup ok: recipe-app-$STAMP.tar.gz"
