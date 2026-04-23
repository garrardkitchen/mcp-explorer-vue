#!/usr/bin/env bash
# One-time Dev Tunnels sign-in for the API container.
#
# Microsoft's Dev Tunnels service requires the host process to be
# authenticated before it will issue tunnel URLs, even when clients
# connect anonymously via `--allow-anonymous`. Running this script
# once persists the credential in the `devtunnels-cli` Docker volume
# (or your mounted HOST_DEVTUNNELS_DIR) so that future tunnel starts
# skip the login dialog entirely.

set -euo pipefail

cd "$(dirname "$0")/.."

service="${DEVTUNNEL_SERVICE:-api}"

if ! docker compose ps --services --filter "status=running" | grep -qx "$service"; then
  echo "▶ Starting '$service' container so we can sign in…"
  docker compose up -d "$service"
fi

echo
echo "▶ Running: devtunnel user login --use-device-code-auth"
echo "  Follow the prompt — open the URL, enter the code, then return here."
echo

docker compose exec "$service" devtunnel user login --use-device-code-auth

echo
echo "▶ Sign-in complete. Current user:"
docker compose exec "$service" devtunnel user show || true

echo
echo "✔ You can now create and start Dev Tunnels from the UI without further prompts."
