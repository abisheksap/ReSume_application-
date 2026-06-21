using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReSume.Core.Models;
using ReSume.Core.Services;
using ReSume.Views;

namespace ReSume;

public partial class MainWindow : Window
{
    private readonly SessionManager _sessionManager;
    private readonly WindowEnumerator _windowEnumerator;
    private readonly RestoreEngine _restoreEngine;
    private readonly ShutdownService _shutdownService;
    private readonly ProfileConnectionManager _profileManager;
    private ObservableCollection<Session> _sessions = new();
    private bool _isDark = false;

    public MainWindow()
    {
        InitializeComponent();
        string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReSume");
        _sessionManager = new SessionManager(basePath);
        _windowEnumerator = new WindowEnumerator();
        var restorers = new ReSume.Core.Restorers.IAppRestorer[]
        {
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

        ApplyTheme(false);
        RefreshSessionList();

        // Wire up tab switching
        TabSessions.Selected  += (_, _) => ShowPanel(SessionsPanel, DiagnosticsPanel);
        TabDiag.Selected      += (_, _) => ShowPanel(DiagnosticsPanel, SessionsPanel);
    }

    // ── Theme ─────────────────────────────────────────────────────────────

    private void ThemeToggle_Checked(object sender, RoutedEventArgs e)   => ApplyTheme(true);
    private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e) => ApplyTheme(false);

    private void ApplyTheme(bool dark)
    {
        _isDark = dark;
        ThemeLabel.Text      = dark ? "🌙" : "☀";
        ThemeLabelRight.Text = dark ? "☀"  : "🌙";

        var d = Application.Current.Resources;

        if (dark)
        {
            Set(d, "WindowBg",        "#0F1117");
            Set(d, "SidebarBg",       "#16181F");
            Set(d, "DetailBg",        "#12141A");
            Set(d, "CardBg",          "#1E2130");
            Set(d, "CardBorder",      "#2A2D3E");
            Set(d, "CardHover",       "#232640");
            Set(d, "CardSelected",    "#1E2F6A");
            Set(d, "AccentBlue",      "#5C7CFA");
            Set(d, "AccentBlueDark",  "#4466E8");
            Set(d, "TextPrimary",     "#E8EAFF");
            Set(d, "TextSecondary",   "#8892B0");
            Set(d, "TextMuted",       "#5A6480");
            Set(d, "Divider",         "#2A2D3E");
            Set(d, "TabBarBg",        "#16181F");
            Set(d, "HeaderBg",        "#1A1D28");
            Set(d, "ButtonSecBg",     "#1E2130");
            Set(d, "ButtonSecFg",     "#C9D1D9");
            Set(d, "ButtonSecBorder", "#2A2D3E");
            Set(d, "TagAppBg",        "#1C1F40");
            Set(d, "TagAppFg",        "#818CF8");
            Set(d, "TagBrowserBg",    "#0F2010");
            Set(d, "TagBrowserFg",    "#4ADE80");
            Set(d, "SectionHeaderFg", "#8892B0");
            Set(d, "DiagPassBg",      "#0D1F10");
            Set(d, "DiagFailBg",      "#1F0D0D");
            Set(d, "DiagPassBorder",  "#1A4020");
            Set(d, "DiagFailBorder",  "#401A1A");
        }
        else
        {
            Set(d, "WindowBg",        "#F5F6FA");
            Set(d, "SidebarBg",       "#FFFFFF");
            Set(d, "DetailBg",        "#FAFBFD");
            Set(d, "CardBg",          "#FFFFFF");
            Set(d, "CardBorder",      "#E2E5ED");
            Set(d, "CardHover",       "#EEF2FF");
            Set(d, "CardSelected",    "#E0E8FF");
            Set(d, "AccentBlue",      "#4F6EF7");
            Set(d, "AccentBlueDark",  "#3A56D4");
            Set(d, "TextPrimary",     "#1A1D2E");
            Set(d, "TextSecondary",   "#6B7280");
            Set(d, "TextMuted",       "#9CA3AF");
            Set(d, "Divider",         "#E5E7EB");
            Set(d, "TabBarBg",        "#FFFFFF");
            Set(d, "HeaderBg",        "#F0F2F8");
            Set(d, "ButtonSecBg",     "#F3F4F6");
            Set(d, "ButtonSecFg",     "#374151");
            Set(d, "ButtonSecBorder", "#D1D5DB");
            Set(d, "TagAppBg",        "#EEF2FF");
            Set(d, "TagAppFg",        "#4F46E5");
            Set(d, "TagBrowserBg",    "#F0FDF4");
            Set(d, "TagBrowserFg",    "#16A34A");
            Set(d, "SectionHeaderFg", "#374151");
            Set(d, "DiagPassBg",      "#F0FDF4");
            Set(d, "DiagFailBg",      "#FEF2F2");
            Set(d, "DiagPassBorder",  "#BBF7D0");
            Set(d, "DiagFailBorder",  "#FEE2E2");
        }
    }

    private static void Set(ResourceDictionary d, string key, string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        if (d.Contains(key)) d[key] = new SolidColorBrush(c);
        else d.Add(key, new SolidColorBrush(c));
    }

    // ── Panel switching ────────────────────────────────────────────────────

    private static void ShowPanel(UIElement show, UIElement hide)
    {
        show.Visibility = Visibility.Visible;
        hide.Visibility = Visibility.Collapsed;
    }

    // ── Session list ──────────────────────────────────────────────────────

    private void RefreshSessionList()
    {
        _sessions = new ObservableCollection<Session>(
            _sessionManager.ListSessions().OrderByDescending(s => s.CreatedAt));
        SessionsListBox.ItemsSource = _sessions;
        SessionCountLabel.Text = $"{_sessions.Count} session{(_sessions.Count == 1 ? "" : "s")}";
        SetStatus($"{_sessions.Count} sessions loaded");
    }

    // ── Save ──────────────────────────────────────────────────────────────

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string defaultName = "Session  " + DateTime.Now.ToString("MMM d  HH:mm");
        var nameDialog = new NamePromptWindow(defaultName) { Owner = this };
        if (nameDialog.ShowDialog() != true) return;

        SaveButton.IsEnabled = false;
        SetStatus("Capturing windows…");

        // Enumerate desktop windows first (fast)
        var apps = _windowEnumerator.EnumerateWindows();

        // Actively request tab data from each connected Chrome profile
        SetStatus("Capturing browser tabs…");
        var connectedProfiles = _profileManager.GetConnectedProfiles();
        foreach (var profile in connectedProfiles)
            await _profileManager.CaptureProfileAsync(profile.ProfileId);

        var session = new Session
        {
            Label           = nameDialog.SessionName,
            Source          = "manual",
            Applications    = apps,
            BrowserProfiles = _profileManager.GetConnectedProfiles() // now has real tab data
        };

        await _sessionManager.SaveSessionAsync(session);
        RefreshSessionList();
        SaveButton.IsEnabled = true;
        SetStatus($"Saved: {session.Label}");
    }

    // ── Restore ───────────────────────────────────────────────────────────

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (SessionsListBox.SelectedItem is not Session selected) return;

        RestoreButton.IsEnabled = false;
        SetStatus("Restoring session…");
        await _restoreEngine.RestoreSessionAsync(selected);
        await _restoreEngine.RestoreBrowserProfilesAsync(selected.BrowserProfiles, _profileManager);
        RestoreButton.IsEnabled = true;
        SetStatus("Restoration started.");
    }

    // ── Save & Shutdown ───────────────────────────────────────────────────

    private async void SaveShutdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Save the current session and shut down Windows?",
                            "Save & Shutdown", MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes)
        {
            await _shutdownService.SaveAndShutdownAsync(false);
        }
    }

    // ── Selection / detail ────────────────────────────────────────────────

    private void SessionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionsListBox.SelectedItem is Session session)
        {
            RestoreButton.IsEnabled = true;
            RenderDetail(session);
        }
        else
        {
            RestoreButton.IsEnabled = false;
            ShowEmptyState();
        }
    }

    private void RenderDetail(Session session)
    {
        DetailPanel.Children.Clear();

        // ── Header ──────────────────────────────────────────────────────────
        var hdr = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        hdr.Children.Add(new TextBlock
        {
            Text       = session.Label,
            FontSize   = 22,
            FontWeight = FontWeights.Bold,
            Foreground = GetBrush("TextPrimary"),
            TextWrapping = TextWrapping.Wrap
        });
        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        metaRow.Children.Add(MakeMeta($"🕐 {session.CreatedAt.LocalDateTime:MMM d, yyyy  HH:mm}"));
        metaRow.Children.Add(MakeMeta($"  ·  {session.Source}"));
        metaRow.Children.Add(MakeMeta($"  ·  v{session.Version}"));
        hdr.Children.Add(metaRow);
        DetailPanel.Children.Add(hdr);

        // ── Stats row ──────────────────────────────────────────────────────
        var stats = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) };
        int tabCount = session.BrowserProfiles.Sum(p => p.Windows.Sum(w => w.Tabs.Count));
        stats.Children.Add(StatCard("🖥", session.Applications.Count.ToString(), "apps"));
        stats.Children.Add(StatCard("🌐", session.BrowserProfiles.Count.ToString(), "browser profiles"));
        stats.Children.Add(StatCard("🗂", tabCount.ToString(), "tabs"));
        DetailPanel.Children.Add(stats);

        // Divider
        DetailPanel.Children.Add(MakeDivider());

        // ── Applications ──────────────────────────────────────────────────
        DetailPanel.Children.Add(SectionHeader("Applications", session.Applications.Count));

        if (session.Applications.Count == 0)
        {
            DetailPanel.Children.Add(EmptySection("No applications were captured"));
        }
        else
        {
            foreach (var app in session.Applications)
            {
                var card = new Border
                {
                    Background      = GetBrush("CardBg"),
                    BorderBrush     = GetBrush("CardBorder"),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(12, 10, 12, 10),
                    Margin          = new Thickness(0, 0, 0, 6)
                };
                var sp = new StackPanel();
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
                nameRow.Children.Add(new TextBlock
                {
                    Text       = app.ProcessName,
                    FontWeight = FontWeights.SemiBold,
                    FontSize   = 13,
                    Foreground = GetBrush("TextPrimary")
                });
                sp.Children.Add(nameRow);
                if (app.DocumentPaths.Count > 0)
                {
                    foreach (var doc in app.DocumentPaths)
                        sp.Children.Add(new TextBlock
                        {
                            Text         = "  📄 " + doc,
                            FontSize     = 11,
                            Foreground   = GetBrush("TextSecondary"),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            Margin       = new Thickness(0, 2, 0, 0)
                        });
                }
                if (app.Windows.Count > 0)
                {
                    var win = app.Windows[0];
                    sp.Children.Add(new TextBlock
                    {
                        Text       = $"  🪟 {win.Position.Width}×{win.Position.Height}  @ ({win.Position.X},{win.Position.Y})  [{win.WindowState}]",
                        FontSize   = 11,
                        Foreground = GetBrush("TextMuted"),
                        Margin     = new Thickness(0, 2, 0, 0)
                    });
                }
                card.Child = sp;
                DetailPanel.Children.Add(card);
            }
        }

        // ── Browser Profiles ──────────────────────────────────────────────
        if (session.BrowserProfiles.Count > 0)
        {
            DetailPanel.Children.Add(MakeDivider());
            DetailPanel.Children.Add(SectionHeader("Browser Profiles", session.BrowserProfiles.Count));

            foreach (var profile in session.BrowserProfiles)
            {
                int profileTabs = profile.Windows.Sum(w => w.Tabs.Count);
                var card = new Border
                {
                    Background      = GetBrush("CardBg"),
                    BorderBrush     = GetBrush("CardBorder"),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(8),
                    Padding         = new Thickness(12, 10, 12, 10),
                    Margin          = new Thickness(0, 0, 0, 8)
                };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock
                {
                    Text       = $"🌐 {profile.ProfileName}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize   = 13,
                    Foreground = GetBrush("TextPrimary")
                });
                sp.Children.Add(new TextBlock
                {
                    Text       = $"{profile.Windows.Count} window(s)  ·  {profileTabs} tab(s)",
                    FontSize   = 11,
                    Foreground = GetBrush("TextSecondary"),
                    Margin     = new Thickness(0, 3, 0, 6)
                });

                foreach (var win in profile.Windows)
                {
                    foreach (var tab in win.Tabs.Take(5))
                    {
                        sp.Children.Add(new TextBlock
                        {
                            Text         = $"  • {tab.Title ?? tab.Url}",
                            FontSize     = 11,
                            Foreground   = GetBrush("TextSecondary"),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            Margin       = new Thickness(0, 1, 0, 0)
                        });
                    }
                    if (win.Tabs.Count > 5)
                        sp.Children.Add(new TextBlock
                        {
                            Text       = $"  …and {win.Tabs.Count - 5} more",
                            FontSize   = 11,
                            Foreground = GetBrush("TextMuted"),
                            FontStyle  = FontStyles.Italic,
                            Margin     = new Thickness(0, 2, 0, 0)
                        });
                }
                card.Child = sp;
                DetailPanel.Children.Add(card);
            }
        }

        EmptyState.Visibility = Visibility.Collapsed;
    }

    private void ShowEmptyState()
    {
        DetailPanel.Children.Clear();
        DetailPanel.Children.Add(EmptyState);
        EmptyState.Visibility = Visibility.Visible;
    }

    // ── Rename ────────────────────────────────────────────────────────────

    private async void RenameSession_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button btn && btn.Tag is Guid sessionId)
        {
            var existing = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
            if (existing == null) return;

            var dlg = new NamePromptWindow(existing.Label) { Owner = this, Title = "Rename Session" };
            if (dlg.ShowDialog() != true) return;

            // Delete old file, save with new label
            _sessionManager.DeleteSession(sessionId);
            existing.Label = dlg.SessionName;
            await _sessionManager.SaveSessionAsync(existing);
            RefreshSessionList();
            SetStatus($"Renamed to: {existing.Label}");
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button btn && btn.Tag is Guid sessionId)
        {
            var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
            string label = session?.Label ?? "this session";
            if (MessageBox.Show($"Delete \"{label}\"?\n\nThis cannot be undone.",
                                "Delete Session", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                == MessageBoxResult.Yes)
            {
                _sessionManager.DeleteSession(sessionId);
                RefreshSessionList();
                ShowEmptyState();
                SetStatus($"Deleted: {label}");
            }
        }
    }

    // ── Diagnostics ───────────────────────────────────────────────────────

    private async void RunDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("Running diagnostics…");
        DiagnosticsStack.Children.Clear();

        var diag = new DiagnosticsService();
        var checks = await diag.RunAllChecksAsync();

        foreach (var check in checks)
        {
            var row = new Border
            {
                Background      = check.Passed ? GetBrush("DiagPassBg") : GetBrush("DiagFailBg"),
                BorderBrush     = check.Passed ? GetBrush("DiagPassBorder") : GetBrush("DiagFailBorder"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(14, 10, 14, 10),
                Margin          = new Thickness(0, 0, 0, 6)
            };
            var inner = new Grid();
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textCol = new StackPanel();
            textCol.Children.Add(new TextBlock
            {
                Text       = (check.Passed ? "✅  " : "❌  ") + check.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize   = 13,
                Foreground = GetBrush("TextPrimary")
            });
            textCol.Children.Add(new TextBlock
            {
                Text       = check.Details,
                FontSize   = 11,
                Foreground = GetBrush("TextSecondary"),
                Margin     = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(textCol, 0);
            inner.Children.Add(textCol);

            if (check.CanRepair && !check.Passed)
            {
                var repairBtn = new Button
                {
                    Content = "Repair",
                    Style   = (Style)FindResource("BtnSecondary"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                repairBtn.Click += (s, ev) =>
                {
                    try { check.RepairAction?.Invoke(); RunDiagnostics_Click(sender, e); }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Repair failed:\n" + ex.Message, "Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                Grid.SetColumn(repairBtn, 1);
                inner.Children.Add(repairBtn);
            }

            row.Child = inner;
            DiagnosticsStack.Children.Add(row);
        }

        int passed = checks.Count(c => c.Passed);
        SetStatus($"Diagnostics: {passed}/{checks.Count} checks passed");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private Brush GetBrush(string key) =>
        TryFindResource(key) as Brush ?? Brushes.Gray;

    private void SetStatus(string text) => StatusBar.Text = text;

    private TextBlock MakeMeta(string text) => new()
    {
        Text       = text,
        FontSize   = 12,
        Foreground = GetBrush("TextSecondary")
    };

    private Border MakeDivider() => new()
    {
        Height          = 1,
        Background      = GetBrush("Divider"),
        Margin          = new Thickness(0, 16, 0, 16)
    };

    private StackPanel StatCard(string icon, string value, string label)
    {
        var sp = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 24, 0)
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = icon, FontSize = 16, Margin = new Thickness(0, 0, 6, 0) });
        row.Children.Add(new TextBlock
        {
            Text       = value,
            FontSize   = 22,
            FontWeight = FontWeights.Bold,
            Foreground = GetBrush("TextPrimary")
        });
        sp.Children.Add(row);
        sp.Children.Add(new TextBlock
        {
            Text       = label,
            FontSize   = 11,
            Foreground = GetBrush("TextMuted")
        });
        return sp;
    }

    private TextBlock SectionHeader(string title, int count)
    {
        return new TextBlock
        {
            Text       = $"{title.ToUpper()}  ({count})",
            FontSize   = 11,
            FontWeight = FontWeights.Bold,
            Foreground = GetBrush("SectionHeaderFg"),
            Margin     = new Thickness(0, 0, 0, 10),
            LetterSpacing = 1
        };
    }

    private TextBlock EmptySection(string text) => new()
    {
        Text       = text,
        FontSize   = 12,
        FontStyle  = FontStyles.Italic,
        Foreground = GetBrush("TextMuted"),
        Margin     = new Thickness(0, 0, 0, 10)
    };
}
