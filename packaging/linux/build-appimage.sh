#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

APP_NAME="zien-display-fixer"
APP_DISPLAY_NAME="Zien Display Fixer"
VERSION="1.0.0"
CONFIG="${CONFIGURATION:-Release}"
RID="linux-x64"

PROJECT_FILE="${REPO_ROOT}/src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj"
PUBLISH_DIR="${REPO_ROOT}/src/ZienDisplayFixer.App/bin/${CONFIG}/net10.0/${RID}/publish"
DESKTOP_FILE="${SCRIPT_DIR}/zien-display-fixer.desktop"
DIST_DIR="${REPO_ROOT}/dist/linux"
APPDIR="${DIST_DIR}/AppDir"
APPIMAGE_NAME="ZienDisplayFixer-${VERSION}-x86_64.AppImage"

echo "=================================================="
echo "Publishing ${APP_DISPLAY_NAME} for ${RID}..."
echo "=================================================="

dotnet publish "${PROJECT_FILE}" \
  -c "${CONFIG}" \
  -r "${RID}" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false

echo "Constructing AppDir filesystem layout in ${APPDIR}..."
rm -rf "${APPDIR}"
mkdir -p "${APPDIR}/usr/bin"
mkdir -p "${APPDIR}/usr/lib"
mkdir -p "${APPDIR}/usr/share/applications"
mkdir -p "${APPDIR}/usr/share/icons/hicolor/256x256/apps"

# Copy application binary
cp "${PUBLISH_DIR}/ZienDisplayFixer.App" "${APPDIR}/usr/bin/${APP_NAME}"
chmod 755 "${APPDIR}/usr/bin/${APP_NAME}"

# Desktop integration files at root of AppDir
cp "${DESKTOP_FILE}" "${APPDIR}/${APP_NAME}.desktop"
cp "${DESKTOP_FILE}" "${APPDIR}/usr/share/applications/${APP_NAME}.desktop"

# Icon
if [ -f "${REPO_ROOT}/src/ZienDisplayFixer.App/Assets/avalonia-logo.ico" ]; then
  cp "${REPO_ROOT}/src/ZienDisplayFixer.App/Assets/avalonia-logo.ico" "${APPDIR}/${APP_NAME}.ico"
  cp "${REPO_ROOT}/src/ZienDisplayFixer.App/Assets/avalonia-logo.ico" "${APPDIR}/.DirIcon"
fi

# AppRun launcher script
cat <<'EOF' > "${APPDIR}/AppRun"
#!/bin/sh
HERE="$(dirname "$(readlink -f "${0}")")"
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
export XDG_DATA_DIRS="${HERE}/usr/share:${XDG_DATA_DIRS:-/usr/local/share:/usr/share}"
exec "${HERE}/usr/bin/zien-display-fixer" "$@"
EOF
chmod 755 "${APPDIR}/AppRun"

mkdir -p "${DIST_DIR}"

if command -v appimagetool >/dev/null 2>&1; then
  echo "Invoking appimagetool..."
  ARCH=x86_64 appimagetool "${APPDIR}" "${DIST_DIR}/${APPIMAGE_NAME}"
  echo "AppImage created: ${DIST_DIR}/${APPIMAGE_NAME}"
else
  echo "appimagetool not found on current host. AppDir staged at: ${APPDIR}"
  echo "To build AppImage on a Linux machine:"
  echo "  ARCH=x86_64 appimagetool \"${APPDIR}\" \"${DIST_DIR}/${APPIMAGE_NAME}\""
fi

