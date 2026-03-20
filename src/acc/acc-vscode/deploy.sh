#!/usr/bin/env bash
set -euo pipefail

# Quick deploy script for the ACC VS Code extension
# Usage: ./deploy.sh [--publish]

PUBLISH=false
if [[ "${1:-}" == "--publish" ]]; then
  PUBLISH=true
fi

ROOT_DIR="$(cd "$(dirname "$0")"/.. && pwd)"
cd "$ROOT_DIR/acc-vscode"

echo "[INFO] Building extension..."
npm ci --no-audit --no-fund
npm run compile

VSIX_NAME=""
echo "[INFO] Packaging extension (using npx @vscode/vsce)..."
if npx -y @vscode/vsce package; then
  # vsce names the file <publisher>.<name>-<version>.vsix or <name>-<version>.vsix
  VSIX_NAME=$(ls *.vsix | head -n1 || true)
  echo "[OK] Created $VSIX_NAME"
else
  echo "[ERROR] Packaging failed. Ensure @vscode/vsce is available." >&2
  exit 2
fi

if ! $PUBLISH ; then
  echo "[INFO] Built package at: $VSIX_NAME"
  echo "Run './deploy.sh --publish' to upload to GitHub release (requires gh CLI)."
  exit 0
fi

if ! command -v gh &>/dev/null; then
  echo "[ERROR] GitHub CLI (gh) not found. Install: https://cli.github.com/" >&2
  exit 1
fi

VERSION=$(node -e "console.log(require('./package.json').version)")
RELEASE_TAG="v${VERSION}"

echo "[GITHUB] Ensuring release $RELEASE_TAG exists..."
if ! gh release view "$RELEASE_TAG" &>/dev/null; then
  gh release create "$RELEASE_TAG" --title "$RELEASE_TAG" --notes "Release $RELEASE_TAG"
fi

echo "[GITHUB] Uploading $VSIX_NAME to release $RELEASE_TAG..."
gh release upload "$RELEASE_TAG" "$VSIX_NAME" --clobber
echo "[GITHUB] Upload complete."
