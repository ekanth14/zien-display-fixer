#!/usr/bin/env bash
set -euo pipefail

# Determine script and repository root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

APP_NAME="Zien Display Fixer"
PROJECT_FILE="${REPO_ROOT}/src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj"
PLIST_FILE="${SCRIPT_DIR}/Info.plist"
OUTPUT_DIR="${REPO_ROOT}/dist/macos"
CONFIG="${CONFIGURATION:-Release}"
TARGET_ARCH="${1:-all}" # arm64, x64, or all

build_arch() {
  local ARCH="$1"
  local RID="osx-${ARCH}"
  local PUBLISH_DIR="${REPO_ROOT}/src/ZienDisplayFixer.App/bin/${CONFIG}/net10.0/${RID}/publish"
  local BUNDLE_DIR="${OUTPUT_DIR}/${ARCH}/${APP_NAME}.app"

  echo "=================================================="
  echo "Building macOS application bundle for ${ARCH} (${RID})..."
  echo "=================================================="

  dotnet publish "${PROJECT_FILE}" \
    -c "${CONFIG}" \
    -r "${RID}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false

  echo "Constructing .app bundle structure in ${BUNDLE_DIR}..."
  rm -rf "${BUNDLE_DIR}"
  mkdir -p "${BUNDLE_DIR}/Contents/MacOS"
  mkdir -p "${BUNDLE_DIR}/Contents/Resources"

  echo "Copying published binary..."
  cp "${PUBLISH_DIR}/ZienDisplayFixer.App" "${BUNDLE_DIR}/Contents/MacOS/ZienDisplayFixer.App"
  chmod +x "${BUNDLE_DIR}/Contents/MacOS/ZienDisplayFixer.App"

  echo "Copying Info.plist..."
  cp "${PLIST_FILE}" "${BUNDLE_DIR}/Contents/Info.plist"

  # Copy icon if available
  if [ -f "${SCRIPT_DIR}/AppIcon.icns" ]; then
    cp "${SCRIPT_DIR}/AppIcon.icns" "${BUNDLE_DIR}/Contents/Resources/AppIcon.icns"
  elif [ -f "${REPO_ROOT}/src/ZienDisplayFixer.App/Assets/avalonia-logo.ico" ]; then
    cp "${REPO_ROOT}/src/ZienDisplayFixer.App/Assets/avalonia-logo.ico" "${BUNDLE_DIR}/Contents/Resources/AppIcon.ico"
  fi

  # Ad-hoc codesign on macOS
  if [ "$(uname)" = "Darwin" ]; then
    echo "Applying ad-hoc code signature..."
    codesign --force --deep --sign - "${BUNDLE_DIR}"
  fi

  echo "Successfully built: ${BUNDLE_DIR}"
}

mkdir -p "${OUTPUT_DIR}"

case "${TARGET_ARCH}" in
  arm64)
    build_arch "arm64"
    ;;
  x64)
    build_arch "x64"
    ;;
  all)
    build_arch "arm64"
    build_arch "x64"
    ;;
  *)
    echo "Unknown architecture: ${TARGET_ARCH}. Valid options: arm64, x64, all"
    exit 1
    ;;
esac

echo ""
echo "macOS packaging complete. Bundles available in: ${OUTPUT_DIR}"

