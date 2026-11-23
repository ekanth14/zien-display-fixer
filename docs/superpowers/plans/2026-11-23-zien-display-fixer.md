# Zien Display Fixer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build "Zien Display Fixer", a cross-platform desktop application (.NET 10, Avalonia 12, CommunityToolkit.Mvvm) that detects connected displays via EDID, visualizes the desk setup in real time, and configures each display to its best resolution, aspect ratio, and refresh rate with a single click and a 12-second fail-safe confirmation flow.

**Architecture:** Clean multi-project solution separating pure C# domain models, EDID parsing, heuristic mode picking, and scene layout calculations (`ZienDisplayFixer.Core`) from OS-specific P/Invoke and D-Bus services (`Services/Windows`, `Services/MacOS`, `Services/Linux`), UI views/viewmodels (`ZienDisplayFixer.App`), and an xUnit test project (`ZienDisplayFixer.Tests`). Dependency injection registers the correct platform implementations at runtime.

**Tech Stack:** .NET 10, C# 13, Avalonia 12.1.6, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.DependencyInjection, xUnit 2.9, Tmds.DBus.

**Spec:** [docs/superpowers/specs/2026-11-23-zien-display-fixer-design.md](file:///C:/Users/Ekanth's%20laptop/OneDrive/Desktop/visual%20studio%20projects/p1/Zien%20Display%20Fixer/docs/superpowers/specs/2026-11-23-zien-display-fixer-design.md)

---

## Global Constraints

- **Language & Runtime:** C# on .NET 10 (target framework `net10.0`).
- **UI Framework:** Avalonia 12.1.6 with `CommunityToolkit.Mvvm`.
- **Architectural Boundary:** No OS-specific code (`System.Runtime.InteropServices`, Win32, CoreGraphics, X11, D-Bus) in Views, ViewModels, or Core.
- **Fail-Safe Requirement:** Every mode change must be tested, applied, and verified with a 12-second confirmation countdown or automatically reverted.
- **Visual Design:** Minimal, premium, calm. Dark graphite background (`#0B0B0D`), champagne accent (`#CDB78F`), thin 1px borders (`#27272D`), Inter font, no neon or sci-fi effects. Dark/Light OS theme support.
- **File Granularity:** Keep files small and single-purpose. After each task, report exact build and test steps to the user.

---

## Review Focus

1. **Non-Standard or Corrupted EDID Binary Blocks:** Returns a graceful parse failure or fallback rather than throwing index/null exceptions.
2. **Missing Native Mode in Driver Modes List:** When the EDID preferred mode is not enumerated by `EnumDisplaySettings` / `CGDisplayCopyAllDisplayModes`, picks the closest supported resolution matching the physical aspect ratio.
3. **Display Mode Overlap on Resolution Increase:** When a secondary monitor is upgraded from 1080p to 4K, re-tiles monitor coordinates left-to-right to prevent desktop coordinate overlap.
4. **Wayland Display Config Protocol Variations:** If Sway/Hyprland (`wlr-randr`), KDE (`kscreen-doctor`), and GNOME Mutter D-Bus are unavailable, returns "not supported on this desktop" rather than crashing.
5. **Display Mode Confirmation Timeout on Revert Failure:** If reverting a display fails, logs and notifies the user while ensuring UI thread state remains responsive and not locked in countdown.

---

### Task 1: Solution Structure, Core Models, Interfaces & BestModePicker

**Files:**
- Create: `ZienDisplayFixer.Core/Models/DisplayInfo.cs`
- Create: `ZienDisplayFixer.Core/Models/DisplayMode.cs`
- Create: `ZienDisplayFixer.Core/Models/Enums.cs`
- Create: `ZienDisplayFixer.Core/Interfaces/IDisplayService.cs`
- Create: `ZienDisplayFixer.Core/Interfaces/IEdidReader.cs`
- Create: `ZienDisplayFixer.Core/Interfaces/IAutostartService.cs`
- Create: `ZienDisplayFixer.Core/Interfaces/IWallpaperService.cs`
- Create: `ZienDisplayFixer.Core/Interfaces/IWirelessDisplayLauncher.cs`
- Create: `ZienDisplayFixer.Core/Interfaces/IScreenModeService.cs`
- Create: `ZienDisplayFixer.Core/Engine/BestModePicker.cs`
- Create: `ZienDisplayFixer.App/Services/Windows/WindowsDisplayServiceStub.cs`
- Create: `ZienDisplayFixer.App/Services/MacOS/MacDisplayServiceStub.cs`
- Create: `ZienDisplayFixer.App/Services/Linux/LinuxDisplayServiceStub.cs`
- Create: `ZienDisplayFixer.App/Services/ServiceRegistration.cs`
- Create: `ZienDisplayFixer.Tests/Engine/BestModePickerTests.cs`
- Modify: `Zien Display Fixer.slnx` (or convert to C# `.sln` with `ZienDisplayFixer.Core`, `ZienDisplayFixer.App`, `ZienDisplayFixer.Tests`)

**Interfaces:**
- Produces: `BestModePicker.PickBestMode(IEnumerable<DisplayMode> availableModes, DisplayInfo display)`
- Produces: `IDisplayService`, `IEdidReader`, `IScreenModeService`, `IWallpaperService`, `IWirelessDisplayLauncher`, `IAutostartService`

- [x] **Step 1: Write the failing unit tests for BestModePicker**
  Create `ZienDisplayFixer.Tests/Engine/BestModePickerTests.cs` testing:
  - Highest resolution picked first among 1080p, 1440p, 4K modes.
  - Highest refresh rate (e.g., 144Hz over 60Hz) picked when resolution is tied.
  - Interlaced modes (`IsInterlaced == true`) are ignored.
  - VGA / Wi-Fi connections are capped at 1920x1080 @ 60Hz.

- [x] **Step 2: Run test to verify it fails**
  Run: `dotnet test --filter FullyQualifiedName~BestModePickerTests`
  Expected: FAIL (project/classes not found).

- [x] **Step 3: Setup C# solution structure and projects**
  Convert the F# project to standard C# .NET 10 projects:
  - `src/ZienDisplayFixer.Core/ZienDisplayFixer.Core.csproj`
  - `src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj`
  - `tests/ZienDisplayFixer.Tests/ZienDisplayFixer.Tests.csproj`
  Define `DisplayInfo`, `DisplayMode`, `ConnectionType`, `DeviceType`, all 6 interfaces, platform stubs throwing `NotSupportedException`, and `ServiceRegistration.cs` using `IServiceCollection`.

- [x] **Step 4: Implement `BestModePicker`**
  Implement the filtering logic in `BestModePicker.cs`: filter out interlaced modes, sort by total pixels descending then refresh rate descending, apply connection caps for VGA/WiFi.

- [x] **Step 5: Run tests and verify build**
  Run: `dotnet test`
  Expected: All tests pass.

- [x] **Step 6: Commit**
  ```bash
  git add .
  git commit -m "feat: setup solution structure, core models, service interfaces, and BestModePicker with tests"
  ```

---

### Task 2: WindowsDisplayService Implementation

**Files:**
- Create: `ZienDisplayFixer.App/Services/Windows/Native/User32.cs`
- Create: `ZienDisplayFixer.App/Services/Windows/Native/DevModeW.cs`
- Create: `ZienDisplayFixer.App/Services/Windows/WindowsDisplayService.cs`
- Test: Manual Windows environment verification via test harness / integration runner.

**Interfaces:**
- Consumes: `IDisplayService`, `DisplayInfo`, `DisplayMode`
- Produces: `WindowsDisplayService : IDisplayService` with `GetDisplaysAsync`, `GetModesAsync`, `TestModeAsync`, `ApplyModeAsync`, `RestorePreviousModesAsync`, `DisplaysChanged`

- [x] **Step 1: Define Win32 DEVMODEW struct and User32 P/Invoke signatures**
  Define `DEVMODEW` with exact 220-byte layout (`Marshal.SizeOf<DEVMODEW>() == 220`), `EnumDisplayDevices`, `EnumDisplaySettingsEx`, and `ChangeDisplaySettingsEx`.
  Constants: `CDS_TEST`, `CDS_UPDATEREGISTRY`, `CDS_NORESET`, `DISP_CHANGE_SUCCESSFUL`, `DISPLAY_DEVICE_ATTACHED_TO_DESKTOP`.

- [x] **Step 2: Implement display enumeration in `WindowsDisplayService`**
  Filter only devices with `DISPLAY_DEVICE_ATTACHED_TO_DESKTOP`. Read current mode and position ($X, Y$). Enumerate all valid supported modes with `EnumDisplaySettingsEx`.

- [x] **Step 3: Implement mode testing and atomic batch apply**
  Test with `CDS_TEST`. Apply each monitor change with `CDS_UPDATEREGISTRY | CDS_NORESET`. Calculate bounding boxes: if higher resolution causes overlap, re-tile displays left-to-right maintaining original index order. Perform final commit call.

- [x] **Step 4: Implement mode restore and change notification**
  Save pre-apply snapshot to support `RestorePreviousModesAsync`. Subscribe to `Microsoft.Win32.SystemEvents.DisplaySettingsChanged` and raise `DisplaysChanged`.

- [x] **Step 5: Verify build & P/Invoke layout**
  Run: `dotnet build`
  Verify `DEVMODEW` struct size in a unit test (`Assert.Equal(220, Marshal.SizeOf<DEVMODEW>())`).

- [x] **Step 6: Commit**
  ```bash
  git add src/ZienDisplayFixer.App/Services/Windows/
  git commit -m "feat: implement WindowsDisplayService with Win32 CCD and DEVMODEW layout"
  ```

---

### Task 3: MacDisplayService Implementation

**Files:**
- Create: `ZienDisplayFixer.App/Services/MacOS/Native/CoreGraphics.cs`
- Create: `ZienDisplayFixer.App/Services/MacOS/MacDisplayService.cs`

**Interfaces:**
- Consumes: `IDisplayService`
- Produces: `MacDisplayService : IDisplayService`

- [x] **Step 1: Define CoreGraphics P/Invoke bindings**
  Declare `CGGetActiveDisplayList`, `CGDisplayCopyAllDisplayModes`, `CGDisplayModeGetWidth`, `CGDisplayModeGetHeight`, `CGDisplayModeGetRefreshRate`, `CGBeginDisplayConfiguration`, `CGConfigureDisplayWithDisplayMode`, `CGCompleteDisplayConfiguration`, and `CGDisplayRegisterReconfigurationCallback`.
  Include `kCGDisplayShowDuplicateLowResolutionModes` in options dictionary.

- [x] **Step 2: Implement display and mode enumeration**
  Query active display IDs, enumerate display modes. Treat refresh rate of 0 as 60 Hz.

- [x] **Step 3: Implement mode configuration & restore**
  Use `CGBeginDisplayConfiguration`, `CGConfigureDisplayWithDisplayMode`, and `CGCompleteDisplayConfiguration(..., CGConfigureOption.ForSession)`. Keep snapshot for rollback.

- [x] **Step 4: Register reconfiguration callback**
  Pin callback delegate with `GCHandle.Alloc` to prevent garbage collection and raise `DisplaysChanged`.

- [x] **Step 5: Verify build**
  Run: `dotnet build`

- [x] **Step 6: Commit**
  ```bash
  git add src/ZienDisplayFixer.App/Services/MacOS/
  git commit -m "feat: implement MacDisplayService with CoreGraphics P/Invoke"
  ```

---

### Task 4: LinuxDisplayService Implementation

**Files:**
- Create: `ZienDisplayFixer.Core/Linux/X11Parser.cs`
- Create: `ZienDisplayFixer.App/Services/Linux/LinuxDisplayService.cs`
- Create: `ZienDisplayFixer.Tests/Linux/X11ParserTests.cs`

**Interfaces:**
- Consumes: `IDisplayService`
- Produces: `X11Parser.ParseXRandrQuery(string output)`, `LinuxDisplayService : IDisplayService`

- [x] **Step 1: Write failing unit test for `X11Parser`**
  Test parsing sample `xrandr --query` text: extracts display names, connected status, primary flag, current resolution/rate, and list of supported modes.

- [x] **Step 2: Implement `X11Parser`**
  Pure C# regex and text parser for `xrandr` outputs. Run test to verify pass.

- [x] **Step 3: Implement `LinuxDisplayService`**
  Detect session using `Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")`.
  - On X11: execute `xrandr` process commands.
  - On Wayland: probe `wlr-randr`, `kscreen-doctor`, and `org.gnome.Mutter.DisplayConfig` D-Bus.
  - If unsupported, return a clean failure result (`IsSuccess = false, ErrorMessage = "Not supported on this desktop"`).
  - Background polling timer (2.0s interval) checking output signature hash to raise `DisplaysChanged`.

- [x] **Step 4: Verify build and parser tests**
  Run: `dotnet test --filter FullyQualifiedName~X11ParserTests`
  Expected: PASS.

- [x] **Step 5: Commit**
  ```bash
  git add src/ZienDisplayFixer.Core/Linux/ src/ZienDisplayFixer.App/Services/Linux/ tests/ZienDisplayFixer.Tests/Linux/
  git commit -m "feat: implement LinuxDisplayService with X11 parser and Wayland fallbacks"
  ```

---

### Task 5: EdidParser and OS IEdidReader Implementations

**Files:**
- Create: `ZienDisplayFixer.Core/Edid/EdidParser.cs`
- Create: `ZienDisplayFixer.Core/Edid/EdidData.cs`
- Create: `ZienDisplayFixer.App/Services/Windows/WindowsEdidReader.cs`
- Create: `ZienDisplayFixer.App/Services/Linux/LinuxEdidReader.cs`
- Create: `ZienDisplayFixer.App/Services/MacOS/MacEdidReader.cs`
- Create: `ZienDisplayFixer.Tests/Edid/EdidParserTests.cs`

**Interfaces:**
- Produces: `EdidParser.Parse(byte[] rawEdid) -> EdidData?`
- Produces: `WindowsEdidReader`, `LinuxEdidReader`, `MacEdidReader` implementing `IEdidReader`

- [x] **Step 1: Write failing tests for `EdidParser`**
  Provide synthetic and realistic 128-byte EDID byte arrays:
  - Header check (`00 FF FF FF FF FF FF 00`) & checksum modulo 256.
  - 3-letter compressed ASCII vendor decoding (e.g. `DEL` -> Dell).
  - Model name extraction from `0xFC` descriptor block.
  - Width and height (bytes 21, 22), diagonal inches calculation, aspect ratio snapping to nearest of (16:9, 16:10, 21:9, 32:9, 4:3, 5:4).
  - Preferred native mode from first Detailed Timing Descriptor.

- [x] **Step 2: Run test to verify it fails**
  Run: `dotnet test --filter FullyQualifiedName~EdidParserTests`

- [x] **Step 3: Implement `EdidParser` in pure C#**
  Implement header, checksum, vendor unpacking (`(byte1 & 0x7C) >> 2`, etc.), descriptor looping, and aspect ratio snapping.

- [x] **Step 4: Implement platform `IEdidReader` classes**
  - Windows: read from `HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY\...\Device Parameters\EDID`.
  - Linux: read binary bytes from `/sys/class/drm/*/edid`.
  - macOS: parse `IODisplayEDID` from `ioreg -l -w0 -r -c AppleDisplay`.

- [x] **Step 5: Run tests and verify**
  Run: `dotnet test --filter FullyQualifiedName~EdidParserTests`
  Expected: PASS.

- [x] **Step 6: Commit**
  ```bash
  git add src/ZienDisplayFixer.Core/Edid/ src/ZienDisplayFixer.App/Services/ tests/ZienDisplayFixer.Tests/Edid/
  git commit -m "feat: implement EdidParser with unit tests and OS-specific IEdidReader implementations"
  ```

---

### Task 6: App Shell, Theme Resources & Navigation

**Files:**
- Create: `ZienDisplayFixer.App/Styles/AppTheme.axaml`
- Create: `ZienDisplayFixer.App/Styles/Colors.axaml`
- Create: `ZienDisplayFixer.App/Styles/Typography.axaml`
- Create: `ZienDisplayFixer.App/ViewModels/ViewModelBase.cs`
- Create: `ZienDisplayFixer.App/ViewModels/MainViewModel.cs`
- Create: `ZienDisplayFixer.App/Views/MainWindow.axaml`
- Create: `ZienDisplayFixer.App/Views/MainWindow.axaml.cs`
- Modify: `ZienDisplayFixer.App/App.axaml`

**Interfaces:**
- Produces: `MainViewModel` (CurrentPage, PageNavigationCommand, DisplaySummaryText)
- Produces: Top bar wordmark, pill navigation, and page placeholders.

- [x] **Step 1: Create Theme Dictionaries**
  Define color palette resources for both Dark and Light variants:
  - Dark: Background `#0B0B0D`, Panel `#121215`, Border `#27272D`, Text `#F1EFEA`, Muted `#9A978F`, Accent `#CDB78F`, OK `#86B894`, Warning `#D4A35F`.
  - Light: Background `#F4F2ED`, Panel `#FFFFFF`, Border `#E1DDD3`, Text `#1B1A17`, Muted `#6B665B`, Accent `#8A6A3B`.
  Bundle Inter font and set up 24px border radii, 1px borders, and pill button styles.

- [x] **Step 2: Implement `MainViewModel`**
  Implement page routing properties: `CurrentPage` (Home, Displays, Wireless, Settings), `SelectedPageIndex`, and `DisplaySummaryText` ("N displays · M wireless").

- [x] **Step 3: Build `MainWindow.axaml`**
  Replicate HTML header:
  - Brand: "ZIEN" bold wordmark (300 weight, 0.42em spacing) + "Display Fixer" subtitle.
  - Centered navigation pill buttons (Home, Displays, Wireless, Settings) bound to `MainViewModel`.
  - Right-aligned summary text block.
  - ContentControl presenting `CurrentPage` using Avalonia `DataTemplates`.

- [x] **Step 4: Verify application launch**
  Run: `dotnet run --project src/ZienDisplayFixer.App`
  Verify the window renders cleanly with the graphite/champagne theme and responsive navigation.

- [x] **Step 5: Commit**
  ```bash
  git add src/ZienDisplayFixer.App/
  git commit -m "feat: implement app shell, theme dictionaries, Inter typography, and page navigation"
  ```

---

### Task 7: SceneControl Custom Drawing & SceneLayout Math

**Files:**
- Create: `ZienDisplayFixer.Core/Layout/SceneLayout.cs`
- Create: `ZienDisplayFixer.Core/Layout/SceneGeometryModels.cs`
- Create: `ZienDisplayFixer.App/Controls/SceneControl.cs`
- Create: `ZienDisplayFixer.Tests/Layout/SceneLayoutTests.cs`

**Interfaces:**
- Consumes: `DisplayInfo`, `DeviceType`
- Produces: `SceneLayout.Calculate(DeviceType device, double laptopInches, IReadOnlyList<DisplayInfo> displays, Size viewportSize) -> SceneGeometry`
- Produces: `SceneControl : Control` with Avalonia `Render(DrawingContext context)`

- [ ] **Step 1: Write unit tests for `SceneLayout`**
  Verify coordinates calculation:
  - Laptop or PC placed centrally at $(500, 265)$.
  - Displays partitioned into Left (odd/alternating) and Right (even) sides, stacked up to 3 per column, forming outward columns when $> 3$.
  - Scaled physical dimensions according to aspect ratio and diagonal.
  - Bézier cable curves endpoints and pill badge placement.

- [ ] **Step 2: Implement `SceneLayout` in pure C#**
  Implement geometric layout mathematics. Run unit tests to verify accuracy.

- [ ] **Step 3: Implement `SceneControl` custom control**
  Override `Render(DrawingContext context)`. Draw:
  - Device (Laptop with keyboard notch / PC tower with ZIEN logo).
  - Monitors with wallpaper gradient/image, large display number, status dot (green/amber), size/aspect tags, and resolution/refresh text.
  - Cables with Bézier curves, animated pulse dashes (30 FPS dispatcher timer), Wi-Fi air waves, and port label pills.
  - Respect `IsAnimationEnabled` property.

- [ ] **Step 4: Run tests and verify rendering**
  Run: `dotnet test --filter FullyQualifiedName~SceneLayoutTests`
  Expected: PASS.

- [ ] **Step 5: Commit**
  ```bash
  git add src/ZienDisplayFixer.Core/Layout/ src/ZienDisplayFixer.App/Controls/ tests/ZienDisplayFixer.Tests/Layout/
  git commit -m "feat: implement SceneLayout math and custom SceneControl with animated vector cables"
  ```

---

### Task 8: Home Page & 12-Second Fail-Safe Confirmation

**Files:**
- Create: `ZienDisplayFixer.App/ViewModels/HomeViewModel.cs`
- Create: `ZienDisplayFixer.App/ViewModels/ModeConfirmationViewModel.cs`
- Create: `ZienDisplayFixer.App/Views/HomeView.axaml`
- Create: `ZienDisplayFixer.App/Views/HomeView.axaml.cs`
- Create: `ZienDisplayFixer.App/Views/ModeConfirmationDialog.axaml`
- Create: `ZienDisplayFixer.App/Services/Windows/WindowsScreenModeService.cs`

**Interfaces:**
- Consumes: `IDisplayService`, `IScreenModeService`, `BestModePicker`
- Produces: `HomeViewModel`, `ModeConfirmationViewModel`, `HomeView`

- [ ] **Step 1: Implement `IScreenModeService`**
  - Windows: execute `DisplaySwitch.exe /extend`, `/clone`, `/internal`, `/external`.
  - macOS: CoreGraphics mirror API.
  - Linux: `xrandr --same-as` / `--right-of`.

- [ ] **Step 2: Implement `ModeConfirmationViewModel`**
  12-second countdown timer. Properties: `RemainingSeconds`, `KeepCommand`, `RevertCommand`. If timer hits 0 without user click, automatically triggers revert.

- [ ] **Step 3: Implement `HomeViewModel`**
  - "Fix my screens" command: evaluates all displays, picks best mode via `BestModePicker` (capped at 1080p@60Hz for VGA/WiFi), tests modes first, applies all in batch, then opens confirmation overlay.
  - On Revert or timeout: calls `IDisplayService.RestorePreviousModesAsync()`.
  - Topology segmented buttons: Extend, Duplicate, Main only, Second only.
  - Status text computation ("X of Y displays can be improved" / "All displays are at their best settings").

- [ ] **Step 4: Build `HomeView.axaml` and `ModeConfirmationDialog.axaml`**
  Assemble `SceneControl`, large "Fix my screens" button, status text, segmented mode buttons, and the confirmation modal overlay.

- [ ] **Step 5: Verify build & workflow**
  Run: `dotnet run --project src/ZienDisplayFixer.App`
  Test mode application, countdown countdown tick, and revert flow.

- [ ] **Step 6: Commit**
  ```bash
  git add src/ZienDisplayFixer.App/
  git commit -m "feat: implement Home view, screen mode switcher, and 12-second fail-safe confirmation dialog"
  ```

---

### Task 9: Displays Page, Wallpaper Service & Demo Mode

**Files:**
- Create: `ZienDisplayFixer.App/Services/Windows/WindowsWallpaperService.cs`
- Create: `ZienDisplayFixer.App/Services/MacOS/MacWallpaperService.cs`
- Create: `ZienDisplayFixer.App/Services/Linux/LinuxWallpaperService.cs`
- Create: `ZienDisplayFixer.App/ViewModels/DisplaysViewModel.cs`
- Create: `ZienDisplayFixer.App/Views/DisplaysView.axaml`
- Create: `ZienDisplayFixer.App/Views/DisplaysView.axaml.cs`

**Interfaces:**
- Consumes: `IWallpaperService`, `IDisplayService`
- Produces: `DisplaysViewModel`, `DisplaysView`

- [ ] **Step 1: Implement `IWallpaperService`**
  - Windows: P/Invoke `SystemParametersInfo(SPI_GETDESKWALLPAPER)` to get active wallpaper image path.
  - macOS: `osascript -e 'tell app "finder" to get posix path of (get desktop picture as alias)'`.
  - Linux: `gsettings get org.gnome.desktop.background picture-uri`.

- [ ] **Step 2: Implement `DisplaysViewModel`**
  - Device type toggle (Laptop / PC), laptop size selector (13"–17").
  - Wallpaper selector with 4 gradient presets (Dusk, Dune, Mist, Graphite), current OS wallpaper, and custom file Upload button.
  - Connected displays list with model line, diagonal slider (13"–65"), aspect ratio dropdown, connection dropdown, native mode label.
  - Demo mode support: when enabled in Settings, allows `+ Add` display and manual property edits (up to max 8).

- [ ] **Step 3: Build `DisplaysView.axaml`**
  Match prototype layout with panels, sliders, dropdowns, and display list cards.

- [ ] **Step 4: Verify in runtime**
  Run: `dotnet run --project src/ZienDisplayFixer.App`
  Verify wallpaper loading and live updates reflected in `SceneControl`.

- [ ] **Step 5: Commit**
  ```bash
  git add src/ZienDisplayFixer.App/
  git commit -m "feat: implement Displays page, OS wallpaper integration, and Demo mode support"
  ```

---

### Task 10: Wireless Displays Page & Launcher

**Files:**
- Create: `ZienDisplayFixer.App/Services/Windows/WindowsWirelessDisplayLauncher.cs`
- Create: `ZienDisplayFixer.App/Services/MacOS/MacWirelessDisplayLauncher.cs`
- Create: `ZienDisplayFixer.App/Services/Linux/LinuxWirelessDisplayLauncher.cs`
- Create: `ZienDisplayFixer.App/ViewModels/WirelessViewModel.cs`
- Create: `ZienDisplayFixer.App/Views/WirelessView.axaml`

**Interfaces:**
- Consumes: `IWirelessDisplayLauncher`
- Produces: `WirelessViewModel`, `WirelessView`

- [ ] **Step 1: Implement `IWirelessDisplayLauncher`**
  - Windows: launch URI `ms-settings-connectabledevices:devicediscovery` via `Process.Start`.
  - macOS: open Displays preference pane via `open /System/Library/PreferencePanes/Displays.prefPane`.
  - Linux: check for and launch `gnome-network-displays` if available, or return clear warning.

- [ ] **Step 2: Implement `WirelessViewModel`**
  "Search again" command triggers scanning state with animated spinner and calls launcher.

- [ ] **Step 3: Build `WirelessView.axaml`**
  Instruction hint card, "Search again" button, and discovered device list with Connect actions.

- [ ] **Step 4: Verify URI launch**
  Verify clicking opens the OS wireless connection flyout without exceptions.

- [ ] **Step 5: Commit**
  ```bash
  git add src/ZienDisplayFixer.App/
  git commit -m "feat: implement Wireless page and OS wireless display launcher integration"
  ```

---

### Task 11: Settings Page & Persistence

**Files:**
- Create: `ZienDisplayFixer.Core/Models/AppSettings.cs`
- Create: `ZienDisplayFixer.App/Services/SettingsService.cs`
- Create: `ZienDisplayFixer.App/ViewModels/SettingsViewModel.cs`
- Create: `ZienDisplayFixer.App/Views/SettingsView.axaml`

**Interfaces:**
- Produces: `SettingsService.LoadSettings()`, `SettingsService.SaveSettings(AppSettings settings)`
- Produces: `SettingsViewModel`, `SettingsView`

- [ ] **Step 1: Implement `AppSettings` & `SettingsService`**
  Settings properties: `FixAutomaticallyOnConnect`, `StartAtLogin`, `IsAnimationEnabled`, `IsDemoModeEnabled`.
  Persist JSON to `Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "ZienDisplayFixer", "settings.json")`.

- [ ] **Step 2: Build `SettingsViewModel` and `SettingsView.axaml`**
  Checkboxes with descriptive labels matching prototype. Binding to settings service.

- [ ] **Step 3: Verify persistence**
  Toggle settings, restart app, verify persisted values remain set.

- [ ] **Step 4: Commit**
  ```bash
  git add src/ZienDisplayFixer.Core/ src/ZienDisplayFixer.App/
  git commit -m "feat: implement Settings page and local JSON settings persistence"
  ```

---

### Task 12: Hot-Plug Auto-Fix, Autostart Service & System Tray

**Files:**
- Create: `ZienDisplayFixer.App/Services/Windows/WindowsAutostartService.cs`
- Create: `ZienDisplayFixer.App/Services/MacOS/MacAutostartService.cs`
- Create: `ZienDisplayFixer.App/Services/Linux/LinuxAutostartService.cs`
- Create: `ZienDisplayFixer.App/Services/DisplayHotPlugMonitor.cs`
- Modify: `ZienDisplayFixer.App/App.axaml.cs`
- Modify: `ZienDisplayFixer.App/App.axaml` (TrayIcon declaration)

**Interfaces:**
- Consumes: `IAutostartService`, `IDisplayService`, `IEdidReader`
- Produces: Background hot-plug monitor with 1.5s debounce, System Tray icon with Fix Now / Open / Quit actions.

- [ ] **Step 1: Implement `IAutostartService`**
  - Windows: write entry with `--minimized` to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
  - macOS: write LaunchAgent plist to `~/Library/LaunchAgents/com.zien.displayfixer.plist`.
  - Linux: write desktop entry to `~/.config/autostart/zien-display-fixer.desktop`.

- [ ] **Step 2: Implement `DisplayHotPlugMonitor`**
  Listen to `IDisplayService.DisplaysChanged`. Debounce events by 1.5 seconds. If `FixAutomaticallyOnConnect` is true and changes were not user-initiated, read new EDID, find best mode, and apply with 12s fail-safe confirmation toast.

- [ ] **Step 3: Configure Avalonia `TrayIcon`**
  Menu items: "Fix now", "Open", "Quit".
  Intercept window closing: when `FixAutomaticallyOnConnect` is active, hide window to tray instead of quitting.

- [ ] **Step 4: Verify tray and background monitor**
  Run: `dotnet run --project src/ZienDisplayFixer.App`
  Test minimize to tray, restore from tray, and background hot-plug logic.

- [ ] **Step 5: Commit**
  ```bash
  git add src/ZienDisplayFixer.App/
  git commit -m "feat: implement hot-plug auto-fix with 1.5s debounce, autostart service, and system tray integration"
  ```

---

### Task 13: Multi-Platform Publishing, Packaging Scripts & Documentation

**Files:**
- Create: `packaging/windows/installer.iss` (Inno Setup script)
- Create: `packaging/macos/build-app.sh`
- Create: `packaging/macos/Info.plist`
- Create: `packaging/linux/build-deb.sh`
- Create: `packaging/linux/zien-display-fixer.desktop`
- Create: `Properties/PublishProfiles/win-x64.pubxml`
- Create: `Properties/PublishProfiles/osx-arm64.pubxml`
- Create: `Properties/PublishProfiles/osx-x64.pubxml`
- Create: `Properties/PublishProfiles/linux-x64.pubxml`
- Create: `README.md`

**Interfaces:**
- Produces: Self-contained, single-file publish artifacts for Windows, macOS, and Linux; distribution scripts; README test matrix.

- [ ] **Step 1: Configure .NET 10 publish profiles**
  Create publish profiles for `win-x64`, `osx-arm64`, `osx-x64`, `linux-x64` with `<PublishSingleFile>true</PublishSingleFile>`, `<SelfContained>true</SelfContained>`, and `<PublishReadyToRun>true</PublishReadyToRun>`.

- [ ] **Step 2: Create Inno Setup script for Windows**
  Script installs executable, registers uninstall entry, adds desktop and start menu shortcuts, and registers optional autostart.

- [ ] **Step 3: Create macOS `.app` packager and Linux `.deb` packager**
  Scripts bundle icon, Info.plist, and desktop metadata.

- [ ] **Step 4: Write `README.md`**
  Document project overview, prerequisites (.NET 10 SDK), build commands, test execution (`dotnet test`), release packaging commands, and OS testing matrix table (Windows, macOS, Linux).

- [ ] **Step 5: Run full test suite and verify build**
  Run: `dotnet test`
  Run: `dotnet publish src/ZienDisplayFixer.App/ZienDisplayFixer.App.csproj -c Release -r win-x64 --no-self-contained`

- [ ] **Step 6: Commit**
  ```bash
  git add packaging/ Properties/ README.md
  git commit -m "feat: add multi-platform publish profiles, installer scripts, and documentation"
  ```

