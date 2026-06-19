using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ReSume.Core.Models;

namespace ReSume.Core.Services;

public class SessionManager {
    private readonly string _sessionsDir;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SessionManager(string basePath) {
        _sessionsDir = Path.Combine(basePath, "sessions");
        Directory.CreateDirectory(_sessionsDir);
    }

    public async Task SaveSessionAsync(Session session) {
        string fileName = $"{session.CreatedAt:yyyy-MM-dd_HHmmss}_{SanitizeFileName(session.Label)}.json";
        string path = Path.Combine(_sessionsDir, fileName);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(session, _jsonOptions));
    }

    public async Task<Session?> LoadSessionAsync(Guid sessionId) {
        foreach (string file in Directory.GetFiles(_sessionsDir, "*.json")) {
            Session? s = JsonSerializer.Deserialize<Session>(await File.ReadAllTextAsync(file));
            if (s?.SessionId == sessionId) return s;
        }
        return null;
    }

    public IEnumerable<Session> ListSessions() {
        foreach (string file in Directory.GetFiles(_sessionsDir, "*.json")) {
            Session? s = JsonSerializer.Deserialize<Session>(File.ReadAllText(file));
            if (s != null) yield return s;
        }
    }

    public void DeleteSession(Guid sessionId) {
        foreach (string file in Directory.GetFiles(_sessionsDir, "*.json")) {
            Session? s = JsonSerializer.Deserialize<Session>(File.ReadAllText(file));
            if (s?.SessionId == sessionId) {
                File.Delete(file);
                return;
            }
        }
    }

    private static string SanitizeFileName(string name) {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "session" : name;
    }
}