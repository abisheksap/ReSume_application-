namespace ReSume.Core.Models;

public sealed class MonitorInfo {
    public string DeviceName { get; set; } = string.Empty;
    public WindowPosition? Bounds { get; set; }
    public bool IsPrimary { get; set; }
    public double ScaleFactor { get; set; } = 1.0;
}