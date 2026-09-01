using NekoGameLauncher.Infrastructure;
using NekoGameLauncher.Models;
using NekoGameLauncher.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Threading;

namespace NekoGameLauncher.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LibraryService _libraryService = new();
    private readonly LauncherDetectionService _launcherDetection = new();
    private readonly SettingsService _settingsService = new();
    private readonly DealsService _dealsService = new();
    private readonly LaunchService _launchService = new();
    private readonly GameActivityService _activityService = new();
    private readonly SystemPerformanceService _performanceService = new();
    private readonly GamingModeService _gamingModeService = new();
    private readonly DispatcherTimer _monitorTimer;
    private AppSettings _settings = new();
    private string _searchText = string.Empty;
    private string _dealsQuery = string.Empty;
    private string _status = "Ready";
    private bool _freeOnly;
    private bool _useGamerPower = true;
    private bool _useCheapShark = true;
    private bool _autoGamingMode;
    private bool _boostGamePriority = true;
    private bool _isGamingModeEnabled;
    private bool _gamingModeWasAuto;
    private bool _monitorBusy;
    private string _newEndpointName = string.Empty;
    private string _newEndpointUrl = string.Empty;
    private double _systemCpuPercent;
    private double _memoryUsedGb;
    private double _memoryTotalGb;
    private double _memoryPercent;
    private int _activeGameCount;
    private string _activeGameName = "No game detected";
    private double _activeGameCpuPercent;
    private double _activeGameMemoryMb;
    private string _activeGameSession = "Idle";

    public ObservableCollection<GameEntry> Games { get; } = [];
    public ObservableCollection<GameEntry> TopPlayedGames { get; } = [];
    public ObservableCollection<DealOffer> Deals { get; } = [];
    public ObservableCollection<GameLookupResult> OnlineGames { get; } = [];
    public ObservableCollection<LauncherStatus> Launchers { get; } = [];
    public ObservableCollection<CustomDealEndpoint> CustomEndpoints { get; } = [];
    public ICollectionView GamesView { get; }

    public MainViewModel()
    {
        GamesView = CollectionViewSource.GetDefaultView(Games);
        GamesView.Filter = FilterGame;
        RefreshLibraryCommand = new AsyncRelayCommand(RefreshLibraryAsync);
        RefreshDealsCommand = new AsyncRelayCommand(RefreshDealsAsync);
        LookupGamesCommand = new AsyncRelayCommand(LookupGamesAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ToggleGamingModeCommand = new AsyncRelayCommand(ToggleGamingModeAsync);
        LaunchGameCommand = new RelayCommand<GameEntry>(LaunchGame);
        OpenDealCommand = new RelayCommand<DealOffer>(deal => { if (deal is not null) _launchService.OpenUrl(deal.DealUrl); });
        OpenLookupCommand = new RelayCommand<GameLookupResult>(game => { if (game is not null) _launchService.OpenUrl(game.DealUrl); });
        AddEndpointCommand = new AsyncRelayCommand(AddEndpointAsync);
        RemoveEndpointCommand = new RelayCommand<CustomDealEndpoint>(RemoveEndpoint);
        OpenWindowsGameModeCommand = new RelayCommand(GamingModeService.OpenWindowsGameModeSettings);
        OpenGraphicsSettingsCommand = new RelayCommand(GamingModeService.OpenGraphicsSettings);
        OpenTaskManagerCommand = new RelayCommand(GamingModeService.OpenTaskManager);

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _monitorTimer.Tick += async (_, _) => await MonitorTickAsync();
    }

    public string SearchText
    {
        get => _searchText;
        set { Set(ref _searchText, value); GamesView.Refresh(); }
    }

    public string DealsQuery { get => _dealsQuery; set => Set(ref _dealsQuery, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool FreeOnly { get => _freeOnly; set => Set(ref _freeOnly, value); }
    public bool UseGamerPower { get => _useGamerPower; set => Set(ref _useGamerPower, value); }
    public bool UseCheapShark { get => _useCheapShark; set => Set(ref _useCheapShark, value); }
    public bool AutoGamingMode { get => _autoGamingMode; set => Set(ref _autoGamingMode, value); }
    public bool BoostGamePriority { get => _boostGamePriority; set => Set(ref _boostGamePriority, value); }
    public string NewEndpointName { get => _newEndpointName; set => Set(ref _newEndpointName, value); }
    public string NewEndpointUrl { get => _newEndpointUrl; set => Set(ref _newEndpointUrl, value); }
    public string PricingRegion => _dealsService.RegionDescription;

    public bool IsGamingModeEnabled
    {
        get => _isGamingModeEnabled;
        private set
        {
            if (!Set(ref _isGamingModeEnabled, value)) return;
            OnPropertyChanged(nameof(GamingModeLabel));
            OnPropertyChanged(nameof(GamingModeDetail));
        }
    }

    public string GamingModeLabel => IsGamingModeEnabled ? "GAMING MODE ACTIVE" : "GAMING MODE OFF";
    public string GamingModeDetail => IsGamingModeEnabled ? "High Performance power plan + game process boost" : "Your normal Windows power plan is being used";
    public double SystemCpuPercent { get => _systemCpuPercent; set => Set(ref _systemCpuPercent, value); }
    public double MemoryUsedGb { get => _memoryUsedGb; private set => Set(ref _memoryUsedGb, value); }
    public double MemoryTotalGb { get => _memoryTotalGb; private set => Set(ref _memoryTotalGb, value); }
    public double MemoryPercent { get => _memoryPercent; set => Set(ref _memoryPercent, value); }
    public int ActiveGameCount { get => _activeGameCount; private set => Set(ref _activeGameCount, value); }
    public string ActiveGameName { get => _activeGameName; private set => Set(ref _activeGameName, value); }
    public double ActiveGameCpuPercent { get => _activeGameCpuPercent; private set => Set(ref _activeGameCpuPercent, value); }
    public double ActiveGameMemoryMb { get => _activeGameMemoryMb; private set => Set(ref _activeGameMemoryMb, value); }
    public string ActiveGameSession { get => _activeGameSession; private set => Set(ref _activeGameSession, value); }

    public AsyncRelayCommand RefreshLibraryCommand { get; }
    public AsyncRelayCommand RefreshDealsCommand { get; }
    public AsyncRelayCommand LookupGamesCommand { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }
    public AsyncRelayCommand ToggleGamingModeCommand { get; }
    public RelayCommand<GameEntry> LaunchGameCommand { get; }
    public RelayCommand<DealOffer> OpenDealCommand { get; }
    public RelayCommand<GameLookupResult> OpenLookupCommand { get; }
    public AsyncRelayCommand AddEndpointCommand { get; }
    public RelayCommand<CustomDealEndpoint> RemoveEndpointCommand { get; }
    public RelayCommand OpenWindowsGameModeCommand { get; }
    public RelayCommand OpenGraphicsSettingsCommand { get; }
    public RelayCommand OpenTaskManagerCommand { get; }

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        await _activityService.LoadAsync();
        UseGamerPower = _settings.GamerPowerEnabled;
        UseCheapShark = _settings.CheapSharkEnabled;
        AutoGamingMode = _settings.AutoGamingModeEnabled;
        BoostGamePriority = _settings.BoostGamePriorityEnabled;
        CustomEndpoints.Clear();
        foreach (var endpoint in _settings.CustomDealEndpoints) CustomEndpoints.Add(endpoint);

        var cached = await _libraryService.LoadCacheAsync();
        Replace(Games, cached);
        UpdateLaunchers();
        await RefreshLibraryAsync();
        await MonitorTickAsync();
        _monitorTimer.Start();
        await RefreshDealsAsync();
    }

    public async Task ShutdownAsync()
    {
        _monitorTimer.Stop();
        await _activityService.SaveAsync();
        if (_gamingModeService.IsEnabled) await _gamingModeService.DisableAsync();
    }

    public async Task RefreshLibraryFromManagementAsync()
    {
        await RefreshLibraryAsync();
    }

    private bool FilterGame(object value)
    {
        if (value is not GameEntry game) return false;
        return string.IsNullOrWhiteSpace(SearchText)
            || game.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || game.Launcher.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshLibraryAsync()
    {
        Status = "Scanning Steam, Epic, Wargaming, HoYoPlay, Kuro, standalone games and more...";
        var games = await _libraryService.RefreshAsync();
        Replace(Games, games);
        UpdateLaunchers();
        await MonitorTickAsync();
        Status = $"Found {Games.Count} installed games • right-click a game card to manage/uninstall";
    }

    private async Task MonitorTickAsync()
    {
        if (_monitorBusy) return;
        _monitorBusy = true;
        try
        {
            var activity = await _activityService.UpdateAsync(Games);
            var performance = _performanceService.GetSnapshot();
            SystemCpuPercent = performance.CpuPercent;
            MemoryUsedGb = performance.MemoryUsedGb;
            MemoryTotalGb = performance.MemoryTotalGb;
            MemoryPercent = performance.MemoryPercent;
            ActiveGameCount = activity.ActiveGameCount;

            var activeGame = Games.FirstOrDefault(x => x.IsRunning);
            ActiveGameName = activeGame?.Name ?? "No game detected";
            ActiveGameCpuPercent = activeGame?.CpuUsagePercent ?? 0;
            ActiveGameMemoryMb = activeGame?.MemoryMb ?? 0;
            ActiveGameSession = activeGame?.SessionLabel ?? "Idle";

            Replace(TopPlayedGames, Games.OrderByDescending(x => x.TotalPlayTimeSeconds).ThenBy(x => x.Name).Take(8));

            if (AutoGamingMode && activity.ActiveGameCount > 0 && !_gamingModeService.IsEnabled)
            {
                if (await _gamingModeService.EnableAsync()) _gamingModeWasAuto = true;
            }
            else if (_gamingModeWasAuto && (!AutoGamingMode || activity.ActiveGameCount == 0) && _gamingModeService.IsEnabled)
            {
                await _gamingModeService.DisableAsync();
                _gamingModeWasAuto = false;
            }

            if (_gamingModeService.IsEnabled)
                _gamingModeService.BoostProcesses(activity.GameProcessIds, BoostGamePriority);
            IsGamingModeEnabled = _gamingModeService.IsEnabled;
        }
        finally { _monitorBusy = false; }
    }

    private async Task ToggleGamingModeAsync()
    {
        if (_gamingModeService.IsEnabled)
        {
            await _gamingModeService.DisableAsync();
            _gamingModeWasAuto = false;
            Status = "Gaming Mode disabled and previous Windows power plan restored";
        }
        else
        {
            var enabled = await _gamingModeService.EnableAsync();
            _gamingModeWasAuto = false;
            Status = enabled ? "Gaming Mode enabled" : "Gaming Mode could not change the Windows power plan";
        }
        IsGamingModeEnabled = _gamingModeService.IsEnabled;
    }

    private async Task RefreshDealsAsync()
    {
        Status = $"Checking game offers • Windows pricing region {_dealsService.RegionDescription}...";
        SyncSettingsFromUi();
        var deals = await _dealsService.GetOffersAsync(_settings, DealsQuery, FreeOnly);
        Replace(Deals, deals);
        Status = $"Loaded {Deals.Count} offers • CheapShark prices are labelled USD • region {_dealsService.RegionDescription}";
    }

    private async Task LookupGamesAsync()
    {
        if (string.IsNullOrWhiteSpace(DealsQuery))
        {
            Status = "Type a game name to look it up";
            return;
        }
        Status = $"Looking up {DealsQuery} • checking Steam regional price for {_dealsService.RegionDescription}...";
        try
        {
            var results = await _dealsService.SearchGamesAsync(DealsQuery);
            Replace(OnlineGames, results);
            Status = $"Found {OnlineGames.Count} matching games • regional Steam pricing {_dealsService.RegionDescription}";
        }
        catch { Status = "Game lookup failed; try again later"; }
    }

    private void LaunchGame(GameEntry? game)
    {
        if (game is null) return;
        if (_launchService.Launch(game))
        {
            _ = _libraryService.SaveAsync(Games);
            Status = $"Launching {game.Name}";
        }
        else Status = $"Could not launch {game.Name}";
    }

    private async Task AddEndpointAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEndpointName) || !Uri.TryCreate(NewEndpointUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Status = "Enter an endpoint name and a valid http/https URL";
            return;
        }
        CustomEndpoints.Add(new CustomDealEndpoint { Name = NewEndpointName.Trim(), Url = uri.ToString(), Enabled = true });
        NewEndpointName = string.Empty;
        NewEndpointUrl = string.Empty;
        await SaveSettingsAsync();
        Status = "Deal endpoint added";
    }

    private void RemoveEndpoint(CustomDealEndpoint? endpoint)
    {
        if (endpoint is null) return;
        CustomEndpoints.Remove(endpoint);
        _ = SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        SyncSettingsFromUi();
        await _settingsService.SaveAsync(_settings);
        Status = "Settings saved";
    }

    private void SyncSettingsFromUi()
    {
        _settings.GamerPowerEnabled = UseGamerPower;
        _settings.CheapSharkEnabled = UseCheapShark;
        _settings.AutoGamingModeEnabled = AutoGamingMode;
        _settings.BoostGamePriorityEnabled = BoostGamePriority;
        _settings.CustomDealEndpoints = CustomEndpoints.ToList();
    }

    private void UpdateLaunchers() => Replace(Launchers, _launcherDetection.Detect(Games));

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event PropertyChangedEventHandler? PropertyChanged;
}
