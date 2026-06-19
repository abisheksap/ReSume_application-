using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ReSume.Core.Services;

public class DiagnosticsService
{
    public async Task<List<DiagnosticCheck>> RunAllChecksAsync()
    {
        var checks = new List<DiagnosticCheck>();

        // Chrome installed?
        bool chrome = false;
        try { using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"); chrome = k != null; } catch { }
        checks.Add(new DiagnosticCheck { Name = "Chrome Installation", Passed = chrome, Details = chrome ? "Found" : "Not found", CanRepair = false });

        // Edge installed?
        bool edge = false;
        try { using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"); edge = k != null; } catch { }
        checks.Add(new DiagnosticCheck { Name = "Edge Installation", Passed = edge, Details = edge ? "Found" : "Not found", CanRepair = false });

        // Chrome Native Host
        bool chromeNH = false;
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.resume.nativehost"))
        {
            if (k != null) { string? p = k.GetValue("") as string; chromeNH = File.Exists(p); }
        }
        checks.Add(new DiagnosticCheck
        {
            Name = "Chrome Native Host",
            Passed = chromeNH,
            Details = chromeNH ? "Registered" : "Not registered",
            CanRepair = true,
            RepairAction = () => RepairChromeNativeHost()
        });

        // Edge Native Host
        bool edgeNH = false;
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Edge\NativeMessagingHosts\com.resume.nativehost"))
        {
            if (k != null) { string? p = k.GetValue("") as string; edgeNH = File.Exists(p); }
        }
        checks.Add(new DiagnosticCheck
        {
            Name = "Edge Native Host",
            Passed = edgeNH,
            Details = edgeNH ? "Registered" : "Not registered",
            CanRepair = true,
            RepairAction = () => RepairEdgeNativeHost()
        });

        // Startup entry
        bool startup = false;
        using (var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run")) { startup = k?.GetValue("ReSume") != null; }
        checks.Add(new DiagnosticCheck
        {
            Name = "Startup Entry",
            Passed = startup,
            Details = startup ? "Enabled" : "Not enabled",
            CanRepair = true,
            RepairAction = () => RepairStartup()
        });

        // Session storage
        string sessionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReSume", "sessions");
        bool storage = false;
        try { Directory.CreateDirectory(sessionsDir); var tf = Path.Combine(sessionsDir, ".test"); File.WriteAllText(tf, ""); File.Delete(tf); storage = true; } catch { }
        checks.Add(new DiagnosticCheck
        {
            Name = "Session Storage",
            Passed = storage,
            Details = storage ? "Writable" : "Error",
            CanRepair = true,
            RepairAction = () => { try { Directory.CreateDirectory(sessionsDir); } catch (Exception ex) { throw new Exception("Could not create session folder: " + ex.Message); } }
        });

        return checks;
    }

    public void RepairChromeNativeHost()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ReSume");
            if (!Directory.Exists(dir))
                throw new Exception("ReSume installation not found. Run install.ps1 as Administrator first.");

            string manifest = Path.Combine(dir, "nativehost-manifest.json");
            if (!File.Exists(manifest))
            {
                string content = @"{""name"":""com.resume.nativehost"",""path"":""" +
                    Path.Combine(dir, "NativeHost", "ReSume.NativeHost.exe") +
                    @""",""type"":""stdio"",""allowed_origins"":[""chrome-extension://YOUR_CHROME_EXTENSION_ID/""]}";
                File.WriteAllText(manifest, content);
            }
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.resume.nativehost");
            key.SetValue("", manifest);
        }
        catch (Exception ex)
        {
            throw new Exception("Chrome Native Host repair failed: " + ex.Message);
        }
    }

    public void RepairEdgeNativeHost()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ReSume");
            if (!Directory.Exists(dir))
                throw new Exception("ReSume installation not found. Run install.ps1 as Administrator first.");

            string manifest = Path.Combine(dir, "nativehost-manifest-edge.json");
            if (!File.Exists(manifest))
            {
                string content = @"{""name"":""com.resume.nativehost"",""path"":""" +
                    Path.Combine(dir, "NativeHost", "ReSume.NativeHost.exe") +
                    @""",""type"":""stdio"",""allowed_origins"":[""extension://YOUR_EDGE_EXTENSION_ID/""]}";
                File.WriteAllText(manifest, content);
            }
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Edge\NativeMessagingHosts\com.resume.nativehost");
            key.SetValue("", manifest);
        }
        catch (Exception ex)
        {
            throw new Exception("Edge Native Host repair failed: " + ex.Message);
        }
    }

    public void RepairStartup()
    {
        try
        {
            string exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ReSume", "ReSume.exe");
            if (!File.Exists(exePath))
                throw new Exception("ReSume.exe not found. Install the application first.");
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            key.SetValue("ReSume", exePath);
        }
        catch (Exception ex)
        {
            throw new Exception("Startup repair failed: " + ex.Message);
        }
    }
}

public sealed class DiagnosticCheck
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Details { get; set; } = string.Empty;
    public bool CanRepair { get; set; }
    public Action? RepairAction { get; set; }
}