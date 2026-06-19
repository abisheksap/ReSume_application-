# ReSume – Final Planning Structure (v1)

---

## 1. Application Overview

### Purpose and Vision

ReSume is a native Windows desktop application that captures and restores
complete desktop sessions, including application states, window positions,
browser tabs, and open documents. It targets a single, frustrating problem:
losing your working context after a shutdown, restart, crash, or update.

### Native Windows Desktop Focus

This is not a cross-platform tool. Every architectural decision optimizes
for Windows APIs, Windows shell integration, and Windows user expectations.
This focus enables deep integration that cross-platform frameworks cannot
achieve.

### Offline-First Philosophy

ReSume never requires an internet connection to function. All session data
is stored locally. No accounts, no telemetry, no cloud dependencies. The
application works identically whether the machine is connected or air-gapped.

---

## 2. Product Scope

| Decision             | Choice                  | Rationale                                      |
|----------------------|-------------------------|------------------------------------------------|
| Platform             | Windows only `.exe`     | Deep OS integration via Win32 APIs             |
| Framework            | C# / .NET 8 / WPF       | Modern, performant, native Windows UI          |
| Rejected: Electron   | Firm no                 | Memory overhead, no native API depth           |
| Rejected: SaaS       | Firm no                 | Contradicts offline-first philosophy           |
| Rejected: Web app    | Firm no                 | Cannot access OS-level window state            |
| Rejected: Cloud      | Firm no                 | Privacy, reliability, simplicity               |

### Key Constraint

Every feature must function without network access. If a feature cannot
work offline, it does not belong in the core product.

---

## 3. Core Features

### 3.1 Save Current Session

Captures the complete state of the user's desktop at the moment of invocation.

**What gets captured:**
- All visible, restorable application windows
- Window positions, sizes, and monitor assignments
- Per-application document or file paths where detectable
- All Chrome browser windows, tab URLs, tab order, and active tab per window
- Chrome profile associations for each browser window
- User-assigned session label and optional notes

**Trigger methods:**
- Main window button
- System tray menu
- Keyboard shortcut (configurable)
- Pre-shutdown automatic save

**Behavior:**
- Saving is fast and non-blocking to the user
- A progress indicator shows capture status
- Partial captures save what is available and log what was missed
- Each save produces a single versioned JSON file

---

### 3.2 Restore Session

Reconstructs a previously saved session as closely as possible.

**Restoration sequence:**
1. Parse selected session file
2. Launch applications in dependency order
3. Restore window positions and sizes
4. Adjust for current monitor configuration
5. Restore browser windows and tabs via Chrome extension
6. Report restoration results to user

**Handling edge cases:**
- Application not installed → skip, log, notify user
- Monitor no longer connected → remap windows to available displays
- File no longer exists → open application without file, notify user
- Chrome profile not present → prompt user, offer to restore into default profile
- Window position off-screen → snap to nearest visible monitor edge

**User controls:**
- Restore entire session
- Restore selected applications only
- Preview session contents before restoring
- Dry-run mode showing what would happen without executing

---

### 3.3 History Browser

Provides a browsable, searchable archive of all saved sessions.

**Displayed information per session:**
- Session label
- Save timestamp
- Application count
- Tab count
- File size
- Auto-save vs. manual indicator

**Actions available:**
- Restore full session
- Restore partial session (selected apps only)
- Rename session
- Delete session
- Duplicate session
- Export session file
- Compare two sessions (future enhancement)

**Storage management:**
- Configurable maximum session count
- Configurable maximum storage size
- Automatic pruning of oldest auto-saves when limits are reached
- Manual saves are never auto-pruned

---

### 3.4 Save & Shutdown

Combines session capture with a system shutdown command.

**Sequence:**
1. Trigger full session save
2. Wait for save confirmation (including Chrome extension response)
3. Set timeout failsafe (configurable, default 30 seconds)
4. If save succeeds → initiate `shutdown /s /t 0`
5. If save times out → prompt user to shutdown without complete save or cancel
6. On next boot, if startup integration is enabled, offer to restore last session

**Variants:**
- Save & Shutdown
- Save & Restart
- Save & Sleep

---

### 3.5 System Tray Integration

ReSume runs as a tray application for persistent, unobtrusive access.

**Tray icon states:**
- Idle (default icon)
- Saving in progress (animated or alternate icon)
- Error state (warning overlay)
- Chrome extension disconnected (indicator badge)

**Tray context menu:**
Save Current Session
Restore Last Session
─────────────────────
Save & Shutdown
Save & Restart
─────────────────────
Open ReSume
Settings
─────────────────────
Exit

**Behavior:**
- Double-click tray icon opens main window
- Single-click shows context menu
- Minimize to tray is configurable (default: enabled)
- Close button behavior is configurable (close to tray vs. exit)

---

## 4. Technology Stack

| Technology                    | Purpose                                                  |
|-------------------------------|----------------------------------------------------------|
| WPF                           | Desktop UI framework with XAML-based layout              |
| .NET 8                        | Runtime, modern C# features and performance              |
| P/Invoke                      | Direct calls to Win32 APIs                               |
| Named Pipes                   | IPC between ReSume.exe and ReSume.NativeHost.exe         |
| System.Text.Json              | Session serialization and deserialization                |
| WiX / MSIX                    | Professional Windows installer packaging                 |
| Chrome Extension (MV3)        | Browser tab capture and restoration                      |

### Why These Choices

- **WPF over WinUI 3:** WPF is mature, stable, and has comprehensive tooling.
- **Named Pipes over HTTP localhost:** Faster, no port management, built-in
  Windows security. No firewall prompts.
- **System.Text.Json over Newtonsoft:** Native to .NET 8, faster, smaller
  dependency footprint.
- **WiX over raw MSIX:** Fine-grained control over registry entries, file
  placement, and custom actions needed for Native Messaging Host registration.

---

## 5. Architecture

### 5.1 Component Diagram
┌──────────────────────────────────────────────────────┐
│ User's Desktop │
│ │
│ ┌─────────────┐ ┌─────────────┐ ┌────────────┐ │
│ │ Notepad++ │ │ VS Code │ │ Explorer │ │
│ └─────────────┘ └─────────────┘ └────────────┘ │
│ │
│ ┌───────────────────────────────────────────────┐ │
│ │ Chrome Browser │ │
│ │ ┌──────────┐ ┌──────────┐ ┌────────────┐ │ │
│ │ │ Personal │ │ Work │ │ School │ │ │
│ │ │ Window 1 │ │ Window 1 │ │ Window 1 │ │ │
│ │ │ Tab A │ │ Tab D │ │ Tab G │ │ │
│ │ │ Tab B │ │ Tab E │ │ Tab H │ │ │
│ │ │ Window 2 │ │ Tab F │ │ │ │ │
│ │ │ Tab C │ │ │ │ │ │ │
│ │ └──────────┘ └──────────┘ └────────────┘ │ │
│ │ Chrome Extension (per profile) │ │
│ └───────────────────┬───────────────────────────┘ │
│ │ Chrome Native Messaging │
│ ┌──────────▼──────────┐ │
│ │ ReSume.NativeHost │ │
│ │ (per profile) │ │
│ └──────────┬──────────┘ │
│ │ Named Pipes │
│ ┌──────────▼──────────┐ │
│ │ ReSume.exe │ │
│ │ ┌───────────────┐ │ │
│ │ │ Session Mgr │ │ │
│ │ │ Window Enum │ │ │
│ │ │ Restore Eng │ │ │
│ │ │ Pipe Server │ │ │
│ │ │ Tray Service │ │ │
│ │ └───────────────┘ │ │
│ └─────────────────────┘ │
└──────────────────────────────────────────────────────┘

### 5.2 Main Components

**ReSume.exe**
The primary application. Houses the UI, session management logic, window
enumeration, restoration engine, and Named Pipe server.

**Session Manager**
Internal module within ReSume.exe responsible for creating, storing, loading,
and pruning session files. Handles JSON serialization and file I/O.

**ReSume.NativeHost.exe**
A lightweight console application that Chrome launches via Native Messaging.
Receives tab data from the Chrome extension over stdin/stdout and relays it
to ReSume.exe via Named Pipes. One instance runs per Chrome profile.

**Chrome Extension**
A Manifest V3 extension that captures all open tabs, URLs, window groupings,
and active tab state. Communicates with ReSume.NativeHost.exe through
Chrome's Native Messaging API.

### 5.3 Multi-Profile Architecture
ReSume.exe (single instance)
│
├── Named Pipe: resume_pipe_personal
│ └── ReSume.NativeHost.exe (Personal profile)
│ └── Chrome Extension (Personal profile)
│
├── Named Pipe: resume_pipe_work
│ └── ReSume.NativeHost.exe (Work profile)
│ └── Chrome Extension (Work profile)
│
└── Named Pipe: resume_pipe_school
└── ReSume.NativeHost.exe (School profile)
└── Chrome Extension (School profile)

**Profile discovery:**
ReSume scans `%LocalAppData%\Google\Chrome\User Data\` for profile
directories and reads `Preferences` JSON files to extract human-readable
profile names.

---

## 6. Session Data Models

### 6.1 Session JSON Schema

```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "label": "Friday afternoon work session",
  "createdAt": "2025-01-17T16:30:00Z",
  "version": "1.0",
  "source": "manual",
  "metadata": {
    "machineName": "DESKTOP-ABC123",
    "username": "jsmith",
    "osVersion": "Windows 11 23H2",
    "resumeVersion": "1.0.0",
    "monitorConfiguration": [
      {
        "deviceName": "\\\\.\\DISPLAY1",
        "bounds": { "x": 0, "y": 0, "width": 2560, "height": 1440 },
        "isPrimary": true,
        "scaleFactor": 1.25
      },
      {
        "deviceName": "\\\\.\\DISPLAY2",
        "bounds": { "x": 2560, "y": 0, "width": 1920, "height": 1080 },
        "isPrimary": false,
        "scaleFactor": 1.0
      }
    ]
  },
  "applications": [
    {
      "processName": "notepad++",
      "executablePath": "C:\\Program Files\\Notepad++\\notepad++.exe",
      "commandLine": "\"C:\\Program Files\\Notepad++\\notepad++.exe\" \"C:\\notes.txt\"",
      "windows": [
        {
          "title": "notes.txt - Notepad++",
          "position": { "x": 100, "y": 200, "width": 800, "height": 600 },
          "monitor": "\\\\.\\DISPLAY1",
          "windowState": "Normal",
          "isTopmost": false,
          "zOrder": 3
        }
      ],
      "documentPaths": ["C:\\Users\\jsmith\\notes.txt"],
      "restorationHint": "command_line"
    }
  ],
  "browserProfiles": [
    {
      "profileName": "Personal",
      "profileDirectory": "Default",
      "profileId": "profile_personal_abc123",
      "browserWindows": [
        {
          "windowId": 1,
          "position": { "x": 0, "y": 0, "width": 1280, "height": 900 },
          "monitor": "\\\\.\\DISPLAY1",
          "windowState": "Maximized",
          "isIncognito": false,
          "activeTabIndex": 2,
          "tabs": [
            {
              "index": 0,
              "url": "https://github.com",
              "title": "GitHub",
              "isPinned": false,
              "isMuted": false,
              "groupId": -1,
              "groupTitle": null,
              "groupColor": null
            },
            {
              "index": 1,
              "url": "https://stackoverflow.com",
              "title": "Stack Overflow",
              "isPinned": true,
              "isMuted": false,
              "groupId": -1,
              "groupTitle": null,
              "groupColor": null
            },
            {
              "index": 2,
              "url": "https://learn.microsoft.com",
              "title": "Microsoft Learn",
              "isPinned": false,
              "isMuted": false,
              "groupId": 1,
              "groupTitle": "Research",
              "groupColor": "blue"
            }
          ]
        }
      ]
    },
    {
      "profileName": "Work",
      "profileDirectory": "Profile 1",
      "profileId": "profile_work_def456",
      "browserWindows": [
        {
          "windowId": 2,
          "position": { "x": 2560, "y": 0, "width": 1920, "height": 1080 },
          "monitor": "\\\\.\\DISPLAY2",
          "windowState": "Maximized",
          "isIncognito": false,
          "activeTabIndex": 0,
          "tabs": [
            {
              "index": 0,
              "url": "https://jira.company.com/board",
              "title": "Sprint Board - Jira",
              "isPinned": false,
              "isMuted": false,
              "groupId": -1,
              "groupTitle": null,
              "groupColor": null
            }
          ]
        }
      ]
    }
  ]
}
6.2 Browser Profile Model (C#)
csharp

public sealed class BrowserProfile
{
    public required string ProfileName { get; init; }
    public required string ProfileDirectory { get; init; }
    public required string ProfileId { get; init; }
    public List<BrowserWindow> Windows { get; init; } = [];
    public bool IsConnected { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
}
6.3 File Storage Layout

%LocalAppData%\ReSume\
├── sessions\
│   ├── 2025-01-17_163000_friday-afternoon.json
│   ├── 2025-01-17_120000_auto-save.json
│   └── 2025-01-16_090000_morning-start.json
├── settings\
│   └── config.json
├── logs\
│   ├── resume-2025-01-17.log
│   └── nativehost-2025-01-17.log
└── cache\
    └── profile-mappings.json
7. Detailed Design
7.1 Window Enumeration
Win32 APIs used:

API	Purpose
EnumWindows	Iterate all top-level windows
IsWindowVisible	Filter invisible windows
GetWindowText	Retrieve window title
GetWindowRect	Get position and size
GetWindowPlacement	Get minimized/maximized state
DwmGetWindowAttribute	Detect cloaked UWP windows
GetWindowThreadProcessId	Map window to process ID
GetClassName	Identify window class for filtering
Filtering rules — windows are excluded if:

Not visible (IsWindowVisible returns false)
Cloaked (DwmGetWindowAttribute with DWMWA_CLOAKED)
Window class is in exclusion list
Title is empty and window is not a known application type
Process is ReSume.exe itself
Window area is zero or negative
DPI awareness:
ReSume is Per-Monitor DPI Aware v2. All coordinates are stored in physical
pixels. During restoration, coordinates are adjusted if scale factor changed.

7.2 Document-Aware Restoration
csharp

public interface IAppRestorer
{
    string ProcessName { get; }
    bool CanRestore(ApplicationState app);
    Task<RestoreResult> RestoreAsync(ApplicationState app);
}
Built-in restorers:

Application	Strategy	Detection Method
File Explorer	Launch with folder path	IShellWindows COM interface
Notepad	Launch with file path argument	Command-line argument
Notepad++	Launch with file path argument	Command-line, window title
VS Code	Launch with workspace/folder	--folder-uri or --file-uri
Microsoft Word	Launch with file path	Command-line, window title, DDE
Microsoft Excel	Launch with file path	Command-line, window title
PDF readers	Launch associated app	Command-line, window title
Generic	Launch exe with command line	Captured command line
7.3 Browser Capture & Restore
Capture flow:

ReSume.exe          NativeHost          Chrome Extension
    │                    │                    │
    │  "capture" (pipe)  │                    │
    ├───────────────────►│                    │
    │                    │  native message    │
    │                    ├───────────────────►│
    │                    │                    │ chrome.windows.getAll
    │                    │  tab data response │
    │                    │◄───────────────────┤
    │  tab data (pipe)   │                    │
    │◄───────────────────┤                    │
Restoration flow:

ReSume.exe          NativeHost          Chrome Extension
    │                    │                    │
    │  "restore" + data  │                    │
    ├───────────────────►│                    │
    │                    │  native message    │
    │                    ├───────────────────►│
    │                    │                    │ chrome.windows.create()
    │                    │                    │ chrome.tabs.create()
    │                    │                    │ chrome.tabs.update()
    │                    │  confirmation      │
    │                    │◄───────────────────┤
    │  confirmation      │                    │
    │◄───────────────────┤                    │
What is captured per tab:

URL and title
Index (position in tab strip)
Pinned and muted state
Active state
Tab group ID, title, and color
Discarded state
8. Chrome Extension & Multi-Profile Support
8.1 Chrome Extension
Manifest V3 structure:

JSON

{
  "manifest_version": 3,
  "name": "ReSume Session Helper",
  "version": "1.0.0",
  "description": "Captures and restores browser tabs for ReSume desktop sessions",
  "permissions": [
    "tabs",
    "tabGroups",
    "nativeMessaging"
  ],
  "background": {
    "service_worker": "background.js"
  },
  "icons": {
    "16": "icons/icon16.png",
    "48": "icons/icon48.png",
    "128": "icons/icon128.png"
  },
  "action": {
    "default_popup": "popup.html",
    "default_icon": "icons/icon16.png"
  }
}
Distribution:

Primary: Chrome Web Store
Secondary: Developer mode side-loading for testing and enterprise
Extension popup shows:

Connection status to ReSume desktop app
Current profile name
Manual save trigger button
Link to ReSume diagnostics page
8.2 Multiple Chrome Profiles
Simultaneous connections:

csharp

public sealed class ProfileConnectionManager
{
    private readonly ConcurrentDictionary<string, ProfileConnection> _connections = new();

    public async Task AcceptConnectionAsync(NamedPipeServerStream pipe)
    {
        var handshake = await ReadHandshakeAsync(pipe);
        var connection = new ProfileConnection
        {
            ProfileId = handshake.ProfileId,
            ProfileName = handshake.ProfileName,
            Pipe = pipe,
            ConnectedAt = DateTimeOffset.UtcNow
        };
        _connections.AddOrUpdate(handshake.ProfileId, connection, (_, __) => connection);
    }
}
Missing profile handling:

Scenario	Behavior
Profile exists but extension not installed	Notify user, provide installation guidance
Profile exists but extension not connected	Wait (configurable timeout), then notify
Profile no longer exists	Notify user, offer to redirect tabs
Profile exists, extension connected	Restore normally
9. Installation & Uninstallation
9.1 Installer
Technology: WiX Toolset v4 producing MSI wrapped in bootstrapper EXE.

Installation steps:

Install ReSume.exe to %ProgramFiles%\ReSume\
Install ReSume.NativeHost.exe to %ProgramFiles%\ReSume\NativeHost\
Register Native Messaging Host via registry key and manifest JSON
Create data directories under %LocalAppData%\ReSume\
Offer Windows Startup integration (default: enabled)
Offer Chrome extension installation guidance
Native Messaging Host manifest:

JSON

{
  "name": "com.resume.nativehost",
  "description": "ReSume Native Messaging Host",
  "path": "C:\\Program Files\\ReSume\\NativeHost\\ReSume.NativeHost.exe",
  "type": "stdio",
  "allowed_origins": [
    "chrome-extension://abcdefghijklmnopabcdefghijklmnop/"
  ]
}
Registry key:

HKCU\Software\Google\Chrome\NativeMessagingHosts\com.resume.nativehost
9.2 Standard Uninstall
Removed:

%ProgramFiles%\ReSume\ (all executables)
Registry: Native Messaging Host key
Registry: Startup Run entry (if present)
Start Menu and Desktop shortcuts
Preserved:

%LocalAppData%\ReSume\sessions\
%LocalAppData%\ReSume\settings\
9.3 Complete Removal
Removed (everything above plus):

%LocalAppData%\ReSume\sessions\
%LocalAppData%\ReSume\settings\
%LocalAppData%\ReSume\logs\
%LocalAppData%\ReSume\cache\
The entire %LocalAppData%\ReSume\ directory
Trigger: Checkbox during uninstall: "Remove all saved sessions and settings"

10. Integration Health & Repair
10.1 Diagnostics Dashboard
Checks performed:

Check	What It Verifies	Pass Condition
Chrome Installation	Chrome is installed and detectable	chrome.exe found via registry
Extension Installation	Extension installed in at least one profile	At least one NativeHost connection active
Native Host Manifest	Manifest JSON exists and is valid	File exists, JSON parses, path correct
Native Host Registry	Registry key exists and points to manifest	Key exists under HKCU\...\NativeMessagingHosts
Native Host Executable	Executable exists at registered path	File exists and is accessible
Startup Entry	ReSume registered for Windows startup	Registry Run key exists (if opted in)
Session Storage	Session directory exists and is writable	Directory exists, write test succeeds
Profile Mappings	Chrome profiles are discoverable	At least one profile directory found
Pipe Server	Named Pipe server is running	Server is accepting connections
Display example:

✅ Chrome Installation     Detected: Chrome 121.0.6167.85
✅ Native Host Registry    Key present, path valid
✅ Native Host Executable  Found at expected path
✅ Native Host Manifest    Valid JSON, correct extension ID
✅ Extension (Personal)    Connected
✅ Extension (Work)        Connected
❌ Extension (School)      Not connected
✅ Startup Entry           Enabled
✅ Session Storage         Writable, 12 sessions stored
10.2 Fix Integration
One-click repair actions:

Action	What It Does
Repair Registry	Rewrites Native Messaging Host registry key
Repair Native Host	Verifies executable exists; copies from installation if missing
Repair Startup	Re-creates or removes Windows Startup registry entry
Repair Manifest	Regenerates manifest JSON with correct path and extension ID
Extension Guidance	Opens Chrome Web Store page or manual installation guide
Repair Storage	Recreates session directory if missing
Repair All	Runs all repair actions in sequence
Repair is never destructive. It only adds or corrects entries.
It never deletes user data.

11. Logging & Diagnostics
11.1 Log Categories
Log File	Contents
resume-{date}.log	Main app events: saves, restores, errors, UI actions
nativehost-{date}.log	NativeHost communication: connections, messages
diagnostics-{date}.log	Diagnostic check results and repair actions
11.2 Log Location

%LocalAppData%\ReSume\logs\
├── resume-2025-01-17.log
├── resume-2025-01-16.log
├── nativehost-2025-01-17.log
└── diagnostics-2025-01-17.log
11.3 Log Format

[2025-01-17 16:30:00.123] [INFO]  [SessionManager] Session saved: "Friday afternoon"
[2025-01-17 16:30:00.456] [WARN]  [ProfileManager] Chrome profile "School" not responding
[2025-01-17 16:30:01.789] [ERROR] [WindowEnumerator] Failed to get command line: Access denied
11.4 Log Retention
Default: 30 days (configurable)
Maximum total log size: 100 MB (configurable)
Automatic cleanup on application startup
11.5 Diagnostic Export
Exports all logs as a single ZIP file containing:

All log files within retention period
Current diagnostics check results
System information (OS version, .NET version, Chrome version)
Session file listing (names and sizes only, not content)
Current settings (sensitive paths redacted)
12. UI / UX
12.1 Main Window Layout

┌─────────────────────────────────────────────────────────┐
│  ReSume                                    ─  □  ✕     │
├──────────────────┬──────────────────────────────────────┤
│  Sessions        │  Session: Friday afternoon           │
│                  │  Saved: Jan 17, 2025 4:30 PM         │
│  ┌────────────┐  │  Source: Manual                      │
│  │ Fri 4:30PM │  │                                      │
│  │ 8 apps     │  │  Applications (8)                    │
│  │ 24 tabs    │  │  ┌──────────────────────────────┐   │
│  ├────────────┤  │  │ ☑ Notepad++ — notes.txt      │   │
│  │ Fri 12:00  │  │  │ ☑ VS Code — project-folder   │   │
│  │ Auto-save  │  │  │ ☑ File Explorer — Documents  │   │
│  │ 6 apps     │  │  │ ☑ Word — report.docx         │   │
│  ├────────────┤  │  │ ☑ Excel — budget.xlsx        │   │
│  │ Thu 9:00AM │  │  │ ☐ Calculator                 │   │
│  │ 5 apps     │  │  └──────────────────────────────┘   │
│  └────────────┘  │                                      │
│                  │  Browser Tabs (24)                   │
│  [Save Session]  │  ┌──────────────────────────────┐   │
│  [Save &         │  │ Personal (2 windows, 8 tabs)  │   │
│   Shutdown]      │  │ Work (1 window, 12 tabs)      │   │
│                  │  │ School ⚠ Not connected        │   │
│                  │  └──────────────────────────────┘   │
│                  │                                      │
│                  │  [Restore Selected]  [Restore All]   │
├──────────────────┴──────────────────────────────────────┤
│  Integration: ✅ Chrome  ✅ Native Host  ⚠ 2/3 profiles │
└─────────────────────────────────────────────────────────┘
12.2 First-Run Wizard
Step 1: Welcome

Brief explanation of what ReSume does
Privacy assurance (all data stays local)
Step 2: Startup Configuration

"Start ReSume when Windows starts?" toggle (default: enabled)
Step 3: Chrome Extension Setup

Detects if Chrome is installed
Provides Chrome Web Store link and instructions
"Test Connection" button
Step 4: Native Host Verification

Automatically verifies Native Host registration
Shows green check if working, offers Repair button if not
Step 5: Ready

"Save your first session" prompt
"Take a tour" option
12.3 Tray Menu

┌───────────────────────┐
│ 💾 Save Current Session│
│ 🔄 Restore Last Session│
│ ───────────────────── │
│ ⏻  Save & Shutdown     │
│ 🔄 Save & Restart      │
│ ───────────────────── │
│ 📂 Open ReSume         │
│ ⚙  Settings            │
│ ───────────────────── │
│ ✕  Exit                │
└───────────────────────┘
12.4 Integration Status Page

┌─────────────────────────────────────────────┐
│  Integration Status                          │
│                                              │
│  ✅ Chrome Installation    v121.0.6167.85    │
│  ✅ Native Host Registry   Valid             │
│  ✅ Native Host Exe        Found             │
│  ✅ Native Host Manifest   Valid             │
│  ✅ Extension (Personal)   Connected         │
│  ✅ Extension (Work)       Connected         │
│  ❌ Extension (School)     Not responding    │
│  ✅ Startup Entry          Enabled           │
│  ✅ Session Storage        OK (12 sessions)  │
│                                              │
│  [Run Diagnostics]  [Fix All Issues]         │
└─────────────────────────────────────────────┘
13. Repository Structure

ReSume/
├── src/
│   ├── ReSume/                          # Main WPF application
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── Views/
│   │   │   ├── SessionListView.xaml
│   │   │   ├── SessionDetailView.xaml
│   │   │   ├── IntegrationStatusView.xaml
│   │   │   ├── SettingsView.xaml
│   │   │   └── FirstRunWizard.xaml
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── SessionListViewModel.cs
│   │   │   ├── SessionDetailViewModel.cs
│   │   │   └── IntegrationStatusViewModel.cs
│   │   ├── Services/
│   │   │   └── TrayIconService.cs
│   │   ├── Converters/
│   │   └── Resources/
│   │       ├── Styles/
│   │       └── Icons/
│   │
│   ├── ReSume.Core/                     # Core logic (no UI dependency)
│   │   ├── Models/
│   │   │   ├── Session.cs
│   │   │   ├── ApplicationState.cs
│   │   │   ├── BrowserProfile.cs
│   │   │   ├── BrowserWindow.cs
│   │   │   ├── TabInfo.cs
│   │   │   └── MonitorInfo.cs
│   │   ├── Services/
│   │   │   ├── SessionManager.cs
│   │   │   ├── WindowEnumerator.cs
│   │   │   ├── RestoreEngine.cs
│   │   │   ├── PipeServer.cs
│   │   │   ├── ProfileDiscovery.cs
│   │   │   └── DiagnosticsService.cs
│   │   ├── Restorers/
│   │   │   ├── IAppRestorer.cs
│   │   │   ├── GenericRestorer.cs
│   │   │   ├── ExplorerRestorer.cs
│   │   │   ├── NotepadRestorer.cs
│   │   │   ├── VSCodeRestorer.cs
│   │   │   ├── WordRestorer.cs
│   │   │   └── ExcelRestorer.cs
│   │   ├── Interop/
│   │   │   ├── NativeMethods.cs
│   │   │   ├── User32.cs
│   │   │   ├── Kernel32.cs
│   │   │   └── Dwmapi.cs
│   │   └── Configuration/
│   │       └── AppSettings.cs
│   │
│   ├── ReSume.NativeHost/               # Chrome Native Messaging Host
│   │   ├── Program.cs
│   │   ├── MessageHandler.cs
│   │   ├── PipeClient.cs
│   │   └── NativeMessaging.cs
│   │
│   └── ReSume.Installer/               # WiX installer project
│       ├── Product.wxs
│       ├── Components.wxs
│       ├── UI.wxs
│       └── nativehost-manifest.json
│
├── extensions/
│   └── chrome/
│       ├── manifest.json
│       ├── background.js
│       ├── popup.html
│       ├── popup.js
│       ├── popup.css
│       └── icons/
│           ├── icon16.png
│           ├── icon48.png
│           └── icon128.png
│
├── docs/
│   ├── planning.md
│   ├── architecture.md
│   ├── chrome-extension.md
│   ├── native-host-protocol.md
│   └── user-guide.md
│
├── tests/
│   ├── ReSume.Core.Tests/
│   │   ├── SessionManagerTests.cs
│   │   ├── WindowEnumeratorTests.cs
│   │   ├── RestoreEngineTests.cs
│   │   └── PipeServerTests.cs
│   └── ReSume.NativeHost.Tests/
│       ├── MessageHandlerTests.cs
│       └── PipeClientTests.cs
│
├── scripts/
│   ├── build.ps1
│   ├── test.ps1
│   ├── package.ps1
│   └── register-native-host.ps1
│
├── ReSume.sln
├── README.md
├── LICENSE
└── .gitignore
14. Development Roadmap
Phase 0 — Native Foundation
Goal: Establish project structure, build system, and core infrastructure.

Deliverables:

Solution structure with all projects
WPF application shell with main window
Build and test scripts
Basic settings infrastructure
Logging framework
Exit criteria: Application compiles, runs, and shows an empty main window.

Phase 1 — Core Save & Restore
Goal: Save and restore non-browser desktop applications.

Deliverables:

Window enumeration using Win32 APIs
Process discovery and command-line extraction
Session serialization to JSON
Session deserialization and basic restoration
Generic application restorer
Window position and state restoration
Session file storage and listing
Basic session list UI
Exit criteria: User can save a session, close apps, and restore them to
original positions.

Phase 2 — Chrome Integration
Goal: Capture and restore Chrome browser tabs across profiles.

Deliverables:

Chrome extension (Manifest V3) with tab capture
ReSume.NativeHost.exe with Native Messaging
Named Pipe server in ReSume.exe
Named Pipe client in ReSume.NativeHost.exe
Chrome profile discovery
Multi-profile connection management
Tab and window restoration via extension
Browser data integrated into session JSON
Exit criteria: User can save Chrome tabs across multiple profiles,
close Chrome, restore, and see all tabs restored correctly.

Phase 3 — Document-Aware Restoration
Goal: Restore applications with their specific documents open.

Deliverables:

IAppRestorer interface and plugin architecture
Explorer restorer (folder paths via COM)
Notepad / Notepad++ restorer
VS Code restorer (workspaces and folders)
Word restorer
Excel restorer
PDF restorer
Generic fallback restorer
Exit criteria: Restoring a session opens each application with its
correct document.

Phase 4 — UI, Tray & History Polish
Goal: Complete the user interface and session management features.

Deliverables:

Session detail view with app and tab listings
Selective restoration (checkbox per app/profile)
Session rename, delete, duplicate
History browser with search and filter
System tray integration with full context menu
Tray icon states (idle, saving, error)
Save & Shutdown / Save & Restart commands
First-run wizard
Settings page
Exit criteria: Application is usable as a daily driver with polished UI.

Phase 5 — Diagnostics & Repair
Goal: Help users identify and fix integration issues.

Deliverables:

Diagnostics dashboard with all checks
One-click repair for each integration point
"Fix All" batch repair
Diagnostic export (ZIP)
Basic log viewer
Status bar integration health indicator
Exit criteria: User can diagnose and fix common issues without technical
knowledge.

Phase 6 — Additional Browser Support
Goal: Extend browser support beyond Chrome.

Deliverables:

Microsoft Edge extension (Manifest V3)
Edge Native Messaging Host registration
Edge profile discovery
Unified browser management in UI
Exit criteria: Edge tabs are captured and restored alongside Chrome tabs.

15. Risks & Mitigations
Risk	Impact	Likelihood	Mitigation
Missing Chrome Extension	High	Medium	Graceful degradation: save/restore apps without tabs
Broken Native Host	High	Medium	Diagnostics detect this. One-click repair available
Missing Chrome Profiles	Medium	Low	Notify user, offer to redirect to another profile
Off-screen windows	Medium	Medium	Validate positions, snap to nearest monitor edge
UWP app limitations	High	High	Use shell:AppsFolder launch URIs, document limitations
WMI failures	Medium	Medium	Fall back to NtQueryInformationProcess, then process name only
Chrome API rate limits	Low	Low	Sequential tab creation with small delays
Antivirus interference	Medium	Low	Sign executables with code signing certificate
Permission elevation	Medium	Medium	Detect elevated processes, notify user
Multi-monitor changes	Medium	Medium	Store full monitor config, implement remapping algorithm
16. Future Enhancements
Version 2 — Microsoft Edge Support
Manifest V3 extension for Edge
Edge Native Messaging Host registration
Edge profile discovery (%LocalAppData%\Microsoft\Edge\User Data\)
Unified browser section in UI
Version 3 — Firefox Support
WebExtension with nativeMessaging permission
Firefox-specific Native Messaging Host manifest
Firefox profile handling (different profile system)
Tab and window API adaptation
Future Possibilities
Enhancement	Description
Plugin ecosystem	Third-party IAppRestorer implementations via plugin directory
Export/import sessions	Portable session packages for sharing between machines
Optional cloud sync	Opt-in sync via OneDrive/Dropbox (never mandatory)
Scheduled saves	Automatic session capture at configurable intervals
Session templates	Reusable session layouts (e.g., "morning work setup")
Monitor layout profiles	Associate sessions with specific monitor configurations
Keyboard shortcuts	Global hotkeys for save, restore, and quick actions
CLI interface	resume.exe --save, resume.exe --restore latest
Virtual desktop support	Capture Windows Virtual Desktop assignments
Session diff	Compare two sessions to see what changed
17. Vision
ReSume aims to become a production-ready Windows workspace restoration engine
capable of reconstructing an entire desktop environment — including
applications, documents, browser tabs, and layouts — with minimal user
intervention after shutdowns, restarts, updates, crashes, or power failures.

Design Principles
Reliability over features.
A session that restores 90% correctly every time is better than one that
promises 100% and fails unpredictably.

Transparency over magic.
The user should always understand what ReSume captured, what it will
restore, and what it could not handle. No silent failures.

Privacy by architecture.
All data is local. No analytics, no telemetry, no network calls. Privacy
is not a setting — it is a structural guarantee.

Graceful degradation.
Every optional component can fail without taking down core functionality.
The application always does the best it can with what is available.

Repairability.
When something breaks, the user can diagnose and fix it from within the
application. No registry editing, no manual file manipulation.

This document is the single source of truth for all ReSume development
decisions. Each phase of development should reference this document for
requirements, data models, and architectural guidance.

Document version: 1.0 | Last updated: 2025-01-17
