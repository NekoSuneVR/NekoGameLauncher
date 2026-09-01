using NekoGameLauncher.Infrastructure;
using NekoGameLauncher.Models;
using NekoGameLauncher.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace NekoGameLauncher.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LibraryService _libraryService = new();
    private readonly LauncherDetectionService _launcherDetection = new();
    private readonly SettingsService _settingsService = new();
    private readonly DealsService _dealsService = new();
    private readonly LaunchService _launchService = new();
    private AppSettings _settings = new();
    private string _searchText = string.Empty;
    private string _dealsQuery = string.Empty;
    private string _status = "Ready";
    private bool _freeOnly;
    private bool _useGamerPower = true;
    private bool _useCheapShark = true;
    private string _newEndpointName = string.Empty;
    private string _newEndpointUrl = string.Empty;

    public ObservableCollection<GameEntry> Games { get; } = [];
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
        LaunchGameCommand = new RelayCommand<GameEntry>(LaunchGame);
        OpenDealCommand = new RelayCommand<DealOffer>(deal => { if (deal is not null) _launchService.OpenUrl(deal.DealUrl); });
        OpenLookupCommand = new RelayCommand<GameLookupResult>(game => { if (game is not null) _launchService.OpenUrl(game.DealUrl); });
        AddEndpointCommand = new AsyncRelayCommand(AddEndpointAsync);
        RemoveEndpointCommand = new RelayCommand<CustomDealEndpoint>(RemoveEndpoint);
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
    public string NewEndpointName { get => _newEndpointName; set => Set(ref _newEndpointName, value); }
    public string NewEndpointUrl { get => _newEndpointUrl; set => Set(ref _newEndpointUrl, value); }

    public AsyncRelayCommand RefreshLibraryCommand { get; }
    public AsyncRelayCommand RefreshDealsCommand { get; }
    public AsyncRelayCommand LookupGamesCommand { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }
    public RelayCommand<GameEntry> LaunchGameCommand { get; }
    public RelayCommand<DealOffer> OpenDealCommand { get; }
    public RelayCommand<GameLookupResult> OpenLookupCommand { get; }
    public AsyncRelayCommand AddEndpointCommand { get; }
    public RelayCommand<CustomDealEndpoint> RemoveEndpointCommand { get; }

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        UseGamerPower = _settings.GamerPowerEnabled;
        UseCheapShark = _settings.CheapSharkEnabled;
        CustomEndpoints.Clear();
        foreach (var endpoint in _settings.CustomDealEndpoints) CustomEndpoints.Add(endpoint);

        var cached = await _libraryService.LoadCacheAsync();
        Replace(Games, cached);
        UpdateLaunchers();
        await RefreshLibraryAsync();
        await RefreshDealsAsync();
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
        Status = "Scanning installed launchers and games...";
        var games = await _libraryService.RefreshAsync();
        Replace(Games, games);
        UpdateLaunchers();
        Status = $"Found {Games.Count} installed games";
    }

    private async Task RefreshDealsAsync()
    {
        Status = "Checking game offers...";
        SyncSettingsFromUi();
        var deals = await _dealsService.GetOffersAsync(_settings, DealsQuery, FreeOnly);
        Replace(Deals, deals);
        Status = $"Loaded {Deals.Count} offers";
    }

    private async Task LookupGamesAsync()
    {
        if (string.IsNullOrWhiteSpace(DealsQuery))
        {
            Status = "Type a game name to look it up";
            return;
        }
        Status = $"Looking up {DealsQuery}...";
        try
        {
            var results = await _dealsService.SearchGamesAsync(DealsQuery);
            Replace(OnlineGames, results);
            Status = $"Found {OnlineGames.Count} matching games";
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
        _settings.CustomDealEndpoints = CustomEndpoints.ToList();
    }

    private void UpdateLaunchers() => Replace(Launchers, _launcherDetection.Detect(Games));

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
