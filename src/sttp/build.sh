#!/usr/bin/env bash
# STTP master build wrapper.
#
# Orchestrates existing per-project build/build-image scripts while allowing
# per-project version overrides for mixed release scenarios.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

MODE="all"
STACK="all"
TARGETS_RAW=""

DEFAULT_VERSION=""
MCP_VERSION=""
GATEWAY_VERSION=""
UI_VERSION=""
GATEWAY_RS_VERSION=""

IMAGE_PREFIX="ghcr.io/keryxlabs"
LOCAL_IMAGE_TAGS=false
PUBLISH=false
DRY_RUN=false

usage() {
  cat <<'EOF'
Usage:
  ./build.sh [options]

Modes:
  --mode release|images|all   Build release archives, Docker images, or both (default: all)

Targeting:
  --stack all|dotnet|web|rust Default target set (default: all)
  --targets a,b,c             Explicit targets (overrides --stack)

Valid targets:
  mcp, gateway, ui, gateway-rs

Version controls:
  --default-version X.Y.Z
  --mcp-version X.Y.Z
  --gateway-version X.Y.Z
  --ui-version X.Y.Z
  --gateway-rs-version X.Y.Z

Image controls:
  --image-prefix ghcr.io/keryxlabs   Prefix for non-local image tags
  --local-image-tags                 Use local tags like sttp-mcp:1.2.3

Other:
  --publish    Forward --publish into per-project release build.sh scripts
  --dry-run    Print commands without executing them
  -h, --help   Show help

Examples:
  # Full release+images using one shared version
  ./build.sh --default-version 1.2.4

  # Mixed versions with explicit targets
  ./build.sh --mode all --targets mcp,gateway-rs --mcp-version 1.2.4 --gateway-rs-version 1.3.0

  # Only Docker images with local tags for fast validation
  ./build.sh --mode images --stack web --default-version 1.2.4 --local-image-tags

  # Publish release archives for dotnet hosts
  ./build.sh --mode release --stack dotnet --default-version 1.2.4 --publish
EOF
}

die() {
  echo "error: $*" >&2
  exit 1
}

run_in_dir() {
  local dir="$1"
  shift
  if $DRY_RUN; then
    printf '[DRY-RUN] (cd %s &&' "$dir"
    for arg in "$@"; do
      printf ' %q' "$arg"
    done
    echo ")"
    return 0
  fi

  (
    cd "$dir"
    "$@"
  )
}

add_target_if_missing() {
  local candidate="$1"
  local existing
  for existing in "${TARGETS[@]:-}"; do
    if [[ "$existing" == "$candidate" ]]; then
      return 0
    fi
  done
  TARGETS+=("$candidate")
}

expand_and_add_target() {
  local token="$1"
  case "$token" in
    all)
      add_target_if_missing "mcp"
      add_target_if_missing "gateway"
      add_target_if_missing "ui"
      add_target_if_missing "gateway-rs"
      ;;
    dotnet)
      add_target_if_missing "mcp"
      add_target_if_missing "gateway"
      add_target_if_missing "ui"
      ;;
    web)
      add_target_if_missing "gateway"
      add_target_if_missing "ui"
      ;;
    rust)
      add_target_if_missing "gateway-rs"
      ;;
    mcp|gateway|ui|gateway-rs)
      add_target_if_missing "$token"
      ;;
    *)
      die "Unknown target or stack token: $token"
      ;;
  esac
}

resolve_version_for_target() {
  local target="$1"
  case "$target" in
    mcp)
      echo "${MCP_VERSION:-${DEFAULT_VERSION:-1.2.3}}"
      ;;
    gateway)
      echo "${GATEWAY_VERSION:-${DEFAULT_VERSION:-1.2.3}}"
      ;;
    ui)
      echo "${UI_VERSION:-${DEFAULT_VERSION:-1.2.3}}"
      ;;
    gateway-rs)
      echo "${GATEWAY_RS_VERSION:-${DEFAULT_VERSION:-1.2.3}}"
      ;;
    *)
      die "Unknown target in version resolver: $target"
      ;;
  esac
}

image_name_for_target() {
  case "$1" in
    mcp) echo "sttp-mcp" ;;
    gateway) echo "sttp-gateway" ;;
    ui) echo "sttp-ui" ;;
    gateway-rs) echo "sttp-gateway-rs" ;;
    *) die "Unknown target in image resolver: $1" ;;
  esac
}

build_image_tag() {
  local target="$1"
  local version="$2"
  local image_name
  image_name="$(image_name_for_target "$target")"

  if $LOCAL_IMAGE_TAGS; then
    echo "${image_name}:${version}"
    return 0
  fi

  local prefix="${IMAGE_PREFIX%/}"
  echo "${prefix}/${image_name}:${version}"
}

run_release_for_target() {
  local target="$1"
  local version="$2"

  case "$target" in
    mcp)
      if $PUBLISH; then
        run_in_dir "$SCRIPT_DIR/sttp-mcp" env STTP_MCP_VERSION="$version" STTP_VERSION="$version" bash ./build.sh --publish
      else
        run_in_dir "$SCRIPT_DIR/sttp-mcp" env STTP_MCP_VERSION="$version" STTP_VERSION="$version" bash ./build.sh
      fi
      ;;
    gateway)
      if $PUBLISH; then
        run_in_dir "$SCRIPT_DIR/sttp-gateway" env STTP_GATEWAY_VERSION="$version" STTP_VERSION="$version" bash ./build.sh --publish
      else
        run_in_dir "$SCRIPT_DIR/sttp-gateway" env STTP_GATEWAY_VERSION="$version" STTP_VERSION="$version" bash ./build.sh
      fi
      ;;
    ui)
      if $PUBLISH; then
        run_in_dir "$SCRIPT_DIR/sttp-ui" env STTP_UI_VERSION="$version" STTP_VERSION="$version" bash ./build.sh --publish
      else
        run_in_dir "$SCRIPT_DIR/sttp-ui" env STTP_UI_VERSION="$version" STTP_VERSION="$version" bash ./build.sh
      fi
      ;;
    gateway-rs)
      echo "[INFO] Skipping release mode for gateway-rs: no release build.sh script is defined (use --mode images)."
      ;;
    *)
      die "Unknown target in release mode: $target"
      ;;
  esac
}

run_images_for_target() {
  local target="$1"
  local version="$2"
  local image_tag
  image_tag="$(build_image_tag "$target" "$version")"

  case "$target" in
    mcp)
      run_in_dir "$SCRIPT_DIR/sttp-mcp" bash ./build-image.sh "$image_tag"
      ;;
    gateway)
      run_in_dir "$SCRIPT_DIR/sttp-gateway" bash ./build-image.sh "$image_tag"
      ;;
    ui)
      run_in_dir "$SCRIPT_DIR/sttp-ui" bash ./build-image.sh "$image_tag"
      ;;
    gateway-rs)
      run_in_dir "$SCRIPT_DIR/sttp-gateway-rs" bash ./build-image.sh "$image_tag"
      ;;
    *)
      die "Unknown target in image mode: $target"
      ;;
  esac
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)
      [[ $# -ge 2 ]] || die "Missing value for --mode"
      MODE="$2"
      shift 2
      ;;
    --stack)
      [[ $# -ge 2 ]] || die "Missing value for --stack"
      STACK="$2"
      shift 2
      ;;
    --targets)
      [[ $# -ge 2 ]] || die "Missing value for --targets"
      TARGETS_RAW="$2"
      shift 2
      ;;
    --default-version)
      [[ $# -ge 2 ]] || die "Missing value for --default-version"
      DEFAULT_VERSION="$2"
      shift 2
      ;;
    --mcp-version)
      [[ $# -ge 2 ]] || die "Missing value for --mcp-version"
      MCP_VERSION="$2"
      shift 2
      ;;
    --gateway-version)
      [[ $# -ge 2 ]] || die "Missing value for --gateway-version"
      GATEWAY_VERSION="$2"
      shift 2
      ;;
    --ui-version)
      [[ $# -ge 2 ]] || die "Missing value for --ui-version"
      UI_VERSION="$2"
      shift 2
      ;;
    --gateway-rs-version)
      [[ $# -ge 2 ]] || die "Missing value for --gateway-rs-version"
      GATEWAY_RS_VERSION="$2"
      shift 2
      ;;
    --image-prefix)
      [[ $# -ge 2 ]] || die "Missing value for --image-prefix"
      IMAGE_PREFIX="$2"
      shift 2
      ;;
    --local-image-tags)
      LOCAL_IMAGE_TAGS=true
      shift
      ;;
    --publish)
      PUBLISH=true
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      die "Unknown option: $1"
      ;;
  esac
done

case "$MODE" in
  release|images|all)
    ;;
  *)
    die "Invalid --mode '$MODE' (expected: release, images, all)"
    ;;
esac

case "$STACK" in
  all|dotnet|web|rust)
    ;;
  *)
    die "Invalid --stack '$STACK' (expected: all, dotnet, web, rust)"
    ;;
esac

declare -a TARGETS=()

if [[ -n "$TARGETS_RAW" ]]; then
  IFS=',' read -r -a target_tokens <<< "$TARGETS_RAW"
  for token in "${target_tokens[@]}"; do
    normalized="$(echo "$token" | tr -d '[:space:]')"
    [[ -n "$normalized" ]] || continue
    expand_and_add_target "$normalized"
  done
else
  expand_and_add_target "$STACK"
fi

if [[ ${#TARGETS[@]} -eq 0 ]]; then
  die "No targets resolved."
fi

echo "[INFO] Mode: $MODE"
echo "[INFO] Targets: ${TARGETS[*]}"
for target in "${TARGETS[@]}"; do
  version="$(resolve_version_for_target "$target")"
  echo "[INFO] - $target => version $version"
done

if [[ "$MODE" == "release" || "$MODE" == "all" ]]; then
  echo "[INFO] Running release builds..."
  for target in "${TARGETS[@]}"; do
    version="$(resolve_version_for_target "$target")"
    run_release_for_target "$target" "$version"
  done
fi

if [[ "$MODE" == "images" || "$MODE" == "all" ]]; then
  echo "[INFO] Running image builds..."
  for target in "${TARGETS[@]}"; do
    version="$(resolve_version_for_target "$target")"
    run_images_for_target "$target" "$version"
  done
fi

echo "[INFO] Done."
