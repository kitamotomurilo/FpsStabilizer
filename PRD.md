# Product Requirements Document (PRD)
## FPS Stabilizer — Game Performance Profile Manager for Windows

---

## 1. Executive Summary

**Product Name:** FPS Stabilizer  
**Version:** 1.0.0  
**Platform:** Windows 10/11 (x64)  
**Framework:** .NET 8, WPF, MVVM (CommunityToolkit.Mvvm)  
**License:** MIT  

### 1.1 Vision Statement
FPS Stabilizer is a user-mode Windows application that empowers gamers to create, manage, and automatically apply performance profiles for their games. It centralizes FPS limiting (via RTSS), process priority management, CPU affinity configuration, and Windows gaming optimizations into a single, lightweight system tray utility.

### 1.2 Problem Statement
Gamers currently manage performance settings through fragmented tools:
- **RTSS** for FPS limiting (manual per-game profiles)
- **Task Manager** for process priority/affinity (manual, non-persistent)
- **Windows Settings** for Game Mode, GPU Scheduling (buried in menus)
- **No unified solution** for automatic profile application when games launch

### 1.3 Solution
A unified desktop application with:
- **Background service** (system tray) for continuous game detection
- **Rich WPF UI** for profile creation/management
- **Automatic profile application** when target games are detected
- **Integration with Windows gaming features** (Game Mode, GPU Scheduling, Power Plans)
- **RTSS integration** for precise frame rate limiting

---

## 2. Target Audience

| Segment | Description | Needs |
|---------|-------------|-------|
| **Competitive Gamers** | CS2, Valorant, League, Dota 2 players | Low latency, stable FPS, P-core affinity |
| **Content Creators** | Streamers, benchmarkers | Consistent frame pacing, GPU scheduling |
| **Laptop Gamers** | Battery-conscious, thermal-constrained | Power saving profiles, E-core affinity |
| **Enthusiasts/Tweakers** | Hardware enthusiasts | Full control, export/import, automation |

---

## 3. Core Features

### 3.1 Game Profile Management
**User Story:** As a gamer, I want to create profiles per game with FPS limit, priority, and CPU affinity settings.

| Feature | Description | Priority |
|---------|-------------|----------|
| **Profile CRUD** | Create, read, update, delete game profiles | P0 |
| **Profile Fields** | Name, Process Name, Executable Path, Launch Args, Working Directory | P0 |
| **FPS Limit (RTSS)** | 30-360 FPS slider + presets (30/60/120/144/240/Unlimited) | P0 |
| **Process Priority** | Idle → Below Normal → Normal → Above Normal → High → RealTime | P0 |
| **CPU Affinity** | 6 modes: All Cores, Specific Cores, Performance Cores Only, Efficiency Cores Only, Exclude Core 0, Custom | P0 |
| **Environment Variables** | Per-profile env vars (e.g., `DXVK_HUD=1`, `MESA_GL_VERSION_OVERRIDE=4.6`) | P1 |
| **Auto-Apply Toggle** | Automatically apply when matching game detected | P0 |
| **Import/Export** | JSON-based profile sharing/backup | P1 |
| **Default Profiles** | 3 presets: Competitive (144 FPS, High, P-cores), Balanced (60 FPS, Above Normal, Exclude Core 0), Power Saving (30 FPS, Normal, E-cores) | P0 |

### 3.2 Automatic Game Detection & Profile Application
**User Story:** As a gamer, I want profiles applied automatically when I launch a game without manual intervention.

| Feature | Description | Priority |
|---------|-------------|----------|
| **Background Monitoring** | System tray service scans every 3s (configurable 1-60s) | P0 |
| **Detection Heuristics** | Visible window + known game process names + GPU activity | P0 |
| **Process Matching** | By process name (case-insensitive) + optional executable path | P0 |
| **Auto-Apply** | Priority + Affinity + RTSS FPS limit + restore on exit | P0 |
| **State Restoration** | Original priority/affinity restored when game closes | P0 |
| **Real-time Updates** | CPU%, Memory, Uptime, Fullscreen/Focused status in UI | P1 |

### 3.3 RTSS Integration (RivaTuner Statistics Server)
**User Story:** As a gamer, I want precise frame rate limiting without in-game limiters adding latency.

| Feature | Description | Priority |
|---------|-------------|----------|
| **Auto-Detection** | Scans Program Files, LocalAppData, Registry for RTSS.exe | P0 |
| **Manual Override** | Browse for custom RTSS path | P0 |
| **Profile Application** | `RTSS.exe -p "ProcessName" -f <FPS>` command-line integration | P0 |
| **Profile Cleanup** | Removes limit (`-f 0`) when game closes or profile removed | P0 |
| **Status Indicator** | Green/Red status in UI showing RTSS availability | P0 |
| **Mock Service** | Development/testing without RTSS installed | P1 |

### 3.4 System Tray Background Service
**User Story:** As a user, I want monitoring to continue when I close the main window.

| Feature | Description | Priority |
|---------|-------------|----------|
| **Tray Icon** | Custom icon with status tooltip (Monitoring: Active/Stopped) | P0 |
| **Context Menu** | Start/Stop monitoring, Open Main UI, Quick Profiles toggle, Settings, Exit | P0 |
| **Auto-Start** | Registry `HKCU\...\Run` entry with `--minimized` flag | P0 |
| **Single Instance** | Mutex prevents duplicate service/UI instances | P0 |
| **Balloon Notifications** | Errors, status changes | P1 |

### 3.5 Windows Gaming Optimizations
**User Story:** As a gamer, I want quick access to Windows gaming settings from one place.

| Feature | Description | Priority |
|---------|-------------|----------|
| **Game Mode Toggle** | Reads/writes `HKCU\Software\Microsoft\GameBar\AllowAutoGameMode` | P1 |
| **GPU Scheduling** | Reads/writes `HKLM\...\GraphicsDrivers\HwSchMode` (requires Admin) | P1 |
| **Quick Links** | Open Windows Game Mode Settings, Graphics Settings, Power Options | P1 |
| **Status Indicators** | Visual green/red dots showing current state | P1 |

### 3.6 Rich WPF UI (MVVM)
**User Story:** As a user, I want a modern, responsive, dark-themed interface.

| Feature | Description | Priority |
|---------|-------------|----------|
| **Main Window** | Split view: Detected Games (left) + Profiles (right) with GridSplitter | P0 |
| **Profile Editor** | Modal dialog with tabs: Basic, FPS, Priority, Affinity (visual core grid), Advanced | P0 |
| **Settings Window** | General, RTSS, Windows Optimizations, Advanced, About | P0 |
| **Dark Theme** | Custom Colors.xaml palette (VS Code-inspired) | P0 |
| **Converters** | Bool↔Visibility, Null↔Visibility, Enum↔Bool, Value→Color (thresholds), Bytes→String, TimeSpan→String | P0 |
| **Responsive Layout** | Min 900x600, resizable, centered | P0 |

---

## 4. Technical Architecture

### 4.1 Solution Structure
```
FpsStabilizer.sln
├── src/
│   ├── FpsStabilizer.Core/          # Class Library (net8.0-windows)
│   │   ├── Models/
│   │   │   ├── GameProfile.cs       # Profile entity + enums
│   │   │   ├── DetectedGame.cs      # Runtime game detection + SystemHardwareInfo
│   │   └── Services/
│   │       ├── IRtssService.cs      # RTSS integration interface
│   │       ├── RtssService.cs       # RTSS implementation + MockRtssService
│   │       ├── IGameDetectionService.cs  # Game monitoring interface
│   │       ├── GameDetectionService.cs   # Timer-based detection + profile application
│   │       ├── IProcessService.cs   # Process manipulation interface
│   │       ├── ProcessService.cs    # Priority, affinity, hardware info (WMI)
│   │       ├── IProfileService.cs   # Profile persistence interface
│   │       └── ProfileService.cs    # JSON file storage (%LOCALAPPDATA%\FpsStabilizer\profiles.json)
│   ├── FpsStabilizer.UI/            # WPF App (net8.0-windows, WinExe)
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs     # Main window logic + messenger
│   │   │   ├── ProfileEditorViewModel.cs  # Profile CRUD + core grid
│   │   │   └── SettingsViewModel.cs # Settings + registry + system info
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml      # Split view + ListViews + GridView
│   │   │   ├── ProfileEditorWindow.xaml  # Modal with core checkbox grid
│   │   │   └── SettingsWindow.xaml  # Grouped settings sections
│   │   ├── Converters/
│   │   │   └── CommonConverters.cs  # 10 value converters
│   │   ├── Styles/
│   │   │   └── Colors.xaml          # Dark theme palette + brushes
│   │   └── App.xaml.cs              # DI container + single instance + startup args
│   └── FpsStabilizer.Service/       # Console/WinForms Host (net8.0-windows, Exe)
│       └── Program.cs               # Generic Host + TrayHostedService (NotifyIcon)
├── assets/
│   └── icon.ico                     # Application icon
├── build.ps1                        # Build script
└── README.md                        # Documentation
```

### 4.2 Dependency Injection & Service Registration

**UI App (App.xaml.cs):**
```csharp
services.AddSingleton<IRtssService, RtssService>();
services.AddSingleton<IProcessService, ProcessService>();
services.AddSingleton<IProfileService, ProfileService>();
services.AddSingleton<IGameDetectionService, GameDetectionService>();
services.AddTransient<MainViewModel>();
services.AddTransient<ProfileEditorViewModel>();
services.AddTransient<SettingsViewModel>();
services.AddTransient<MainWindow>();
services.AddTransient<ProfileWindow>();
services.AddTransient<SettingsWindow>();
```

**Background Service (Program.cs):**
```csharp
builder.Services.AddSingleton<IRtssService, RtssService>();
builder.Services.AddSingleton<IProcessService, ProcessService>();
builder.Services.AddSingleton<IProfileService, ProfileService>();
builder.Services.AddSingleton<IGameDetectionService, GameDetectionService>();
builder.Services.AddHostedService<TrayHostedService>();
```

### 4.3 Key Data Models

#### GameProfile (Core Entity)
```csharp
public class GameProfile
{
    Guid Id { get; set; }
    string Name { get; set; }
    string ProcessName { get; set; }      // e.g., "cs2", "valorant"
    string? ExecutablePath { get; set; }  // Optional full path
    int? FpsLimit { get; set; }           // 30-360, null = unlimited
    bool UseRtss { get; set; } = true;
    ProcessPriority Priority { get; set; } = Normal;
    CpuAffinityConfig CpuAffinity { get; set; }
    Dictionary<string, string> EnvironmentVariables { get; set; }
    string LaunchArguments { get; set; }
    string? WorkingDirectory { get; set; }
    bool AutoApply { get; set; } = true;
    bool IsActive { get; set; } = true;
    DateTime CreatedAt, UpdatedAt { get; set; }
}
```

#### CpuAffinityConfig (Flexible CPU Pinning)
```csharp
public class CpuAffinityConfig
{
    bool Enabled { get; set; }
    AffinityMode Mode { get; set; }  // AllCores, SpecificCores, PerformanceCoresOnly, EfficiencyCoresOnly, ExcludeCore0, Custom
    List<int> CoreIndices { get; set; }  // For SpecificCores/Custom
    bool ExcludeCore0 { get; set; } = true;
    bool PreferPerformanceCores { get; set; } = true;
    bool PreferEfficiencyCores { get; set; } = false;
}
```

#### DetectedGame (Runtime Detection Result)
```csharp
public class DetectedGame
{
    int ProcessId { get; set; }
    string ProcessName { get; set; }
    string MainWindowTitle { get; set; }
    string ExecutablePath { get; set; }
    DateTime StartTime { get; set; }
    double CpuUsage { get; set; }
    long MemoryUsageMB { get; set; }
    bool IsFullscreen { get; set; }
    bool IsFocused { get; set; }
    double? GpuUsage { get; set; }
    Guid? MatchedProfileId { get; set; }
    string? MatchedProfileName { get; set; }
    TimeSpan Uptime => DateTime.Now - StartTime;
}
```

#### SystemHardwareInfo (WMI Queries)
```csharp
public class SystemHardwareInfo
{
    string CpuName { get; set; }
    int CpuCores { get; set; }
    int CpuLogicalProcessors { get; set; }
    bool HasPerformanceCores { get; set; }  // Intel hybrid detection
    int PerformanceCoreCount { get; set; }
    int EfficiencyCoreCount { get; set; }
    string GpuName { get; set; }
    GpuVendor GpuVendor { get; set; }
    double TotalMemoryGB { get; set; }
    string OsVersion { get; set; }
    bool IsGameModeEnabled { get; set; }
    bool IsGpuSchedulingEnabled { get; set; }
}
```

### 4.4 Core Algorithms

#### CPU Affinity Calculation (ProcessService.GetRecommendedCores)
```csharp
// Performance Cores Only (Intel hybrid): even-indexed logical processors (0,2,4...)
// Efficiency Cores Only: odd-indexed (1,3,5...)
// Exclude Core 0: all except 0
// Custom: user-selected CoreIndices
// Bitmask: 1L << coreIndex for each core
```

#### Game Detection Heuristics (ProcessService.DetectGamesAsync)
```csharp
// 1. Get all processes
// 2. Filter out system processes (System, smss, csrss, svchost, dwm, etc.)
// 3. Filter for: Has MainWindowHandle OR Known game name patterns
// 4. For each: Create DetectedGame, get CPU/Memory, check fullscreen (Win32 API)
```

#### Profile Matching (GameDetectionService.RefreshAsync)
```csharp
// On new game detected:
// 1. ProfileService.GetByProcessNameAsync(game.ProcessName)
// 2. If match && profile.AutoApply:
//    - ProcessService.ApplyProfileAsync(profile, processId)
//    - RtssService.ApplyProfileAsync(profile)
//    - Track in _appliedProfiles[pid] = profile
// On game closed:
// 1. ProcessService.RestoreDefaultsAsync(pid)
// 2. RtssService.ClearProfileAsync(profile)
```

---

## 5. User Flows

### 5.1 First-Time Setup
```
1. User downloads/installs FPS Stabilizer
2. Launches app → MainWindow opens
3. App checks RTSS → Status shows "RTSS: [path]" or "Não encontrado"
4. User clicks "Config" → SettingsWindow
5. If RTSS missing: User clicks "Procurar..." → selects RTSS.exe
6. User clicks "Perfis" → ProfileEditorWindow → "Novo Perfil"
7. Fills: Name="CS2 Competitivo", Process="cs2", FPS=144, Priority=High, Affinity=Performance Cores Only, AutoApply=ON
8. Clicks "Salvar" → Profile appears in list
9. Launches CS2 → Service detects → Applies profile automatically
10. Status bar shows "Perfil aplicado a cs2"
```

### 5.2 Background Service Operation
```
1. User closes MainWindow (minimizes to tray if configured)
2. TrayHostedService continues running
3. Every 3s: GameDetectionService.RefreshAsync()
4. New game detected → Profile matched → Auto-applied
5. Game closes → Settings restored
6. User right-clicks tray → "Abrir Interface Principal" → MainWindow shows
```

### 5.3 Profile Export/Import (Sharing)
```
1. User clicks "Exportar" in MainWindow
2. SaveFileDialog → profiles.json with metadata (version, timestamp)
3. User shares file with friend
4. Friend clicks "Importar" → OpenFileDialog
5. Profiles imported with new GUIDs → Added to their list
```

---

## 6. Non-Functional Requirements

### 6.1 Performance
| Metric | Target |
|--------|--------|
| **Detection Interval** | Configurable 1-60s (default 3s) |
| **Profile Apply Latency** | < 500ms from detection to application |
| **Memory Usage (Service)** | < 50 MB |
| **Memory Usage (UI)** | < 100 MB |
| **CPU Impact (Idle)** | < 0.5% |
| **Startup Time (UI)** | < 2s cold start |
| **Startup Time (Service)** | < 1s |

### 6.2 Reliability
| Requirement | Implementation |
|-------------|----------------|
| **Single Instance** | Named Mutex (`FpsStabilizerSingleInstance`, `FpsStabilizerServiceSingleInstance`) |
| **State Persistence** | Profiles: JSON file (atomic write via .tmp + Move); Settings: Registry |
| **Crash Recovery** | Original priority/affinity restored on service stop via `_originalStates` dictionary |
| **Error Handling** | Try/catch at all boundaries; errors logged to Event Viewer + UI status |
| **Admin Rights** | Not required for core features; only for GPU Scheduling toggle |

### 6.3 Compatibility
| Platform | Support |
|----------|---------|
| **Windows 10** | 1809+ (build 17763+) |
| **Windows 11** | All versions |
| **Architecture** | x64 only (ARM64 untested) |
| **.NET Runtime** | .NET 8 (Microsoft.DotNet.Runtime.8) |
| **RTSS** | Any version supporting CLI args |
| **Anti-cheat Games** | May block priority/affinity changes (kernel-level) |

### 6.4 Security
| Aspect | Approach |
|--------|----------|
| **Process Manipulation** | Uses `Process.GetProcessById` + `PriorityClass`/`ProcessorAffinity` (requires same/user permissions) |
| **Registry Access** | `HKCU` for user settings; `HKLM` only for GPU Scheduling (UAC prompt) |
| **File Storage** | `%LOCALAPPDATA%\FpsStabilizer\` (user-writable) |
| **No Network Access** | Fully offline (update check placeholder only) |
| **No Telemetry** | Only Windows Event Log for errors |

---

## 7. UI/UX Specification

### 7.1 Color Palette (Colors.xaml)
```xml
<!-- Primary -->
PrimaryColor:       #007ACC
PrimaryDarkColor:   #005A9E
PrimaryLightColor:  #4EC9F0

<!-- Semantic -->
SuccessColor:       #4CAF50
WarningColor:       #FF9800
ErrorColor:         #F44336
InfoColor:          #2196F3

<!-- Surfaces (Dark Theme) -->
BackgroundColor:    #1E1E1E
SurfaceColor:       #252526
SurfaceVariantColor:#2D2D2D
CardBackgroundColor:#2D2D30
BorderColor:        #3E3E42

<!-- Text -->
TextPrimaryColor:   #FFFFFF
TextSecondaryColor: #CCCCCC
TextMutedColor:     #888888
TextDisabledColor:  #666666
```

### 7.2 Key Converters
| Converter | Use Case |
|-----------|----------|
| `BoolToVisibilityConverter` | Show/hide UI elements |
| `NullToVisibilityConverter` | Show/hide based on null |
| `EnumToBoolConverter` | RadioButton binding to enum |
| `ValueToColorConverter` | CPU/Memory thresholds → Green/Yellow/Red |
| `BytesToStringConverter` | Memory bytes → "1.2 GB" |
| `TimeSpanToStringConverter` | Uptime → "2h 34m 12s" |
| `ProcessPriorityToStringConverter` | Enum → "High", "Real Time" |
| `AffinityModeToStringConverter` | Enum → "Apenas Performance" |

### 7.3 Window Specifications

| Window | Size | Resizable | Key Controls |
|--------|------|-----------|--------------|
| **MainWindow** | 1100x700 (min 900x600) | Yes | Split ListView + GridView, GridSplitter, Toolbar, StatusBar |
| **ProfileEditorWindow** | 650x750 (min 600x650) | Yes | ScrollViewer, GroupBoxes, Core CheckBox WrapPanel, Preset FPS buttons |
| **SettingsWindow** | 550x600 (min 500x550) | Yes | GroupBoxes, Slider, Status Ellipses, Command Buttons |

---

## 8. Configuration & Storage

### 8.1 File Locations
| Data | Location |
|------|----------|
| **Profiles** | `%LOCALAPPDATA%\FpsStabilizer\profiles.json` |
| **Settings** | `HKCU\Software\FpsStabilizer` |
| **Auto-Start** | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `"FpsStabilizer" = "path --minimized"` |
| **Logs** | Windows Event Viewer → Applications and Services Logs → FpsStabilizer |

### 8.2 Settings Registry Keys
| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `StartWithWindows` | DWORD | 0 | Auto-start with Windows |
| `StartMinimized` | DWORD | 0 | Start minimized to tray |
| `MinimizeToTray` | DWORD | 1 | Minimize to tray on close |
| `AutoStartMonitoring` | DWORD | 1 | Start monitoring on launch |
| `DetectionIntervalSeconds` | DWORD | 3 | Scan interval (1-60) |
| `EnableRtssIntegration` | DWORD | 1 | Use RTSS for FPS limiting |
| `RtssPath` | STRING | "" | Custom RTSS.exe path |

### 8.3 Profile JSON Format
```json
[
  {
    "id": "guid",
    "name": "CS2 Competitivo",
    "processName": "cs2",
    "executablePath": null,
    "fpsLimit": 144,
    "useRtss": true,
    "priority": 4,
    "cpuAffinity": {
      "enabled": true,
      "mode": 2,
      "coreIndices": [],
      "excludeCore0": true,
      "preferPerformanceCores": true,
      "preferEfficiencyCores": false
    },
    "environmentVariables": {},
    "launchArguments": "",
    "workingDirectory": null,
    "autoApply": true,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z",
    "isActive": true
  }
]
```

---

## 9. Known Limitations & Constraints

### 9.1 Technical Limitations
| Limitation | Impact | Workaround |
|------------|--------|------------|
| **No Overlay** | No in-game FPS display | Use RTSS/Afterburner overlay |
| **No Frame Pacing** | Only limits max FPS, doesn't stabilize frame times | Use Special K, NVIDIA Frame Pacing, AMD Chill |
| **Anti-Cheat Blocking** | Valorant, Faceit, Vanguard block priority/affinity | Run FPS Stabilizer as Admin (limited help) |
| **User-Mode Only** | Cannot modify driver-level scheduling | N/A |
| **No Multi-GPU Management** | Single GPU focus | N/A |
| **Process Name Matching** | Ambiguous if multiple same-name processes | Use ExecutablePath for precision |

### 9.2 Platform Constraints
- **Windows-only** (Win32 APIs, WMI, Registry)
- **x64 only** (no ARM64 build/test)
- **.NET 8 required** (not self-contained by default)
- **RTSS optional but required for FPS limiting**

---

## 10. Future Enhancements (Roadmap)

### 10.1 v1.1 - Quality of Life
- [ ] **Process Tree Detection** — Detect child processes (launchers → game)
- [ ] **Profile Templates** — Community-shared profiles for popular games
- [ ] **Per-Profile Detection Interval** — Aggressive for competitive, relaxed for single-player
- [ ] **Hotkey Support** — Global hotkeys for quick profile toggle

### 10.2 v1.2 - Advanced Features
- [ ] **Per-Game Power Plans** — Switch power plan on game launch/close
- [ ] **NVIDIA/AMD Control Panel Integration** — Apply driver profiles via NVAPI/ADL
- [ ] **Frame Time Logging** — CSV export for PresentMon/CapFrameX analysis
- [ ] **Discord Rich Presence** — Show current game + profile in Discord status

### 10.3 v2.0 - Platform Expansion
- [ ] **Linux Support** — via Wine/Proton + MangoHud/Gamemode
- [ ] **Web Dashboard** — Remote monitoring via local HTTP API
- [ ] **Plugin System** — Custom detection/apply logic via .NET assemblies
- [ ] **Cloud Sync** — Profile backup to GitHub Gist/OneDrive

---

## 11. Build & Deployment

### 11.1 Build Commands
```powershell
# Prerequisites
winget install Microsoft.DotNet.SDK.8

# Build Solution
dotnet build FpsStabilizer.sln --configuration Release

# Output Locations
# UI:        src/FpsStabilizer.UI/bin/Release/net8.0-windows/FpsStabilizer.exe
# Service:   src/FpsStabilizer.Service/bin/Release/net8.0-windows/FpsStabilizer.Service.exe
```

### 11.2 Publishing (Single File)
```powershell
dotnet publish FpsStabilizer.sln -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

### 11.3 Installation
- **User Install:** Copy `publish/` folder → Run `FpsStabilizer.exe`
- **Auto-Start:** Enable in Settings → "Iniciar com o Windows"
- **Uninstall:** Delete folder + remove Run registry key

---

## 12. Testing Strategy

### 12.1 Unit Tests (Planned)
| Module | Test Cases |
|--------|------------|
| `ProfileService` | Load/Save/Import/Export JSON, default profiles |
| `ProcessService` | Affinity mask calculation, core selection logic, hardware detection |
| `RtssService` | Command building, mock service behavior |
| `GameDetectionService` | New/closed game events, profile matching, state restoration |

### 12.2 Integration Tests
- **RTSS Integration:** Verify CLI commands create/update/remove profiles
- **Process Manipulation:** Verify priority/affinity changes persist
- **Service Lifecycle:** Start/stop monitoring, tray menu actions
- **Profile Persistence:** Survive app restart, service restart, Windows reboot

### 12.3 Manual Test Scenarios
1. **Clean Install** → First run → Create profile → Launch game → Verify apply
2. **Service Only** → Start service → Close UI → Launch game → Verify background apply
3. **Auto-Start** → Enable → Reboot → Verify service running in tray
4. **RTSS Missing** → Uninstall RTSS → Verify graceful degradation
5. **Profile Import/Export** → Round-trip JSON → Verify all fields preserved
6. **Anti-Cheat Game** → Launch Valorant → Verify no crash, graceful failure

---

## 13. Dependencies

### 13.1 NuGet Packages
| Package | Version | Project | Purpose |
|---------|---------|---------|---------|
| `CommunityToolkit.Mvvm` | 8.2.2 | UI | MVVM source generators, ObservableObject, RelayCommand |
| `Microsoft.Extensions.DependencyInjection` | 8.0.0 | UI, Service | DI container |
| `Microsoft.Extensions.Hosting` | 8.0.0 | Service | Generic Host, IHostedService |
| `Microsoft.Extensions.Logging` | 8.0.0 | All | Structured logging |
| `Microsoft.Extensions.Logging.EventLog` | 8.0.0 | Service | Windows Event Log provider |
| `Microsoft.Extensions.Logging.Console` | 8.0.0 | UI | Console logging |
| `Microsoft.Extensions.Logging.Debug` | 8.0.0 | UI | Debug output logging |
| `Microsoft.Xaml.Behaviors.Wpf` | 1.1.77 | UI | Blend behaviors for XAML |
| `Hardcodet.NotifyIcon.Wpf` | 1.1.0 | UI, Service | System tray NotifyIcon |
| `Newtonsoft.Json` | 13.0.3 | Core | JSON serialization |
| `System.Management` | 8.0.0 | Core | WMI queries for hardware info |

### 13.2 External Dependencies
| Dependency | Version | Required | Purpose |
|------------|---------|----------|---------|
| **Windows 10/11** | 1809+ | Yes | OS platform |
| **.NET 8 Runtime** | 8.0.x | Yes | Execution runtime |
| **RTSS (RivaTuner Statistics Server)** | Latest | Optional* | FPS limiting (*required for FPS limit feature) |
| **MSI Afterburner** | Latest | Optional | Common RTSS bundle |

---

## 14. Appendix: Code Reference Map

### Key Entry Points
| File | Purpose |
|------|---------|
| `src/FpsStabilizer.UI/App.xaml.cs` | UI App startup, DI, single instance, window creation |
| `src/FpsStabilizer.Service/Program.cs` | Background service entry, Host builder, TrayHostedService |
| `src/FpsStabilizer.Core/Services/GameDetectionService.cs` | Core monitoring loop, profile matching, application |
| `src/FpsStabilizer.Core/Services/ProcessService.cs` | Process manipulation, hardware detection, affinity logic |
| `src/FpsStabilizer.Core/Services/ProfileService.cs` | Profile persistence, CRUD, import/export |
| `src/FpsStabilizer.Core/Services/RtssService.cs` | RTSS CLI integration, mock service |

### Key ViewModels
| File | Responsibility |
|------|----------------|
| `MainViewModel.cs` | Main window state, game/profile lists, monitoring control, messenger |
| `ProfileEditorViewModel.cs` | Profile editing, core grid, validation, save/cancel |
| `SettingsViewModel.cs` | Settings persistence, registry, system info, Windows optimization commands |

### Key Views
| File | Description |
|------|-------------|
| `MainWindow.xaml` | Split layout: Detected games (ListView) + Profiles (GridView) |
| `ProfileEditorWindow.xaml` | Modal form with core checkbox WrapPanel |
| `SettingsWindow.xaml` | Grouped settings with status indicators |

---

## 15. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2024-11-07 | - | Initial PRD based on current codebase analysis |

---

*This PRD was generated by analyzing the FpsStabilizer source code as of November 2024. It reflects the actual implementation state of the repository.*