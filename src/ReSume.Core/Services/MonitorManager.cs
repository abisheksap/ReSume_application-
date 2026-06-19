using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ReSume.Core.Interop;
using ReSume.Core.Models;

namespace ReSume.Core.Services;

public class MonitorManager {
    public static List<MonitorInfo> GetCurrentMonitorConfiguration() {
        var monitors = new List<MonitorInfo>();
        var dev = new DISPLAY_DEVICE();
        dev.cb = Marshal.SizeOf(dev);
        for (uint i = 0; EnumDisplayDevices(null, i, ref dev, 0); i++) {
            if (dev.StateFlags.HasFlag(DisplayDeviceStateFlags.AttachedToDesktop) &&
                !dev.StateFlags.HasFlag(DisplayDeviceStateFlags.MirroringDriver)) {
                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(dm);
                if (EnumDisplaySettings(dev.DeviceName, ENUM_CURRENT_SETTINGS, ref dm)) {
                    IntPtr hMonitor = MonitorFromPoint(new POINT { x = dm.dmPositionX, y = dm.dmPositionY }, MONITOR_DEFAULTTONEAREST);
                    uint dpiX = 96, dpiY = 96;
                    User32.GetDpiForMonitor(hMonitor, User32.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                    monitors.Add(new MonitorInfo {
                        DeviceName = dev.DeviceName,
                        Bounds = new WindowPosition { X = dm.dmPositionX, Y = dm.dmPositionY, Width = dm.dmPelsWidth, Height = dm.dmPelsHeight },
                        IsPrimary = (dm.dmPositionX == 0 && dm.dmPositionY == 0),
                        ScaleFactor = dpiX / 96.0
                    });
                }
            }
            dev.cb = Marshal.SizeOf(dev);
        }
        return monitors;
    }

    public static void RemapWindowPosition(ApplicationWindow window, List<MonitorInfo> currentMonitors) {
        if (currentMonitors.Count == 0) return;
        var target = window.Position;
        MonitorInfo closest = currentMonitors[0];
        double minDist = double.MaxValue;
        foreach (var mon in currentMonitors) {
            if (mon.Bounds == null) continue;
            double dist = Math.Sqrt(Math.Pow(target.X - mon.Bounds.X, 2) + Math.Pow(target.Y - mon.Bounds.Y, 2));
            if (dist < minDist) {
                minDist = dist;
                closest = mon;
            }
        }
        if (closest.Bounds != null) {
            int newX = Math.Clamp(target.X, closest.Bounds.X, closest.Bounds.X + closest.Bounds.Width - target.Width);
            int newY = Math.Clamp(target.Y, closest.Bounds.Y, closest.Bounds.Y + closest.Bounds.Height - target.Height);
            window.Position.X = newX;
            window.Position.Y = newY;
        }
    }

    #region Native methods
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DISPLAY_DEVICE {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [Flags]
    public enum DisplayDeviceStateFlags {
        AttachedToDesktop = 0x1, MultiDriver = 0x2, PrimaryDevice = 0x4, MirroringDriver = 0x8,
        VGACompatible = 0x10, Removable = 0x20, ModesPruned = 0x8000000, Remote = 0x4000000, Disconnect = 0x2000000
    }
    [DllImport("user32.dll")]
    public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);
    [DllImport("user32.dll")]
    public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);
    public const int ENUM_CURRENT_SETTINGS = -1;
    [StructLayout(LayoutKind.Sequential)]
    public struct DEVMODE {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }
    #endregion
}