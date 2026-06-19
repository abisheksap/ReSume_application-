using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using ReSume.Core.Interop;
using ReSume.Core.Models;

namespace ReSume.Core.Services;

public class WindowEnumerator
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private readonly Dictionary<IntPtr, ApplicationState> _apps = new();
    private readonly List<string> _excludedClasses = new() { "Windows.UI.Core.CoreWindow", "ApplicationFrameWindow", "Progman", "Shell_TrayWnd" };

    public List<ApplicationState> EnumerateWindows()
    {
        _apps.Clear();
        EnumWindows(EnumWindowCallback, IntPtr.Zero);
        return new List<ApplicationState>(_apps.Values);
    }

    private bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
    {
        if (!User32.IsWindowVisible(hWnd)) return true;
        if (User32.IsWindowCloaked(hWnd)) return true;

        int length = User32.GetWindowTextLength(hWnd);
        string title = length > 0 ? User32.GetWindowText(hWnd) : string.Empty;
        User32.GetWindowThreadProcessId(hWnd, out uint processId);
        string className = User32.GetClassName(hWnd);

        if (_excludedClasses.Contains(className)) return true;
        if (string.IsNullOrEmpty(title) && className != "CabinetWClass") return true;

        Process? proc = null;
        try { proc = Process.GetProcessById((int)processId); } catch { return true; }
        if (proc.ProcessName.Equals("ReSume", StringComparison.OrdinalIgnoreCase)) return true;

        if (!_apps.TryGetValue(hWnd, out ApplicationState? app))
        {
            string cmdLine = GetCommandLine(proc);
            string? execPath = null;
            try { execPath = proc.MainModule?.FileName; } catch { }

            // Extract document paths from command line
            var docPaths = ExtractDocumentPaths(proc.ProcessName, cmdLine, title);

            app = new ApplicationState
            {
                ProcessName = proc.ProcessName,
                ExecutablePath = execPath,
                CommandLine = cmdLine,
                DocumentPaths = docPaths
            };
            _apps[hWnd] = app;
        }

        User32.RECT rect;
        if (User32.GetWindowRect(hWnd, out rect))
        {
            string windowState = "Normal";
            if (User32.IsIconic(hWnd)) windowState = "Minimized";
            else if (User32.IsZoomed(hWnd)) windowState = "Maximized";

            app.Windows.Add(new ApplicationWindow
            {
                Title = title,
                Position = new WindowPosition { X = rect.Left, Y = rect.Top, Width = rect.Right - rect.Left, Height = rect.Bottom - rect.Top },
                Monitor = null,
                WindowState = windowState,
                IsTopmost = false,
                ZOrder = 0
            });
        }
        return true;
    }

    private static string GetCommandLine(Process proc)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {proc.Id}");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["CommandLine"]?.ToString() ?? string.Empty;
            }
        }
        catch { }
        return string.Empty;
    }

    private static List<string> ExtractDocumentPaths(string processName, string commandLine, string windowTitle)
    {
        var paths = new List<string>();
        if (string.IsNullOrEmpty(commandLine)) return paths;

        // VS Code: look for --folder-uri or --file-uri, or a bare path
        if (processName.StartsWith("code", StringComparison.OrdinalIgnoreCase))
        {
            // Extract path after last argument that is a file/folder
            var parts = commandLine.Split(' ');
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i].Trim('"');
                if (part.Contains("://")) continue; // skip URIs? We could handle file://
                if (Directory.Exists(part) || File.Exists(part))
                {
                    paths.Add(part);
                    break;
                }
            }
            if (paths.Count == 0 && !string.IsNullOrEmpty(windowTitle))
            {
                // Title often contains the folder name, but we can't get full path
                paths.Add(windowTitle.Replace(" - Visual Studio Code", ""));
            }
        }
        // File Explorer: title usually shows the folder name, but command line may have /root, or just the folder
        else if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            // Explorer command line often has /root,"C:\path" or just the path as last argument
            var match = System.Text.RegularExpressions.Regex.Match(commandLine, @"/root,\s*""([^""]+)""");
            if (match.Success)
                paths.Add(match.Groups[1].Value);
            else
            {
                // Fallback: use last argument that looks like a path
                var parts = commandLine.Split(' ');
                for (int i = parts.Length - 1; i >= 1; i--)
                {
                    string part = parts[i].Trim('"');
                    if (Directory.Exists(part))
                    {
                        paths.Add(part);
                        break;
                    }
                }
            }
            if (paths.Count == 0 && !string.IsNullOrEmpty(windowTitle))
            {
                // If no path found, try the title (which is usually the folder name, not full path)
                paths.Add(windowTitle);
            }
        }
        // Generic: check if last argument is a file path
        else
        {
            var parts = commandLine.Split(' ');
            for (int i = parts.Length - 1; i >= 1; i--)
            {
                string part = parts[i].Trim('"');
                if (File.Exists(part))
                {
                    paths.Add(part);
                    break;
                }
            }
        }

        return paths;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
}