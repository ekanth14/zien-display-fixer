#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

APP_NAME="zien-display-fixer"
VERSION="1.0.0"
ARCH="amd64"
CONFIG="${CONFIGURATION:-Release}"
RID="linux-x64"

PROJECT_FILE="${REPO_ROOT}/src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj"
PUBLISH_DIR="${REPO_ROOT}/src/ZienDisplayFixer.App/bin/${CONFIG}/net10.0/${RID}/publish"
DESKTOP_FILE="${SCRIPT_DIR}/zien-display-fixer.desktop"
DIST_DIR="${REPO_ROOT}/dist/linux"
STAGING_DIR="${DIST_DIR}/deb-staging"
DEB_NAME="${APP_NAME}_${VERSION}_${ARCH}.deb"

echo "=================================================="
echo "Publishing ${APP_NAME} for ${RID}..."
echo "=================================================="

dotnet publish "${PROJECT_FILE}" \
  -c "${CONFIG}" \
  -r "${RID}" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false

echo "Assembling Debian package layout in ${STAGING_DIR}..."
rm -rf "${STAGING_DIR}"
mkdir -p "${STAGING_DIR}/DEBIAN"
mkdir -p "${STAGING_DIR}/usr/bin"
mkdir -p "${STAGING_DIR}/usr/lib/${APP_NAME}"
mkdir -p "${STAGING_DIR}/usr/share/applications"
mkdir -p "${STAGING_DIR}/usr/share/icons/hicolor/256x256/apps"
mkdir -p "${STAGING_DIR}/usr/share/doc/${APP_NAME}"

# Control file
cat <<EOF > "${STAGING_DIR}/DEBIAN/control"
Package: ${APP_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Maintainer: Zien Maintainers <support@zien.dev>
Depends: libc6 (>= 2.31), libgcc-s1, libx11-6, libxrandr2
Recommends: x11-xserver-utils
Description: Multi-platform display resolution and refresh rate optimizer
 Zien Display Fixer is a calm, minimal desktop utility that detects connected
 displays, parses hardware EDIDs, automatically selects optimal resolution
 and refresh rate combinations, and applies changes with a 12-second fail-safe.
EOF

# Copy binary
cp "${PUBLISH_DIR}/ZienDisplayFixer.App" "${STAGING_DIR}/usr/lib/${APP_NAME}/${APP_NAME}"
chmod 755 "${STAGING_DIR}/usr/lib/${APP_NAME}/${APP_NAME}"

# Symlink in /usr/bin
ln -sf "/usr/lib/${APP_NAME}/${APP_NAME}" "${STAGING_DIR}/usr/bin/${APP_NAME}"

# Copy desktop file
cp "${DESKTOP_FILE}" "${STAGING_DIR}/usr/share/applications/${APP_NAME}.desktop"
chmod 644 "${STAGING_DIR}/usr/share/applications/${APP_NAME}.desktop"

# Copy icon if available
if [ -f "${REPO_ROOT}/src/ZienDisplayFixer.App/Assets/avalonia-logo.ico" ]; then
  cp "${REPO_ROOT}/src/ZienDisplayFixer.App/Assets/avalonia-logo.ico" "${STAGING_DIR}/usr/share/icons/hicolor/256x256/apps/${APP_NAME}.ico"
fi

# Post-install & Post-remove hooks
cat <<'EOF' > "${STAGING_DIR}/DEBIAN/postinst"
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
EOF
chmod 755 "${STAGING_DIR}/DEBIAN/postinst"

cat <<'EOF' > "${STAGING_DIR}/DEBIAN/postrm"
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
EOF
chmod 755 "${STAGING_DIR}/DEBIAN/postrm"

# Copyright
cat <<EOF > "${STAGING_DIR}/usr/share/doc/${APP_NAME}/copyright"
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: ${APP_NAME}
Source: https://github.com/zien/zien-display-fixer

Files: *
Copyright: 2026 Zien
License: MIT
EOF

mkdir -p "${DIST_DIR}"

if command -v dpkg-deb >/dev/null 2>&1; then
  echo "Building .deb package..."
  dpkg-deb --build --root-owner-group "${STAGING_DIR}" "${DIST_DIR}/${DEB_NAME}"
  echo "Debian package created: ${DIST_DIR}/${DEB_NAME}"
else
  echo "dpkg-deb not found on current host. Package staged at: ${STAGING_DIR}"
  echo "To build manually on a Debian-compatible system, run:"
  echo "  dpkg-deb --build --root-owner-group \"${STAGING_DIR}\" \"${DIST_DIR}/${DEB_NAME}\""
fi

