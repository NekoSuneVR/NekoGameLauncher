using NekoGameLauncher.Models;
using NekoGameLauncher.Services;
using NekoGameLauncher.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NekoGameLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly GameManagementService _gameManagement = new();
    private bool _shutdownHandled;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        PreviewMouseRightButtonDown += GameCard_MouseRightButtonDown;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_shutdownHandled)
        {
            _shutdownHandled = true;
            try { _viewModel.ShutdownAsync().GetAwaiter().GetResult(); }
            catch { }
        }
        base.OnClosing(e);
    }

    private void GameCard_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var game = FindGameDataContext(e.OriginalSource as DependencyObject);
        if (game is null) return;

        var menu = new ContextMenu
        {
            Background = Brush("#0E1623"),
            Foreground = ResourceBrush("TextBrush"),
            BorderBrush = ResourceBrush("DividerBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        var manage = MenuItem("MANAGE / UNINSTALL…");
        manage.Click += (_, _) =>
        {
            var window = new GameManagementWindow(game, _viewModel.RefreshLibraryFromManagementAsync) { Owner = this };
            window.ShowDialog();
        };
        menu.Items.Add(manage);

        var folder = MenuItem("OPEN INSTALL FOLDER");
        folder.IsEnabled = !string.IsNullOrWhiteSpace(game.InstallPath) && Directory.Exists(game.InstallPath);
        folder.Click += (_, _) => _gameManagement.OpenInstallFolder(game);
        menu.Items.Add(folder);

        var apps = MenuItem("WINDOWS APPS & FEATURES");
        apps.Click += (_, _) => GameManagementService.OpenAppsFeatures();
        menu.Items.Add(apps);

        menu.IsOpen = true;
        e.Handled = true;
    }

    private static GameEntry? FindGameDataContext(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement element && element.DataContext is GameEntry game) return game;
            try { current = VisualTreeHelper.GetParent(current); }
            catch { current = LogicalTreeHelper.GetParent(current); }
        }
        return null;
    }

    private static MenuItem MenuItem(string header) => new()
    {
        Header = header,
        Foreground = ResourceBrush("TextBrush"),
        Background = Brushes.Transparent,
        Padding = new Thickness(10, 7, 10, 7)
    };

    private static Brush ResourceBrush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.White;

    private static SolidColorBrush Brush(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
