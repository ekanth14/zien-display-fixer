# Zien Display Fixer

> **A calm, ultra-refined multi-display optimizer for Windows, macOS, and Linux.**

---

## Overview & Design Philosophy

**Zien Display Fixer** is a native cross-platform desktop application designed to solve common multi-monitor configuration headaches: incorrect resolutions, refresh rates stuck at 60 Hz on high-refresh gaming panels, distorted aspect ratios, and misplaced monitor topologies.

### "Calm by Design"
Zien Display Fixer was engineered from the ground up with a strict aesthetic and functional ethos:
- **Graphite & Champagne Palette**: A distraction-free dark interface with deep graphite surfaces (`#1E1E1E` / `#252526`), soft border contours (`#333333`), and warm champagne accents (`#D4AF37` / `#E6C280`).
- **Zero Telemetry & Tracking**: No analytics, no phone-home pings, no cloud dependencies. Everything runs locally on your machine.
- **One-Click Simplicity**: Automatic detection and intelligent evaluation of connected displays with an instant "Fix my screens" action.
- **Fail-Safe Confidence**: Every mode switch is guarded by a 12-second confirmation modal. If the display signal drops or the driver rejects the change, your previous configuration is automatically restored.

---

## Key Features

1. **One-Click Display Optimization**
   - Automatically inspects all active displays and reads timing specifications directly from hardware EDID.
   - Evaluates candidate modes using an intelligent scoring algorithm (`BestModePicker`) prioritizing native panel resolution, highest available refresh rate, and progressive scanning.

2. **12-Second Fail-Safe Rollback**
   - Prior to making permanent configuration changes, candidate modes are tested with driver-level non-destructive flags (`CDS_TEST` on Windows, temporary configuration on macOS, dry-run testing on Linux).
   - A dedicated countdown modal gives you 12 seconds to confirm the change. If unconfirmed or rejected, the previous display state is immediately restored.

3. **Hardware EDID Extraction & Decoding**
   - Low-level direct reading from kernel and driver interfaces: Win32 WMI and registry on Windows, `IORegistry` / `AppleDisplay` on macOS, and DRM sysfs (`/sys/class/drm/*/edid`) on Linux.
   - Parses physical dimensions in millimeters, calculates diagonal screen inches, resolves display manufacturer and model names, and identifies subpixel structures.

4. **Vector Scene Topology Control**
   - High-fidelity custom vector canvas (`SceneControl`) rendering accurate monitor layouts, proportional aspect ratios, monitor bezels, desk surfaces, and laptop stands.
   - Mathematical layout engine (`SceneLayout`) calculates normalized bounding boxes, scale factors, and spatial relationships across diverse DPI settings.

5. **Multi-Topology Switching**
   - One-click switching between display topologies: **Extend**, **Duplicate / Mirror**, **Primary Only**, and **External Only**.

6. **Desktop Wallpaper Sampling**
   - Seamlessly extracts the user's active desktop wallpaper on Windows, macOS, and Linux desktop environments (GNOME, KDE, XFCE).
   - Gracefully falls back to handcrafted ambient champagne gradients when wallpaper queries are restricted.

7. **Native Wireless Display Projection**
   - Fast access to OS-native wireless casting: Windows Project menu (`ms-settings-connect:`), macOS AirPlay / Control Center, and Linux GNOME Network Displays.

8. **Live Hot-Plug Detection & Debouncing**
   - Background monitor (`DisplayHotPlugMonitor`) listens for hardware connection and disconnection events via OS messaging (`WM_DISPLAYCHANGE`, `CGDisplayRegisterReconfigurationCallback`, udev/xrandr events) with 1.5-second debouncing to prevent thrashing.

9. **Autostart & System Tray**
   - Optional silent startup on login via OS-standard mechanisms (Windows Run Registry, macOS LaunchAgent, Linux XDG Autostart).
   - System tray icon with background monitoring and quick toggle controls.

---

## Solution Structure

```
Zien Display Fixer/
├── src/
│   ├── ZienDisplayFixer.Core/         # Platform-agnostic domain logic
│   │   ├── Models/                   # DisplayInfo, DisplayMode, Topology, Settings
│   │   ├── Layout/                   # SceneLayout vector calculations & geometry
│   │   ├── Services/                 # Service contracts (IDisplayService, IEdidReader, etc.)
│   │   └── Scoring/                  # BestModePicker candidate scoring algorithm
│   │
│   └── ZienDisplayFixer.App/          # Native Avalonia UI (v12.1.6) & Services
│       ├── Assets/                   # Application icons and vector resources
│       ├── Controls/                 # Custom vector controls (SceneControl)
│       ├── Converters/               # XAML value converters
│       ├── Properties/
│       │   └── PublishProfiles/      # .NET 10 self-contained single-file profiles
│       ├── Services/                 # Platform service implementations
│       │   ├── Windows/              # Win32 CCD, WMI EDID, Registry autostart
│       │   ├── MacOS/                # CoreGraphics, IORegistry, LaunchAgents
│       │   └── Linux/                # X11/Xrandr, Wayland D-Bus, sysfs DRM EDID
│       ├── Styles/                   # Graphite & Champagne theme definitions
│       ├── ViewModels/               # CommunityToolkit.Mvvm view models
│       └── Views/                    # Avalonia XAML windows and dialogs
│
├── tests/
│   └── ZienDisplayFixer.Tests/       # Unit & integration test suites
│       ├── Layout/                   # Vector geometry and layout tests
│       ├── Linux/                    # X11 output parser tests
│       ├── Models/                   # Domain model and scoring tests
│       ├── Services/                 # Service mocks and business logic tests
│       └── ViewModels/               # ViewModel state machine and countdown tests
│
└── packaging/                        # Distribution & installer automation
    ├── windows/                      # Inno Setup 6+ installer script (.iss)
    ├── macos/                        # Info.plist & macOS .app bundle builder script
    └── linux/                        # XDG .desktop file, .deb builder, & AppImage builder
```

---

## Prerequisites

- **.NET 10.0 SDK** (v10.0.100 or higher)
- **Supported Operating Systems**:
  - Windows 10 (Build 17763+) or Windows 11 (x64)
  - macOS 12 Monterey or higher (Apple Silicon `arm64` & Intel `x64`)
  - Linux (Ubuntu 22.04+, Fedora 38+, Arch Linux, Debian 12+) with X11 or Wayland

---

## Building and Running

### Build the Solution
```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

### Run the Application
```bash
# Launch via .NET CLI
dotnet run --project src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj
```

### Run the Test Suite
```bash
# Run all unit and integration tests
dotnet test
```

---

## Release Publishing

Zien Display Fixer uses .NET 10 single-file self-contained publishing to produce standalone executables that require no pre-installed runtime dependencies on user machines.

Publish profiles are located in `src/ZienDisplayFixer.App/Properties/PublishProfiles/`:

| Platform | Runtime Identifier | Profile | Characteristics |
|---|---|---|---|
| **Windows x64** | `win-x64` | `win-x64.pubxml` | Self-contained, Single-file, ReadyToRun |
| **macOS Apple Silicon** | `osx-arm64` | `osx-arm64.pubxml` | Self-contained, Single-file |
| **macOS Intel** | `osx-x64` | `osx-x64.pubxml` | Self-contained, Single-file |
| **Linux x64** | `linux-x64` | `linux-x64.pubxml` | Self-contained, Single-file |

### Publish Commands

#### 1. Windows (x64)
```bash
dotnet publish src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj -p:PublishProfile=win-x64
```
*Output*: `src/ZienDisplayFixer.App/bin/Release/net10.0/win-x64/publish/ZienDisplayFixer.App.exe`

#### 2. macOS Apple Silicon (arm64)
```bash
dotnet publish src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj -p:PublishProfile=osx-arm64
```
*Output*: `src/ZienDisplayFixer.App/bin/Release/net10.0/osx-arm64/publish/ZienDisplayFixer.App`

#### 3. macOS Intel (x64)
```bash
dotnet publish src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj -p:PublishProfile=osx-x64
```
*Output*: `src/ZienDisplayFixer.App/bin/Release/net10.0/osx-x64/publish/ZienDisplayFixer.App`

#### 4. Linux (x64)
```bash
dotnet publish src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj -p:PublishProfile=linux-x64
```
*Output*: `src/ZienDisplayFixer.App/bin/Release/net10.0/linux-x64/publish/ZienDisplayFixer.App`

---

## Packaging Guides

### 1. Windows: Inno Setup Installer (`.exe`)
Prerequisite: [Inno Setup 6+](https://jrsoftware.org/isdl.php)

1. Publish the `win-x64` release build:
   ```powershell
   dotnet publish src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj -p:PublishProfile=win-x64
   ```
2. Compile the installer script:
   ```powershell
   iscc packaging/windows/installer.iss
   ```
3. Installer output will be generated in `dist/windows/ZienDisplayFixer-Setup-1.0.0-win-x64.exe`.

### 2. macOS: Application Bundle (`.app`) & DMG
Prerequisite: macOS 12+ terminal or bash environment

1. Run the build script to produce standalone bundles for `arm64`, `x64`, or `all`:
   ```bash
   chmod +x packaging/macos/build-app.sh
   ./packaging/macos/build-app.sh all
   ```
2. The script compiles the binaries, structures the `.app` bundle, injects `Info.plist`, applies resources, and runs ad-hoc codesigning (`codesign --force --deep --sign -`).
3. To package into a distributable DMG on macOS:
   ```bash
   hdiutil create -volname "Zien Display Fixer" -srcfolder "dist/macos/arm64/Zien Display Fixer.app" -ov -format UDZO "dist/macos/ZienDisplayFixer-1.0.0-arm64.dmg"
   ```

### 3. Linux: Debian Package (`.deb`)
Prerequisite: `dpkg-deb` (standard on Debian, Ubuntu, Linux Mint)

1. Run the Debian packaging script:
   ```bash
   chmod +x packaging/linux/build-deb.sh
   ./packaging/linux/build-deb.sh
   ```
2. Generates `dist/linux/zien-display-fixer_1.0.0_amd64.deb` containing the binary in `/usr/lib/zien-display-fixer/`, a symlink in `/usr/bin/`, the `.desktop` file in `/usr/share/applications/`, and icon assets.
3. Install on Debian/Ubuntu systems:
   ```bash
   sudo dpkg -i dist/linux/zien-display-fixer_1.0.0_amd64.deb
   ```

### 4. Linux: AppImage (`.AppImage`)
Prerequisite: [appimagetool](https://github.com/AppImage/AppImageKit)

1. Run the AppImage packaging script:
   ```bash
   chmod +x packaging/linux/build-appimage.sh
   ./packaging/linux/build-appimage.sh
   ```
2. Generates a standalone `dist/linux/ZienDisplayFixer-1.0.0-x86_64.AppImage` executable on any modern Linux distribution.

---

## OS Compatibility & Testing Matrix

The table below outlines the low-level backend integration and feature compatibility across operating systems and window managers:

| Feature / Capability | Windows 10/11 (x64) | macOS 12+ (Apple Silicon & Intel) | Linux X11 (Ubuntu, Fedora, Arch) | Linux Wayland (GNOME / Mutter) | Linux Wayland (KDE / kscreen-doctor) | Linux Wayland (Sway / Hyprland / wlroots) |
|---|---|---|---|---|---|---|
| **Display Detection** | Win32 CCD (`EnumDisplayDevicesW`, `EnumDisplaySettingsExW`) | CoreGraphics (`CGGetOnlineDisplayList`, `CGDisplayCopyAllDisplayModes`) | X11 libXrandr & `xrandr -q` parsing | Mutter D-Bus (`org.gnome.Mutter.DisplayConfig`) | `kscreen-doctor -j` JSON querying | `wlr-randr --json` output parsing |
| **Best-Mode Selection** | Native EDID match, maximum refresh rate, progressive scan | Retina point-scaling aware, highest available refresh rate | Native preferred mode (`+`), highest Hz candidate scoring | Preferred mode matching via D-Bus properties | Preferred mode matching via KScreen profile | Highest mode matching via wlroots output info |
| **Fail-Safe Test & Apply** | Non-destructive `ChangeDisplaySettingsExW(CDS_TEST)` prior to commit | `CGBeginDisplayConfiguration` & `CGConfigureDisplayWithDisplayMode` | `xrandr --output <name> --mode <res> --rate <rate>` execution | `org.gnome.Mutter.DisplayConfig.ApplyMonitorsConfig` (temporary test) | `kscreen-doctor output.<id>.mode.<id>` execution | `wlr-randr --output <name> --mode <res>` execution |
| **Revert Countdown (12s)** | Automated rollback to previous `DEVMODEW` state upon timeout/rejection | Automated rollback to captured `CGDisplayMode` handles upon timeout | Automated rollback via stored `xrandr` baseline parameters | Automated rollback to previous Mutter configuration state | Automated rollback to previous KScreen configuration state | Automated rollback to previous wlroots configuration state |
| **EDID Parsing** | WMI `WmiMonitorDescriptorMethods` & Registry `DISPLAY` fallback | IORegistry `AppleDisplay` / `IODisplayCreateInfoDictionary` | Direct DRM sysfs binary read (`/sys/class/drm/*/edid`) | Direct DRM sysfs binary read (`/sys/class/drm/*/edid`) | Direct DRM sysfs binary read (`/sys/class/drm/*/edid`) | Direct DRM sysfs binary read (`/sys/class/drm/*/edid`) |
| **Wallpaper Pickup** | `SystemParametersInfoW(SPI_GETDESKWALLPAPER)` + file stream | `osascript` desktop picture lookup + fallback gradient | GNOME gsettings / XFCE xfconf / KDE kreadconfig5 + fallback | GNOME gsettings picture-uri + fallback gradient | Plasma wallpaper configuration + fallback gradient | Static / compositor wallpaper lookup + fallback gradient |
| **Autostart on Login** | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | macOS LaunchAgent (`~/Library/LaunchAgents/com.zien.displayfixer.plist`) | XDG Autostart (`~/.config/autostart/zien-display-fixer.desktop`) | XDG Autostart (`~/.config/autostart/zien-display-fixer.desktop`) | XDG Autostart (`~/.config/autostart/zien-display-fixer.desktop`) | XDG Autostart (`~/.config/autostart/zien-display-fixer.desktop`) |
| **System Tray Support** | Avalonia `TrayIcon` with Win32 shell notification icon | macOS menu bar extra status item via Avalonia `TrayIcon` | StatusNotifierItem / AppIndicator / XEmbed tray icon | StatusNotifierItem (SNI) D-Bus protocol | StatusNotifierItem (SNI) D-Bus protocol | StatusNotifierItem (SNI) via Wayland bar (Waybar/etc) |
| **Implementation Status** | **Complete & Verified** | **Complete & Verified** | **Complete & Verified** | **Complete & Verified** | **Complete & Verified** | **Complete & Verified** |

---

## License

This project is licensed under the MIT License - see the LICENSE file for details.

