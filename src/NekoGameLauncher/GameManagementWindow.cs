using NekoGameLauncher.Models;
using NekoGameLauncher.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NekoGameLauncher;

public sealed class GameManagementWindow : Window
{
    private readonly GameEntry _game;
    private readonly GameManagementService _service = new();
    private readonly Func<Task> _afterChange;
    private readonly StackPanel _candidateList = new();
    private readonly TextBlock _cleanupSummary = new();
    private readonly TextBlock _status = new();
    private readonly CheckBox _includeUserData = new();
    private readonly Button _deleteButton = new();
    private IReadOnlyList<CleanupCandidate> _candidates = [];

    public GameManagementWindow(GameEntry game, Func<Task> afterChange)
    {
        _game = game;
        _afterChange = afterChange;
        Title = $"Manage {game.Name}";
        Width = 760;
        Height = 680;
        MinWidth = 680;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextBrush");
        Content = BuildUi();
    }

    private UIElement BuildUi()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBar = new Border
        {
            Background = Brush("#090D15"),
            BorderBrush = ResourceBrush("DividerBrush"),
            BorderThickness = new Thickness(1, 1, 1, 0)
        };
        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            try { DragMove(); } catch { }
        };
        var titleGrid = new Grid { Margin = new Thickness(13, 0, 6, 0) };
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleGrid.Children.Add(new TextBlock
        {
            Text = $"MANAGE GAME  •  {_game.Name}",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ResourceBrush("TextBrush")
        });
        var close = new Button { Content = "✕", Width = 44, Height = 30, Padding = new Thickness(0), Margin = new Thickness(0) };
        close.SetResourceReference(StyleProperty, "WindowButtonStyle");
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        titleGrid.Children.Add(close);
        titleBar.Child = titleGrid;
        root.Children.Add(titleBar);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        var body = new StackPanel { Margin = new Thickness(26, 22, 26, 28) };
        scroll.Content = body;

        body.Children.Add(new TextBlock { Text = "Game Management", FontSize = 27, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("TextBrush") });
        body.Children.Add(new TextBlock
        {
            Text = "Uninstall through the game’s registered launcher/Windows entry, then scan for remnants separately.",
            Foreground = ResourceBrush("MutedTextBrush"), Margin = new Thickness(0, 5, 0, 18), TextWrapping = TextWrapping.Wrap
        });

        body.Children.Add(BuildGameInfoCard());
        body.Children.Add(BuildUninstallCard());
        body.Children.Add(BuildCleanupCard());

        _status.Foreground = ResourceBrush("DimTextBrush");
        _status.Margin = new Thickness(2, 12, 0, 0);
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Text = "Nothing has been removed. Cleanup always requires a preview and confirmation.";
        body.Children.Add(_status);
        return root;
    }

    private Border BuildGameInfoCard()
    {
        var card = Card();
        card.Margin = new Thickness(0, 0, 0, 14);
        var panel = new StackPanel();
        panel.Children.Add(Label("GAME"));
        panel.Children.Add(new TextBlock { Text = _game.Name, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 3), Foreground = ResourceBrush("TextBrush") });
        panel.Children.Add(new TextBlock { Text = $"{_game.Launcher}  •  {_game.PlayTimeLabel} playtime  •  {_game.LaunchCount} sessions", Foreground = ResourceBrush("MutedTextBrush") });
        panel.Children.Add(new TextBlock { Text = _game.InstallPath, Foreground = ResourceBrush("DimTextBrush"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0) });
        card.Child = panel;
        return card;
    }

    private Border BuildUninstallCard()
    {
        var plan = _service.GetUninstallPlan(_game);
        var card = Card();
        card.Margin = new Thickness(0, 0, 0, 14);
        var panel = new StackPanel();
        panel.Children.Add(Label("UNINSTALL"));
        panel.Children.Add(new TextBlock { Text = plan.Label, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 3), Foreground = ResourceBrush("TextBrush") });
        panel.Children.Add(new TextBlock { Text = plan.Detail, Foreground = ResourceBrush("MutedTextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });

        var buttons = new WrapPanel();
        var uninstall = new Button { Content = "UNINSTALL / OPEN UNINSTALLER", Padding = new Thickness(16, 8, 16, 8) };
        uninstall.Click += (_, _) => StartUninstall();
        buttons.Children.Add(uninstall);
        var openFolder = SecondaryButton("OPEN INSTALL FOLDER");
        openFolder.Click += (_, _) =>
        {
            _status.Text = _service.OpenInstallFolder(_game) ? "Opened the install folder." : "The install folder no longer exists.";
        };
        buttons.Children.Add(openFolder);
        var apps = SecondaryButton("WINDOWS APPS & FEATURES");
        apps.Click += (_, _) => GameManagementService.OpenAppsFeatures();
        buttons.Children.Add(apps);
        panel.Children.Add(buttons);
        card.Child = panel;
        return card;
    }

    private Border BuildCleanupCard()
    {
        var card = Card();
        var panel = new StackPanel();
        panel.Children.Add(Label("LEFTOVER CLEANUP"));
        panel.Children.Add(new TextBlock { Text = "Preview before deleting", FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 3), Foreground = ResourceBrush("TextBrush") });
        panel.Children.Add(new TextBlock
        {
            Text = "By default Neko only offers game-scoped install remnants and matching temporary folders. Save/config folders are excluded.",
            Foreground = ResourceBrush("MutedTextBrush"), TextWrapping = TextWrapping.Wrap
        });

        _includeUserData.Content = "Advanced: also scan matching save/settings folders in AppData and Documents\\My Games";
        _includeUserData.Foreground = ResourceBrush("TextBrush");
        _includeUserData.Margin = new Thickness(0, 12, 0, 8);
        _includeUserData.Checked += async (_, _) => await ScanAsync();
        _includeUserData.Unchecked += async (_, _) => await ScanAsync();
        panel.Children.Add(_includeUserData);

        var actions = new WrapPanel();
        var scan = SecondaryButton("SCAN LEFTOVERS");
        scan.Click += async (_, _) => await ScanAsync();
        actions.Children.Add(scan);
        _deleteButton.Content = "DELETE SAFE LEFTOVERS";
        _deleteButton.Padding = new Thickness(16, 8, 16, 8);
        _deleteButton.IsEnabled = false;
        _deleteButton.Click += async (_, _) => await DeleteAsync();
        actions.Children.Add(_deleteButton);
        panel.Children.Add(actions);

        _cleanupSummary.Foreground = ResourceBrush("DimTextBrush");
        _cleanupSummary.Margin = new Thickness(2, 9, 0, 5);
        _cleanupSummary.Text = "Run a scan to see what remains.";
        panel.Children.Add(_cleanupSummary);

        var listHost = new Border
        {
            Background = Brush("#0A111C"), BorderBrush = ResourceBrush("DividerBrush"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9), Padding = new Thickness(10), MaxHeight = 220
        };
        listHost.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _candidateList };
        panel.Children.Add(listHost);
        card.Child = panel;
        return card;
    }

    private void StartUninstall()
    {
        if (_game.IsRunning)
        {
            MessageBox.Show(this, "Close the game before uninstalling it.", "Game is running", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var plan = _service.GetUninstallPlan(_game);
        var answer = MessageBox.Show(this,
            $"Neko will start this uninstall flow:\n\n{plan.Label}\n{plan.Detail}\n\nContinue?",
            $"Uninstall {_game.Name}", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _service.StartUninstall(_game, out var message);
        _status.Text = message + " After it finishes, use Scan Leftovers and then rescan the library.";
    }

    private async Task ScanAsync()
    {
        _status.Text = "Scanning for game-scoped remnants...";
        _candidateList.Children.Clear();
        _deleteButton.IsEnabled = false;
        var includeUserData = _includeUserData.IsChecked == true;
        _candidates = await Task.Run(() => _service.GetCleanupCandidates(_game, includeUserData));
        var safe = _candidates.Where(candidate => candidate.SafeToDelete).ToList();
        var safeBytes = safe.Sum(candidate => candidate.SizeBytes);
        _cleanupSummary.Text = _candidates.Count == 0
            ? "No matching leftovers were found."
            : $"Found {_candidates.Count} locations • {safe.Count} eligible for deletion • {FormatSize(safeBytes)} selected by safety rules";

        foreach (var candidate in _candidates)
        {
            var row = new Border { Background = Brush("#0E1623"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 7) };
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = $"{candidate.Category}  •  {candidate.SizeLabel}  •  {(candidate.SafeToDelete ? "READY" : "REVIEW ONLY")}",
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = candidate.SafeToDelete ? ResourceBrush("SuccessBrush") : Brush("#FFB86C")
            });
            content.Children.Add(new TextBlock { Text = candidate.Path, Foreground = ResourceBrush("MutedTextBrush"), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            row.Child = content;
            _candidateList.Children.Add(row);
        }
        _deleteButton.IsEnabled = safe.Count > 0;
        _status.Text = includeUserData
            ? "Advanced cleanup preview includes matching user save/settings folders. Nothing is deleted until you confirm."
            : "Save/config folders are excluded from this cleanup preview.";
    }

    private async Task DeleteAsync()
    {
        var includeUserData = _includeUserData.IsChecked == true;
        var selected = _candidates.Where(candidate => candidate.SafeToDelete && (!candidate.UserData || includeUserData)).ToList();
        if (selected.Count == 0) return;
        if (_game.IsRunning)
        {
            MessageBox.Show(this, "Close the game before deleting leftovers.", "Game is running", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var message = $"Permanently delete {selected.Count} leftover locations ({FormatSize(selected.Sum(x => x.SizeBytes))}) for {_game.Name}?";
        if (includeUserData) message += "\n\nWARNING: this selection includes matching save/settings folders and can remove local saves or configuration.";
        var first = MessageBox.Show(this, message, "Confirm leftover cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (first != MessageBoxResult.Yes) return;
        if (includeUserData)
        {
            var second = MessageBox.Show(this, "Confirm again: delete the matching user save/settings folders too?", "Delete user data", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (second != MessageBoxResult.Yes) return;
        }

        _deleteButton.IsEnabled = false;
        _status.Text = "Deleting confirmed leftovers...";
        var result = await _service.DeleteCandidatesAsync(selected, includeUserData);
        _status.Text = $"Deleted {result.Deleted.Count} locations" + (result.Failed.Count > 0 ? $" • {result.Failed.Count} could not be removed" : string.Empty) + ".";
        await _afterChange();
        await ScanAsync();
    }

    private static Border Card() => new()
    {
        Background = ResourceBrush("PanelBrush"), BorderBrush = ResourceBrush("DividerBrush"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(13), Padding = new Thickness(18)
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = ResourceBrush("CyanBrush")
    };

    private static Button SecondaryButton(string text)
    {
        var button = new Button { Content = text, Padding = new Thickness(14, 8, 14, 8) };
        button.SetResourceReference(StyleProperty, "SecondaryButtonStyle");
        return button;
    }

    private static Brush ResourceBrush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.White;

    private static SolidColorBrush Brush(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private static string FormatSize(long bytes)
        => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.0} GB"
            : bytes >= 1024L * 1024 ? $"{bytes / (1024d * 1024):0.0} MB"
            : $"{bytes / 1024d:0} KB";
}
