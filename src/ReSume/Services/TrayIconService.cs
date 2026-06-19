using System.Windows.Forms;
using System.Drawing;

namespace ReSume.Services;

public class TrayIconService
{
    private NotifyIcon? _notifyIcon;

    public void Initialize()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "ReSume",
            Visible = true
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Save Session", null, (s, e) => { });
        menu.Items.Add("Restore Last Session", null, (s, e) => { });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Save && Shutdown", null, (s, e) => { });
        menu.Items.Add("Save && Restart", null, (s, e) => { });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open ReSume", null, (s, e) => { });
        menu.Items.Add("Exit", null, (s, e) => System.Windows.Application.Current.Shutdown());
        _notifyIcon.ContextMenuStrip = menu;
    }
}