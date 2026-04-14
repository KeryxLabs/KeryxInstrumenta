#!/usr/bin/env bash
# Build (and optionally push) the sttp-ui Docker image.
#
# Usage:
#   ./build-image.sh [IMAGE_TAG]
#
# Default IMAGE_TAG: ghcr.io/keryxlabs/sttp-ui:1.2.4
#
# Publishes the app on the host first, then packages it into a slim
# aspnet runtime image. No dotnet toolchain required inside the container.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
IMAGE_TAG="${1:-ghcr.io/keryxlabs/sttp-ui:1.2.5}"

echo "▶ Publishing on host..."
dotnet publish "$SCRIPT_DIR/sttp-ui.csproj" \
    -c Release \
    -o "$SCRIPT_DIR/publish"

echo "▶ Building $IMAGE_TAG..."
docker build \
    -f "$SCRIPT_DIR/Dockerfile" \
    -t "$IMAGE_TAG" \
    "$REPO_ROOT"

echo "✓ Done: $IMAGE_TAG"
