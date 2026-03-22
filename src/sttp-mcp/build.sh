# Build for each platform

# Usage: ./build.sh [--publish]
set -e

VERSION="0.1.0"
TAG_PREFIX="sttp-mcp"
RELEASE="${TAG_PREFIX}/v$VERSION"
NAME="sttp-mcp"
RIDS=(osx-arm64 osx-x64 linux-x64 linux-arm64 linux-musl-x64 win-x64 win-arm64)

PUBLISH=false
if [[ "$1" == "--publish" ]]; then
	PUBLISH=true
fi

# Track successful builds so packaging/upload only handles artifacts that exist.
BUILT_RIDS=()

run_publish() {
	local RID="$1"
	echo "[BUILD] dotnet publish -r $RID ..."
	if dotnet publish -c Release -r "$RID" \
		-p:PublishSingleFile=true \
		-p:SelfContained=true \
		-p:EnableCompressionInSingleFile=true \
		-p:PublishReadyToRun=false \
		-p:DebugType=none \
		-p:DebugSymbols=false \
		-p:IncludeNativeLibrariesForSelfExtract=true; then
		BUILT_RIDS+=("$RID")
	else
		echo "[WARN] Build failed for $RID, skipping."
	fi
}

run_all_builds() {
	echo "[BUILD] Building for all platforms..."
	for RID in "${RIDS[@]}"; do
		run_publish "$RID"
	done
}

package_artifact() {
	local RID="$1"
	case "$RID" in
		osx-arm64)
			tar -czf "${NAME}-${VERSION}-macos-arm64.tar.gz" -C "bin/Release/net10.0/osx-arm64/publish" . && echo "  [OK] macos-arm64"
			;;
		osx-x64)
			tar -czf "${NAME}-${VERSION}-macos-x64.tar.gz" -C "bin/Release/net10.0/osx-x64/publish" . && echo "  [OK] macos-x64"
			;;
		linux-x64)
			tar -czf "${NAME}-${VERSION}-linux-x64.tar.gz" -C "bin/Release/net10.0/linux-x64/publish" . && echo "  [OK] linux-x64"
			;;
		linux-arm64)
			tar -czf "${NAME}-${VERSION}-linux-arm64.tar.gz" -C "bin/Release/net10.0/linux-arm64/publish" . && echo "  [OK] linux-arm64"
			;;
		linux-musl-x64)
			tar -czf "${NAME}-${VERSION}-linux-musl-x64.tar.gz" -C "bin/Release/net10.0/linux-musl-x64/publish" . && echo "  [OK] linux-musl-x64"
			;;
		win-x64)
			tar -czf "${NAME}-${VERSION}-win-x64.tar.gz" -C "bin/Release/net10.0/win-x64/publish" . && echo "  [OK] win-x64"
			;;
		win-arm64)
			tar -czf "${NAME}-${VERSION}-win-arm64.tar.gz" -C "bin/Release/net10.0/win-arm64/publish" . && echo "  [OK] win-arm64"
			;;
	esac
}

package_all() {
	echo "[PACKAGE] Packaging binaries..."
	for RID in "${BUILT_RIDS[@]}"; do
		package_artifact "$RID"
	done
}

upload_all() {
	if ! $PUBLISH; then
		echo "[INFO] Skipping GitHub upload. Run with --publish to upload."
		return
	fi

	echo "[GITHUB] Uploading artifacts to GitHub release $RELEASE..."
	if ! command -v gh &>/dev/null; then
		echo "[ERROR] GitHub CLI (gh) not found. Please install it: https://cli.github.com/"
		exit 1
	fi

	if ! gh release view "$RELEASE" &>/dev/null; then
		echo "[GITHUB] Release $RELEASE does not exist. Creating..."
		gh release create "$RELEASE" --title "$RELEASE" --notes "Release $RELEASE"
	fi

	UPLOADS=()
	for RID in "${BUILT_RIDS[@]}"; do
		case "$RID" in
			osx-arm64) UPLOADS+=("${NAME}-${VERSION}-macos-arm64.tar.gz") ;;
			osx-x64) UPLOADS+=("${NAME}-${VERSION}-macos-x64.tar.gz") ;;
			linux-x64) UPLOADS+=("${NAME}-${VERSION}-linux-x64.tar.gz") ;;
			linux-arm64) UPLOADS+=("${NAME}-${VERSION}-linux-arm64.tar.gz") ;;
			linux-musl-x64) UPLOADS+=("${NAME}-${VERSION}-linux-musl-x64.tar.gz") ;;
			win-x64) UPLOADS+=("${NAME}-${VERSION}-win-x64.tar.gz") ;;
			win-arm64) UPLOADS+=("${NAME}-${VERSION}-win-arm64.tar.gz") ;;
		esac
	done

	if [ ${#UPLOADS[@]} -eq 0 ]; then
		echo "[GITHUB] No artifacts to upload."
	else
		gh release upload "$RELEASE" "${UPLOADS[@]}" --clobber
		echo "[GITHUB] Upload complete."
	fi
}

run_all_builds
package_all
upload_all
