using System;
using System.Runtime.InteropServices;

namespace ReSume.Core.Interop;

internal static class Shcore {
    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    public const int MDT_EFFECTIVE_DPI = 0;
}