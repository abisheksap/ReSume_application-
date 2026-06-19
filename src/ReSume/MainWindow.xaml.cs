using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ReSume.Core.Models;
using ReSume.Core.Services;
using ReSume.Views;
using System.IO;
using System.Text.Json;

namespace ReSume;

public partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;
    private readonly WindowEnumerator _windowEnumerator;
    private readonly RestoreEngine _restoreEngine;
    private readonly ShutdownService _shutdownService;
    private readonly ProfileConnectionManager _profileManager;
    private ObservableCollection<Session> _sessions = new();

    public MainWindow()
    {
        InitializeComponent();
        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReSume");
        _sessionManager = new SessionManager(basePath);
        _windowEnumerator = new WindowEnumerator();
        var restorers = new ReSume.Core.Restorers.IAppRestorer[] {
            new ReSume.Core.Restorers.ExplorerRestorer(),
            new ReSume.Core.Restorers.NotepadRestorer(),
            new ReSume.Core.Restorers.NotepadppRestorer(),
            new ReSume.Core.Restorers.VSCodeRestorer(),
            new ReSume.Core.Restorers.WordRestorer(),
            new ReSume.Core.Restorers.ExcelRestorer(),
            new ReSume.Core.Restorers.PowerPointRestorer(),
            new ReSume.Core.Restorers.AcrobatRestorer(),
            new ReSume.Core.Restorers.GenericRestorer()
        };
        _restoreEngine = new RestoreEngine(restorers);

        _profileManager = new ProfileConnectionManager();
        _ = _profileManager.StartAsync("resume_pipe");

        _shutdownService = new ShutdownService(_sessionManager, _windowEnumerator, _profileManager);
        RefreshSessionList();
    }

    private void RefreshSessionList()
    {
        _sessions = new ObservableCollection<Session>(_sessionManager.ListSessions().OrderByDescending(s => s.CreatedAt));
        SessionsListBox.ItemsSource = _sessions;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string defaultName = "Manual save " + DateTime.Now.ToString("HH:mm");
        var nameDialog = new NamePromptWindow(defaultName) { Owner = this };
        if (nameDialog.ShowDialog() != true)
            return; // user cancelled

        var session = new Session
        {
            Label = nameDialog.SessionName,
            Source = "manual",
            Applications = _windowEnumerator.EnumerateWindows(),
            BrowserProfiles = _profileManager.GetConnectedProfiles()
        };

        await _sessionManager.SaveSessionAsync(session);
        RefreshSessionList();
        System.Windows.MessageBox.Show("Session saved.", "ReSume");
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (SessionsListBox.SelectedItem is Session selected)
        {
            await _restoreEngine.RestoreSessionAsync(selected);

            foreach (var bp in selected.BrowserProfiles)
            {
                var restoreMsg = JsonSerializer.Serialize(new
                {
                    action = "restore",
                    data = bp.Windows.Select(w => new
                    {
                        focused = true,
                        state = w.WindowState,
                        left = w.Position.X,
                        top = w.Position.Y,
                        width = w.Position.Width,
                        height = w.Position.Height,
                        incognito = w.IsIncognito,
                        tabs = w.Tabs.Select(t => new { url = t.Url })
                    })
                });
                await _profileManager.SendToProfileAsync(bp.ProfileId, restoreMsg);
            }

            System.Windows.MessageBox.Show("Restoration started.", "ReSume");
        }
    }

    private async void SaveShutdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show("Save and shutdown?", "ReSume", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            await _shutdownService.SaveAndShutdownAsync(false);
    }

    private void SessionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionsListBox.SelectedItem is Session session)
        {
            DetailPanel.Children.Clear();
            DetailPanel.Children.Add(new TextBlock { Text = session.Label, FontWeight = FontWeights.Bold, FontSize = 16 });
            DetailPanel.Children.Add(new TextBlock { Text = $"Saved: {session.CreatedAt:yyyy-MM-dd HH:mm:ss}" });
            DetailPanel.Children.Add(new TextBlock { Text = $"Source: {session.Source}" });
            DetailPanel.Children.Add(new TextBlock { Text = $"Applications ({session.Applications.Count})" });

            foreach (var app in session.Applications)
            {
                string docs = app.DocumentPaths.Count > 0 ? string.Join(", ", app.DocumentPaths) : "no doc";
                DetailPanel.Children.Add(new TextBlock { Text = $"  • {app.ProcessName} – {docs}", Margin = new Thickness(10, 0, 0, 0) });
            }

            if (session.BrowserProfiles.Count > 0)
            {
                DetailPanel.Children.Add(new TextBlock { Text = "Browser Profiles" });
                foreach (var bp in session.BrowserProfiles)
                    DetailPanel.Children.Add(new TextBlock { Text = $"  {bp.ProfileName} ({bp.Windows.Count} windows)" });
            }
        }
        else
        {
            DetailPanel.Children.Clear();
            DetailPanel.Children.Add(new TextBlock { Text = "Select a session", FontStyle = FontStyles.Italic, Foreground = System.Windows.Media.Brushes.Gray });
        }
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid sessionId)
        {
            _sessionManager.DeleteSession(sessionId);
            RefreshSessionList();
            DetailPanel.Children.Clear();
            DetailPanel.Children.Add(new TextBlock { Text = "Select a session", FontStyle = FontStyles.Italic, Foreground = System.Windows.Media.Brushes.Gray });
        }
    }

    private async void RunDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var diag = new DiagnosticsService();
        var checks = await diag.RunAllChecksAsync();
        DiagnosticsPanel.Children.Clear();
        foreach (var check in checks)
        {
            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock { Text = check.Passed ? "✅" : "❌", Width = 20 });
            sp.Children.Add(new TextBlock { Text = $"{check.Name}: {check.Details}", Width = 350 });
            if (check.CanRepair && !check.Passed)
            {
                var btn = new System.Windows.Controls.Button { Content = "Repair", Width = 70 };
                btn.Click += (s, ev) =>
                {
                    try
                    {
                        check.RepairAction?.Invoke();
                        RunDiagnostics_Click(sender, e);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show("Repair failed: " + ex.Message, "Repair Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                sp.Children.Add(btn);
            }
            DiagnosticsPanel.Children.Add(sp);
        }
    }
}