#!/bin/bash
set -euo pipefail

APP_NAME="paper-legends-api"

echo ""
echo "Starting deployment process for Paper Legends API..."
echo ""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "${SCRIPT_DIR}"

# Check if built app exists
if [ ! -f "dist/server.js" ]; then
  echo "Error: built server entry not found at ${SCRIPT_DIR}/dist/server.js"
  echo "Please build locally and sync the dist directory to the server before deploying."
  exit 1
fi

echo "Restarting ${APP_NAME} with PM2..."
if [ -f "ecosystem.config.js" ]; then
  if pm2 describe "${APP_NAME}" > /dev/null 2>&1; then
    echo "Reloading existing PM2 app: ${APP_NAME}"
  else
    echo "Starting new PM2 app: ${APP_NAME}"
  fi

  export DS_PORT_START=${DS_PORT_START:-27200}
  export DS_PORT_END=${DS_PORT_END:-27299}
  export DS_CONTAINER_PORT=${DS_CONTAINER_PORT:-27015}
  echo "Using port range: ${DS_PORT_START} - ${DS_PORT_END} (container port: ${DS_CONTAINER_PORT})"
  pm2 startOrReload ecosystem.config.js --update-env
  pm2 save
else
  echo "PM2 config not found, skipping restart."
fi

echo ""
echo "Deploy completed successfully for ${APP_NAME}."
