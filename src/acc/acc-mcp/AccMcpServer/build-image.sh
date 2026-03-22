#!/usr/bin/env bash
# Build (and optionally push) the acc-mcp Docker image.
#
# Usage:
#   ./build-image.sh [IMAGE_TAG]
#
# Default IMAGE_TAG: ghcr.io/keryxlabs/acc-mcp:latest
#
# Publishes the binary on the host first, then packages it into a minimal
# runtime-deps image. No dotnet toolchain is required inside the container.
#
# At runtime, override the ACC engine endpoint via environment variables:
#   docker run -e AccEngine__Host=<host> -e AccEngine__Port=9339 acc-mcp

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
IMAGE_TAG="${1:-ghcr.io/keryxlabs/acc-mcp:latest}"

echo "▶ Publishing binary on host..."
dotnet publish "$SCRIPT_DIR/AccMcpServer.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o "$SCRIPT_DIR/publish"

echo "▶ Building $IMAGE_TAG..."
docker build \
    -f "$SCRIPT_DIR/Dockerfile" \
    -t "$IMAGE_TAG" \
    "$REPO_ROOT"

echo "✓ Done: $IMAGE_TAG"
