using System.Collections.Generic;
using System;

namespace ReSume.Core.Models;

public sealed class BrowserProfile {
    public string ProfileName { get; set; } = string.Empty;
    public string ProfileDirectory { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public List<BrowserWindow> Windows { get; set; } = new();
    public bool IsConnected { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
}

public sealed class BrowserWindow {
    public int WindowId { get; set; }
    public WindowPosition Position { get; set; } = new();
    public string? Monitor { get; set; }
    public string WindowState { get; set; } = "Normal";
    public bool IsIncognito { get; set; }
    public int ActiveTabIndex { get; set; }
    public List<TabInfo> Tabs { get; set; } = new();
}

public sealed class TabInfo {
    public int Index { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsMuted { get; set; }
    public int GroupId { get; set; } = -1;
    public string? GroupTitle { get; set; }
    public string? GroupColor { get; set; }
}