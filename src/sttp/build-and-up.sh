#!/usr/bin/env bash
# Build the gateway image and bring the full stack up.
#
# Usage:
#   ./build-and-up.sh          # build + start detached
#   ./build-and-up.sh logs     # start and follow logs

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "▶ Building sttp-gateway image..."
bash "$SCRIPT_DIR/sttp-gateway/build-image.sh" sttp-gateway:local

echo "▶ Building sttp-ui image..."
bash "$SCRIPT_DIR/sttp-ui/build-image.sh" sttp-ui:local

echo "▶ Starting stack..."
if [[ "${1:-}" == "logs" ]]; then
    docker compose -f "$SCRIPT_DIR/docker-compose.yml" up --build
else
    docker compose -f "$SCRIPT_DIR/docker-compose.yml" up --build -d
    echo ""
    echo "✓ Stack is up:"
    echo "  UI      → http://$(hostname -I | awk '{print $1}'):5000"
    echo "  Gateway → http://$(hostname -I | awk '{print $1}'):8080"
fi
