using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ReSume.Core.Interop;
using ReSume.Core.Models;
using ReSume.Core.Restorers;
using System.Threading;
using System.Runtime.InteropServices;

namespace ReSume.Core.Services;

public class RestoreEngine {
    private readonly List<IAppRestorer> _restorers;

    public RestoreEngine(IEnumerable<IAppRestorer> restorers) {
        _restorers = restorers.ToList();
    }

    public async Task RestoreSessionAsync(Session session) {
        var currentMonitors = MonitorManager.GetCurrentMonitorConfiguration();

        foreach (var app in session.Applications) {
            Process? proc = null;
            IAppRestorer? restorer = _restorers.FirstOrDefault(r => r.CanRestore(app));
            if (restorer != null) {
                var result = await restorer.RestoreAsync(app);
                if (result.Success && result.Process != null)
                    proc = result.Process;
                else continue;
            } else {
                // Fallback
                try {
                    if (!string.IsNullOrEmpty(app.CommandLine)) {
                        var psi = new ProcessStartInfo {
                            FileName = app.ExecutablePath ?? app.ProcessName,
                            Arguments = ExtractArguments(app.CommandLine, app.ExecutablePath),
                            UseShellExecute = true
                        };
                        proc = Process.Start(psi);
                    } else if (!string.IsNullOrEmpty(app.ExecutablePath)) {
                        proc = Process.Start(new ProcessStartInfo(app.ExecutablePath) { UseShellExecute = true });
                    }
                } catch { }
            }

            if (proc != null) {
                IntPtr hWnd = IntPtr.Zero;
                for (int i = 0; i < 50; i++) {
                    hWnd = FindWindowByProcessId(proc.Id);
                    if (hWnd != IntPtr.Zero) break;
                    await Task.Delay(100);
                }
                if (hWnd != IntPtr.Zero && app.Windows.Count > 0) {
                    var win = app.Windows[0];
                    MonitorManager.RemapWindowPosition(win, currentMonitors);

                    if (win.WindowState == "Maximized") {
                        var placement = new User32.WINDOWPLACEMENT { length = Marshal.SizeOf<User32.WINDOWPLACEMENT>(), showCmd = 3 };
                        User32.SetWindowPlacement(hWnd, ref placement);
                    } else if (win.WindowState == "Minimized") {
                        var placement = new User32.WINDOWPLACEMENT { length = Marshal.SizeOf<User32.WINDOWPLACEMENT>(), showCmd = 2 };
                        User32.SetWindowPlacement(hWnd, ref placement);
                    } else {
                        User32.SetWindowPos(hWnd, IntPtr.Zero, win.Position.X, win.Position.Y,
                            win.Position.Width, win.Position.Height, User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
                    }
                }
            }
        }
    }
    public async Task RestoreBrowserProfilesAsync(List<BrowserProfile> browserProfiles)
{
    foreach (var profile in browserProfiles)
    {
        // We need to send the restore command to the specific profile's extension
        var restoreMessage = JsonSerializer.Serialize(new
        {
            action = "restore",
            data = profile.Windows.Select(w => new
            {
                focused = true,
                state = w.WindowState,
                left = w.Position.X,
                top = w.Position.Y,
                width = w.Position.Width,
                height = w.Position.Height,
                incognito = w.IsIncognito,
                tabs = w.Tabs.Select(t => new { url = t.Url })
            })
        });
        // Send via pipe server – we need a reference to ProfileConnectionManager in RestoreEngine
        // So we'll pass it in the constructor or keep a static reference for now.
    }
}

    private IntPtr FindWindowByProcessId(int processId) {
        IntPtr hWnd = IntPtr.Zero;
        EnumWindows(delegate (IntPtr wnd, IntPtr param) {
            uint pid;
            User32.GetWindowThreadProcessId(wnd, out pid);
            if (pid == processId && User32.IsWindowVisible(wnd)) {
                hWnd = wnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return hWnd;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private static string ExtractArguments(string? commandLine, string? exePath) {
        if (string.IsNullOrEmpty(commandLine) || string.IsNullOrEmpty(exePath)) return string.Empty;
        string cmd = commandLine.Trim();
        if (cmd.StartsWith($"\"{exePath}\"", StringComparison.OrdinalIgnoreCase))
            return cmd.Substring(exePath.Length + 2).Trim();
        if (cmd.StartsWith(exePath, StringComparison.OrdinalIgnoreCase))
            return cmd.Substring(exePath.Length).Trim();
        return cmd;
    }
}