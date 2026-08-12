#!/usr/bin/env bash
set -euo pipefail

RUNTIME_ID="${1:?usage: package.sh <win-x64|osx-arm64|osx-x64|linux-x64> [version]}"
VERSION="${2:-0.1.0}"
case "$RUNTIME_ID" in
  win-x64|osx-arm64|osx-x64|linux-x64) ;;
  *) echo "Unsupported runtime identifier: $RUNTIME_ID" >&2; exit 2 ;;
esac
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "Invalid semantic version: $VERSION" >&2
  exit 2
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
ARTIFACTS_ROOT="$REPOSITORY_ROOT/artifacts"
PUBLISH_DIR="$ARTIFACTS_ROOT/publish/$RUNTIME_ID"
STAGING_DIR="$ARTIFACTS_ROOT/staging/$RUNTIME_ID"
PACKAGES_DIR="$ARTIFACTS_ROOT/packages"

rm -rf -- "$PUBLISH_DIR" "$STAGING_DIR"
mkdir -p -- "$PUBLISH_DIR" "$STAGING_DIR" "$PACKAGES_DIR"

dotnet publish "$REPOSITORY_ROOT/src/MdbTestBench.App/MdbTestBench.App.csproj" \
  --configuration Release \
  --runtime "$RUNTIME_ID" \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  -p:Version="$VERSION" \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None \
  -p:DebugSymbols=false
find "$PUBLISH_DIR" -type f -name '*.pdb' -delete

case "$RUNTIME_ID" in
  win-x64)
    PACKAGE="$PACKAGES_DIR/MDB-Test-Bench-v$VERSION-windows-x64.zip"
    rm -f -- "$PACKAGE"
    (cd -- "$PUBLISH_DIR" && zip -qry "$PACKAGE" .)
    ;;
  osx-arm64|osx-x64)
    APP_DIR="$STAGING_DIR/MDB Test Bench.app"
    mkdir -p -- "$APP_DIR/Contents/MacOS"
    cp -R -- "$PUBLISH_DIR/." "$APP_DIR/Contents/MacOS/"
    sed "s/@VERSION@/$VERSION/g" "$REPOSITORY_ROOT/build/macos/Info.plist.template" > "$APP_DIR/Contents/Info.plist"
    chmod +x "$APP_DIR/Contents/MacOS/MDB-Test-Bench"
    ARCH_NAME="${RUNTIME_ID#osx-}"
    PACKAGE="$PACKAGES_DIR/MDB-Test-Bench-v$VERSION-macos-$ARCH_NAME.zip"
    rm -f -- "$PACKAGE"
    (cd -- "$STAGING_DIR" && zip -qry "$PACKAGE" "MDB Test Bench.app")
    ;;
  linux-x64)
    chmod +x "$PUBLISH_DIR/MDB-Test-Bench"
    PACKAGE="$PACKAGES_DIR/MDB-Test-Bench-v$VERSION-linux-x64.tar.gz"
    rm -f -- "$PACKAGE"
    tar -C "$PUBLISH_DIR" -czf "$PACKAGE" .
    ;;
esac

HOST_OS="$(uname -s)"
HOST_ARCH="$(uname -m)"
if [[ "$RUNTIME_ID" == "linux-x64" && "$HOST_OS" == "Linux" && "$HOST_ARCH" == "x86_64" ]]; then
  SMOKE_CACHE_DIR="$STAGING_DIR/smoke-cache"
  mkdir -p -- "$SMOKE_CACHE_DIR"
  DOTNET_BUNDLE_EXTRACT_BASE_DIR="$SMOKE_CACHE_DIR" "$PUBLISH_DIR/MDB-Test-Bench" --smoke-test
elif [[ "$RUNTIME_ID" == "osx-arm64" && "$HOST_OS" == "Darwin" && "$HOST_ARCH" == "arm64" ]]; then
  SMOKE_CACHE_DIR="$STAGING_DIR/smoke-cache"
  mkdir -p -- "$SMOKE_CACHE_DIR"
  DOTNET_BUNDLE_EXTRACT_BASE_DIR="$SMOKE_CACHE_DIR" "$STAGING_DIR/MDB Test Bench.app/Contents/MacOS/MDB-Test-Bench" --smoke-test
elif [[ "$RUNTIME_ID" == "osx-x64" && "$HOST_OS" == "Darwin" && "$HOST_ARCH" == "x86_64" ]]; then
  SMOKE_CACHE_DIR="$STAGING_DIR/smoke-cache"
  mkdir -p -- "$SMOKE_CACHE_DIR"
  DOTNET_BUNDLE_EXTRACT_BASE_DIR="$SMOKE_CACHE_DIR" "$STAGING_DIR/MDB Test Bench.app/Contents/MacOS/MDB-Test-Bench" --smoke-test
else
  echo "Smoke test skipped: $RUNTIME_ID is not native to $HOST_OS/$HOST_ARCH."
fi

echo "$PACKAGE"
