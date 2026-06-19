using System.Collections.Generic;
using System;

namespace ReSume.Core.Models;

public sealed class Session {
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Version { get; set; } = "1.0";
    public string Source { get; set; } = "manual";
    public SessionMetadata Metadata { get; set; } = new();
    public List<ApplicationState> Applications { get; set; } = new();
    public List<BrowserProfile> BrowserProfiles { get; set; } = new();
}

public sealed class SessionMetadata {
    public string MachineName { get; set; } = Environment.MachineName;
    public string Username { get; set; } = Environment.UserName;
    public string OsVersion { get; set; } = Environment.OSVersion.VersionString;
    public string ResumeVersion { get; set; } = "1.0.0";
    public List<MonitorInfo> MonitorConfiguration { get; set; } = new();
}