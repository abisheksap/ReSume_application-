using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using ReSume.Core.Interop;
using ReSume.Core.Models;
using ReSume.Core.Restorers;

namespace ReSume.Core.Services;

public class RestoreEngine
{
    private readonly List<IAppRestorer> _restorers;

    public RestoreEngine(IEnumerable<IAppRestorer> restorers)
    {
        _restorers = restorers.ToList();
    }

    public async Task RestoreSessionAsync(Session session)
    {
        var currentMonitors = MonitorManager.GetCurrentMonitorConfiguration();

        foreach (var app in session.Applications)
        {
            Process? proc = null;
            var restorer = _restorers.FirstOrDefault(r => r.CanRestore(app));
            if (restorer != null)
            {
                var result = await restorer.RestoreAsync(app);
                if (result.Success && result.Process != null)
                    proc = result.Process;
                else continue;
            }
            else
            {
                try
                {
                    if (!string.IsNullOrEmpty(app.CommandLine))
                    {
                        proc = Process.Start(new ProcessStartInfo
                        {
                            FileName = app.ExecutablePath ?? app.ProcessName,
                            Arguments = ExtractArguments(app.CommandLine, app.ExecutablePath),
                            UseShellExecute = true
                        });
                    }
                    else if (!string.IsNullOrEmpty(app.ExecutablePath))
                    {
                        proc = Process.Start(new ProcessStartInfo(app.ExecutablePath) { UseShellExecute = true });
                    }
                }
                catch { }
            }

            if (proc != null)
            {
                IntPtr hWnd = IntPtr.Zero;
                for (int i = 0; i < 50; i++)
                {
                    hWnd = FindWindowByProcessId(proc.Id);
                    if (hWnd != IntPtr.Zero) break;
                    await Task.Delay(100);
                }

                if (hWnd != IntPtr.Zero && app.Windows.Count > 0)
                {
                    var win = app.Windows[0];
                    MonitorManager.RemapWindowPosition(win, currentMonitors);

                    if (win.WindowState == "Maximized")
                    {
                        User32.SetWindowPlacement(hWnd, ref new User32.WINDOWPLACEMENT { length = Marshal.SizeOf<User32.WINDOWPLACEMENT>(), showCmd = 3 });
                    }
                    else if (win.WindowState == "Minimized")
                    {
                        User32.SetWindowPlacement(hWnd, ref new User32.WINDOWPLACEMENT { length = Marshal.SizeOf<User32.WINDOWPLACEMENT>(), showCmd = 2 });
                    }
                    else
                    {
                        User32.SetWindowPos(hWnd, IntPtr.Zero,
                            win.Position.X, win.Position.Y,
                            win.Position.Width, win.Position.Height,
                            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Restores browser windows by sending restore commands to the respective profile's native host.
    /// </summary>
    public async Task RestoreBrowserProfilesAsync(List<BrowserProfile> browserProfiles, ProfileConnectionManager profileManager)
    {
        foreach (var bp in browserProfiles)
        {
            var restoreMsg = JsonSerializer.Serialize(new
            {
                action = "restore",
                data = bp.Windows.Select(w => new
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
            await profileManager.SendToProfileAsync(bp.ProfileId, restoreMsg);
        }
    }

    // … (other helper methods remain unchanged) …
    private IntPtr FindWindowByProcessId(int processId) { /* unchanged */ }
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private static string ExtractArguments(string? commandLine, string? exePath) { /* unchanged */ }
}