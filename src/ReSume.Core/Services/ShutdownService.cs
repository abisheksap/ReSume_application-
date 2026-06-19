using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ReSume.Core.Models;

namespace ReSume.Core.Services;

public class ShutdownService {
    private readonly SessionManager _sessionManager;
    private readonly WindowEnumerator _windowEnumerator;
    private readonly ProfileConnectionManager _profileManager;
    private readonly int _timeoutSeconds;

    public ShutdownService(SessionManager sessionManager, WindowEnumerator windowEnumerator, ProfileConnectionManager profileManager, int timeoutSeconds = 30) {
        _sessionManager = sessionManager;
        _windowEnumerator = windowEnumerator;
        _profileManager = profileManager;
        _timeoutSeconds = timeoutSeconds;
    }

    public async Task SaveAndShutdownAsync(bool restart = false) {
        var session = new Session {
            Label = "Auto-save before " + (restart ? "restart" : "shutdown"),
            Source = "auto",
            Applications = _windowEnumerator.EnumerateWindows()
        };
        await _sessionManager.SaveSessionAsync(session);
        string args = restart ? "/r /t 0" : "/s /t 0";
        Process.Start("shutdown", args);
    }
}