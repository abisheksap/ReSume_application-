using System.Windows;
using ReSume.Services;

namespace ReSume;

public partial class App : System.Windows.Application
{
    private TrayIconService? _trayService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _trayService = new TrayIconService();
        _trayService.Initialize();
    }
}