using System.Collections.Generic;

namespace ReSume.Core.Models;

public sealed class ApplicationState {
    public string ProcessName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? CommandLine { get; set; }
    public List<ApplicationWindow> Windows { get; set; } = new();
    public List<string> DocumentPaths { get; set; } = new();
    public string RestorationHint { get; set; } = "command_line";
}

public sealed class ApplicationWindow {
    public string? Title { get; set; }
    public WindowPosition Position { get; set; } = new();
    public string? Monitor { get; set; }
    public string WindowState { get; set; } = "Normal";
    public bool IsTopmost { get; set; }
    public int ZOrder { get; set; }
}

public sealed class WindowPosition {
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}