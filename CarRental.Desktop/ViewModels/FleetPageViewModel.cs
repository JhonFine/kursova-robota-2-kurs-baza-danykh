using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Infrastructure;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace CarRental.Desktop.ViewModels;

public sealed class FleetPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private const string AppDataRootDirectoryName = "CarRentalSystem";
    private const string VehiclePhotosDirectoryName = "VehiclePhotos";
    private const string AllMakesOption = "Усі марки";
    private const string AllClassesOption = "Усі класи";
    private const string AllStatusesOption = "Усі статуси";
    private const string StatusAvailable = "Доступне";
    private const string StatusUnavailable = "Недоступне";
    private const string StatusActiveRental = "Активна оренда";
    private const string SearchByCarKey = "car";
    private const string SearchByMileageKey = "mileage";
    private const string SearchByDailyRateKey = "dailyRate";
    private const string SearchByStatusKey = "status";
    private const string SearchByIdKey = "id";
    private const string SearchByLicensePlateKey = "licensePlate";
    private const string SearchByServiceIntervalKey = "serviceInterval";
    private const int PageSize = 40;

    private static readonly HashSet<string> SupportedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".webp"
    };

    private readonly RentalDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private readonly Employee _currentEmployee;
    private readonly List<FleetRow> _allVehicles = [];
    private bool _isLoading;
    private FleetRow? _selectedVehicle;
    private string _newDailyRate = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isVehicleDetailsDialogOpen;
    private string _selectedMakeFilter = AllMakesOption;
    private string _selectedClassFilter = AllClassesOption;
    private string _selectedStatusFilter = AllStatusesOption;
    private SearchFieldOption _selectedSearchField;
    private string _searchText = string.Empty;
    private bool _isSearchPanelOpen;
    private int _guideRequestId;
    private int _currentPage = 1;
    private int _totalVehicles;
    private bool _suppressQueryRefresh;

    public FleetPageViewModel(
        RentalDbContext dbContext,
        IAuthorizationService authorizationService,
        PageRefreshCoordinator refreshCoordinator,
        Employee currentEmployee)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _refreshCoordinator = refreshCoordinator;
        _currentEmployee = currentEmployee;
        _selectedSearchField = SearchFieldOptions[0];
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        UpdateRateCommand = new AsyncRelayCommand(UpdateRateAsync, () => !IsLoading);
        OpenVehicleDetailsCommand = new RelayCommand(OpenSelectedVehicleDetails, () => !IsLoading && SelectedVehicle is not null);
        CloseVehicleDetailsCommand = new RelayCommand(CloseVehicleDetailsDialog);
        ToggleSearchPanelCommand = new RelayCommand(ToggleSearchPanel);
        SearchCommand = new AsyncRelayCommand(() => RefreshForQueryChangeAsync(), () => !IsLoading);
        ClearSearchCommand = new AsyncRelayCommand(ClearSearchAsync, () => !IsLoading);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), CanMovePrevious);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), CanMoveNext);
        RequestGuideCommand = new RelayCommand(RequestGuide);
    }

    public ObservableCollection<FleetRow> Vehicles { get; } = [];
    public ObservableCollection<string> MakeFilterOptions { get; } = [AllMakesOption];
    public ObservableCollection<string> ClassFilterOptions { get; } =
    [
        AllClassesOption,
        "Економ",
        "Середній",
        "Бізнес",
        "Преміум",
        "Позашляховик",
        "Мінівен",
        "Комерційний",
        "Електромобіль",
        "Пікап",
        "Кабріолет"
    ];
    public ObservableCollection<string> StatusFilterOptions { get; } =
    [
        AllStatusesOption,
        StatusAvailable,
        StatusUnavailable,
        StatusActiveRental
    ];
    public ObservableCollection<SearchFieldOption> SearchFieldOptions { get; } =
    [
        new SearchFieldOption(SearchByCarKey, "Назва авто"),
        new SearchFieldOption(SearchByMileageKey, "Пробіг"),
        new SearchFieldOption(SearchByDailyRateKey, "Ціна/доба"),
        new SearchFieldOption(SearchByStatusKey, "Статус"),
        new SearchFieldOption(SearchByIdKey, "ID"),
        new SearchFieldOption(SearchByLicensePlateKey, "Номер"),
        new SearchFieldOption(SearchByServiceIntervalKey, "Інтервал ТО")
    ];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand UpdateRateCommand { get; }

    public IRelayCommand OpenVehicleDetailsCommand { get; }

    public IRelayCommand CloseVehicleDetailsCommand { get; }
    public IRelayCommand ToggleSearchPanelCommand { get; }
    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand ClearSearchCommand { get; }
    public IAsyncRelayCommand PreviousPageCommand { get; }
    public IAsyncRelayCommand NextPageCommand { get; }
    public IRelayCommand RequestGuideCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                UpdateRateCommand.NotifyCanExecuteChanged();
                OpenVehicleDetailsCommand.NotifyCanExecuteChanged();
                SearchCommand.NotifyCanExecuteChanged();
                ClearSearchCommand.NotifyCanExecuteChanged();
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageStatusText));
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TotalVehicles
    {
        get => _totalVehicles;
        private set
        {
            if (SetProperty(ref _totalVehicles, value))
            {
                OnPropertyChanged(nameof(PageStatusText));
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string PageStatusText
    {
        get
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalVehicles, 0) / PageSize));
            return $"Сторінка {CurrentPage}/{totalPages} • записів: {TotalVehicles}";
        }
    }

    public FleetRow? SelectedVehicle
    {
        get => _selectedVehicle;
        set
        {
            if (SetProperty(ref _selectedVehicle, value))
            {
                OpenVehicleDetailsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewDailyRate
    {
        get => _newDailyRate;
        set => SetProperty(ref _newDailyRate, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsVehicleDetailsDialogOpen
    {
        get => _isVehicleDetailsDialogOpen;
        set => SetProperty(ref _isVehicleDetailsDialogOpen, value);
    }

    public string SelectedMakeFilter
    {
        get => _selectedMakeFilter;
        set
        {
            if (SetProperty(ref _selectedMakeFilter, value))
            {
                if (!_suppressQueryRefresh)
                {
                    _ = RefreshForQueryChangeAsync();
                }
            }
        }
    }

    public string SelectedClassFilter
    {
        get => _selectedClassFilter;
        set
        {
            if (SetProperty(ref _selectedClassFilter, value))
            {
                if (!_suppressQueryRefresh)
                {
                    _ = RefreshForQueryChangeAsync();
                }
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                if (!_suppressQueryRefresh)
                {
                    _ = RefreshForQueryChangeAsync();
                }
            }
        }
    }

    public SearchFieldOption SelectedSearchField
    {
        get => _selectedSearchField;
        set
        {
            if (SetProperty(ref _selectedSearchField, value))
            {
                if (!_suppressQueryRefresh)
                {
                    _ = RefreshForQueryChangeAsync();
                }
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public bool IsSearchPanelOpen
    {
        get => _isSearchPanelOpen;
        set => SetProperty(ref _isSearchPanelOpen, value);
    }

    public int GuideRequestId
    {
        get => _guideRequestId;
        private set => SetProperty(ref _guideRequestId, value);
    }

    public bool CanUpdatePricing
        => _authorizationService.HasPermission(_currentEmployee, EmployeePermission.ManagePricing);

    public bool CanManageFleet
        => _authorizationService.HasPermission(_currentEmployee, EmployeePermission.ManageFleet);

    public override async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        var selectedId = SelectedVehicle?.Id;
        IsLoading = true;
        try
        {
            var today = DateTime.Today;
            var activeVehicleIds = await _dbContext.Rentals
                .AsNoTracking()
                .Where(item => item.Status == RentalStatus.Active && item.StartDate <= today && today <= item.EndDate)
                .Select(item => item.VehicleId)
                .Distinct()
                .ToListAsync();
            var active = activeVehicleIds.ToHashSet();

            await UpdateMakeFilterOptionsAsync();

            var query = ApplyVehicleFilters(_dbContext.Vehicles.AsNoTracking(), activeVehicleIds);
            TotalVehicles = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalVehicles, 0) / PageSize));
            if (CurrentPage > totalPages)
            {
                CurrentPage = totalPages;
            }

            var vehicles = await query
                .AsNoTracking()
                .OrderBy(item => item.Make)
                .ThenBy(item => item.Model)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            _allVehicles.Clear();
            foreach (var vehicle in vehicles)
            {
                var car = $"{vehicle.Make} {vehicle.Model}";
                var status = active.Contains(vehicle.Id)
                    ? StatusActiveRental
                    : vehicle.IsAvailable ? StatusAvailable : StatusUnavailable;
                _allVehicles.Add(new FleetRow(
                    vehicle.Id,
                    vehicle.Make,
                    car,
                    ResolveVehicleClass(car, vehicle.DailyRate),
                    vehicle.LicensePlate,
                    vehicle.Mileage,
                    vehicle.DailyRate,
                    vehicle.ServiceIntervalKm,
                    ResolveVehicleImageSource(vehicle),
                    status));
            }

            ApplyFilters(selectedId);
            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateRateAsync()
    {
        StatusMessage = string.Empty;
        if (!CanUpdatePricing)
        {
            StatusMessage = "Недостатньо прав.";
            return;
        }

        if (SelectedVehicle is null)
        {
            StatusMessage = "Оберіть авто.";
            return;
        }

        if (!TryParseDecimal(NewDailyRate, out var newRate))
        {
            StatusMessage = "Некоректна ціна.";
            return;
        }

        if (newRate <= 0)
        {
            StatusMessage = "Ціна має бути більшою за 0.";
            return;
        }

        var vehicle = await _dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == SelectedVehicle.Id);
        if (vehicle is null)
        {
            StatusMessage = "Авто не знайдено.";
            return;
        }

        vehicle.DailyRate = newRate;
        await _dbContext.SaveChangesAsync();
        _refreshCoordinator.Invalidate(PageRefreshArea.Prokat);
        StatusMessage = "Ціну за добу оновлено.";
        await RefreshAsync();
    }

    public void OpenSelectedVehicleDetails()
    {
        if (SelectedVehicle is null)
        {
            StatusMessage = "Оберіть авто для перегляду деталей.";
            return;
        }

        NewDailyRate = SelectedVehicle.DailyRate.ToString("0.##", CultureInfo.CurrentCulture);
        IsVehicleDetailsDialogOpen = true;
    }

    public void CloseVehicleDetailsDialog()
    {
        IsVehicleDetailsDialogOpen = false;
    }

    private void ToggleSearchPanel()
    {
        IsSearchPanelOpen = !IsSearchPanelOpen;
    }

    private async Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        await RefreshForQueryChangeAsync();
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        CloseVehicleDetailsDialog();
        NewDailyRate = string.Empty;
        StatusMessage = string.Empty;
    }

    private async Task RefreshForQueryChangeAsync()
    {
        if (CurrentPage != 1)
        {
            CurrentPage = 1;
        }

        await RefreshAsync();
    }

    private async Task ChangePageAsync(int delta)
    {
        var nextPage = Math.Max(1, CurrentPage + delta);
        if (nextPage == CurrentPage)
        {
            return;
        }

        CurrentPage = nextPage;
        await RefreshAsync();
    }

    private bool CanMovePrevious() => !IsLoading && CurrentPage > 1;

    private bool CanMoveNext()
    {
        if (IsLoading)
        {
            return false;
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalVehicles, 0) / PageSize));
        return CurrentPage < totalPages;
    }

    private IQueryable<Vehicle> ApplyVehicleFilters(IQueryable<Vehicle> query, IReadOnlyCollection<int> activeVehicleIds)
    {
        if (!string.Equals(SelectedMakeFilter, AllMakesOption, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => item.Make == SelectedMakeFilter);
        }

        if (!string.Equals(SelectedClassFilter, AllClassesOption, StringComparison.OrdinalIgnoreCase))
        {
            query = ApplyClassFilter(query, SelectedClassFilter);
        }

        if (!string.Equals(SelectedStatusFilter, AllStatusesOption, StringComparison.OrdinalIgnoreCase))
        {
            query = SelectedStatusFilter switch
            {
                StatusActiveRental => query.Where(item => activeVehicleIds.Contains(item.Id)),
                StatusAvailable => query.Where(item => !activeVehicleIds.Contains(item.Id) && item.IsAvailable),
                StatusUnavailable => query.Where(item => !activeVehicleIds.Contains(item.Id) && !item.IsAvailable),
                _ => query
            };
        }

        var searchValue = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(searchValue))
        {
            return query;
        }

        return SelectedSearchField.Key switch
        {
            SearchByCarKey => query.Where(item => item.Make.Contains(searchValue) || item.Model.Contains(searchValue)),
            SearchByLicensePlateKey => query.Where(item => item.LicensePlate.Contains(searchValue)),
            SearchByIdKey when int.TryParse(searchValue, out var parsedId) => query.Where(item => item.Id == parsedId),
            SearchByMileageKey when int.TryParse(searchValue, out var parsedMileage) => query.Where(item => item.Mileage == parsedMileage),
            SearchByDailyRateKey when TryParseDecimal(searchValue, out var parsedRate) => query.Where(item => item.DailyRate == parsedRate),
            SearchByServiceIntervalKey when int.TryParse(searchValue, out var parsedServiceInterval) => query.Where(item => item.ServiceIntervalKm == parsedServiceInterval),
            SearchByStatusKey => ApplyStatusSearchFilter(query, activeVehicleIds, searchValue),
            _ => query
        };
    }

    private static IQueryable<Vehicle> ApplyClassFilter(IQueryable<Vehicle> query, string selectedClass)
    {
        return selectedClass switch
        {
            "Кабріолет" => query.Where(item =>
                item.Make.Contains("MX-5") || item.Model.Contains("MX-5") ||
                item.Model.Contains("Z4") ||
                item.Model.Contains("500C") ||
                item.Model.Contains("Roadster") ||
                item.Model.Contains("Cabrio")),
            "Пікап" => query.Where(item =>
                item.Model.Contains("Hilux") ||
                item.Model.Contains("Ranger") ||
                item.Model.Contains("Navara") ||
                item.Model.Contains("D-Max") ||
                item.Model.Contains("Dmax")),
            "Електромобіль" => query.Where(item =>
                item.Model.Contains("Leaf") ||
                item.Model.Contains("Model 3") ||
                item.Model.Contains("Model Y") ||
                item.Model.Contains("Ioniq 5") ||
                item.Model.Contains("EV6")),
            "Комерційний" => query.Where(item =>
                item.Model.Contains("Transit") ||
                item.Model.Contains("Sprinter") ||
                item.Model.Contains("Vivaro")),
            "Мінівен" => query.Where(item =>
                item.Model.Contains("Trafi") ||
                item.Model.Contains("Kangoo") ||
                item.Model.Contains("Multivan")),
            "Позашляховик" => query.Where(item =>
                item.Model.Contains("Prado") ||
                item.Model.Contains("X5") ||
                item.Model.Contains("Q7") ||
                item.Model.Contains("GLE") ||
                item.Model.Contains("RAV4") ||
                item.Model.Contains("Sportage") ||
                item.Model.Contains("X-Trail") ||
                item.Model.Contains("Outlander") ||
                item.Model.Contains("Forester") ||
                item.Model.Contains("Tiguan") ||
                item.Model.Contains("Duster") ||
                item.Model.Contains("Wrangler") ||
                item.Model.Contains("Discovery") ||
                item.Model.Contains("Cayenne")),
            "Преміум" => query.Where(item => item.DailyRate >= 95m),
            "Бізнес" => query.Where(item => item.DailyRate >= 70m && item.DailyRate < 95m),
            "Середній" => query.Where(item => item.DailyRate >= 45m && item.DailyRate < 70m),
            "Економ" => query.Where(item => item.DailyRate < 45m),
            _ => query
        };
    }

    private static IQueryable<Vehicle> ApplyStatusSearchFilter(
        IQueryable<Vehicle> query,
        IReadOnlyCollection<int> activeVehicleIds,
        string searchValue)
    {
        var normalized = searchValue.Trim().ToLowerInvariant();
        if (StatusActiveRental.ToLowerInvariant().Contains(normalized, StringComparison.Ordinal))
        {
            return query.Where(item => activeVehicleIds.Contains(item.Id));
        }

        if (StatusAvailable.ToLowerInvariant().Contains(normalized, StringComparison.Ordinal))
        {
            return query.Where(item => !activeVehicleIds.Contains(item.Id) && item.IsAvailable);
        }

        if (StatusUnavailable.ToLowerInvariant().Contains(normalized, StringComparison.Ordinal))
        {
            return query.Where(item => !activeVehicleIds.Contains(item.Id) && !item.IsAvailable);
        }

        return query;
    }

    private async Task UpdateMakeFilterOptionsAsync()
    {
        var currentSelection = SelectedMakeFilter;
        var uniqueMakes = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(item => !string.IsNullOrWhiteSpace(item.Make))
            .Select(item => item.Make)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync();

        MakeFilterOptions.Clear();
        MakeFilterOptions.Add(AllMakesOption);
        foreach (var make in uniqueMakes)
        {
            MakeFilterOptions.Add(make);
        }

        _suppressQueryRefresh = true;
        try
        {
            if (string.IsNullOrWhiteSpace(currentSelection) || !MakeFilterOptions.Contains(currentSelection))
            {
                SelectedMakeFilter = AllMakesOption;
                return;
            }

            SelectedMakeFilter = currentSelection;
        }
        finally
        {
            _suppressQueryRefresh = false;
        }
    }

    private void ApplyFilters(int? preferredSelectedId)
    {
        Vehicles.Clear();
        foreach (var row in _allVehicles)
        {
            Vehicles.Add(row);
        }

        if (preferredSelectedId.HasValue)
        {
            SelectedVehicle = Vehicles.FirstOrDefault(item => item.Id == preferredSelectedId.Value);
        }
        else if (SelectedVehicle is not null && Vehicles.All(item => item.Id != SelectedVehicle.Id))
        {
            SelectedVehicle = null;
        }

        if (IsVehicleDetailsDialogOpen && SelectedVehicle is null)
        {
            IsVehicleDetailsDialogOpen = false;
        }
    }

    private static bool MatchesSearch(FleetRow row, string searchFieldKey, string searchValue)
    {
        return searchFieldKey switch
        {
            SearchByCarKey => IsFuzzyTextMatch(row.Car, searchValue),
            SearchByMileageKey => IsNumericMatch(row.Mileage, searchValue),
            SearchByDailyRateKey => IsDecimalMatch(row.DailyRate, searchValue),
            SearchByStatusKey => IsFuzzyTextMatch(row.Status, searchValue),
            SearchByIdKey => IsNumericMatch(row.Id, searchValue),
            SearchByLicensePlateKey => IsFuzzyTextMatch(row.LicensePlate, searchValue),
            SearchByServiceIntervalKey => IsNumericMatch(row.ServiceIntervalKm, searchValue),
            _ => false
        };
    }

    private static bool IsNumericMatch(int value, string searchValue)
    {
        var digits = new string(searchValue.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
        {
            return false;
        }

        var text = value.ToString(CultureInfo.InvariantCulture);
        return text.Contains(digits, StringComparison.Ordinal);
    }

    private static bool IsDecimalMatch(decimal value, string searchValue)
    {
        if (TryParseDecimal(searchValue, out var parsed))
        {
            return value == parsed;
        }

        var normalizedSearch = searchValue.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        var invariantText = value.ToString("0.##", CultureInfo.InvariantCulture);
        var currentCultureText = value.ToString("0.##", CultureInfo.CurrentCulture);
        return invariantText.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
               currentCultureText.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFuzzyTextMatch(string candidate, string query)
    {
        var normalizedCandidate = NormalizeTextForSearch(candidate);
        var normalizedQuery = NormalizeTextForSearch(query);
        if (string.IsNullOrWhiteSpace(normalizedCandidate) || string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return false;
        }

        if (normalizedCandidate.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return true;
        }

        var candidateTokens = normalizedCandidate
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var queryTokens = normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (candidateTokens.Length == 0 || queryTokens.Length == 0)
        {
            return false;
        }

        foreach (var queryToken in queryTokens)
        {
            var hasTokenMatch = candidateTokens.Any(candidateToken => IsFuzzyTokenMatch(candidateToken, queryToken));
            if (!hasTokenMatch)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFuzzyTokenMatch(string candidateToken, string queryToken)
    {
        if (candidateToken.Contains(queryToken, StringComparison.Ordinal))
        {
            return true;
        }

        if (queryToken.Length <= 2)
        {
            return false;
        }

        var allowedDistance = GetAllowedDistance(queryToken.Length);
        if (Math.Abs(candidateToken.Length - queryToken.Length) > allowedDistance)
        {
            return false;
        }

        return ComputeLevenshteinDistance(candidateToken, queryToken) <= allowedDistance;
    }

    private static int GetAllowedDistance(int queryLength)
    {
        return queryLength switch
        {
            <= 4 => 1,
            <= 8 => 2,
            _ => 3
        };
    }

    private static string NormalizeTextForSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        var buffer = new List<char>(normalized.Length);
        var previousWasSpace = false;
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(ch);
                previousWasSpace = false;
                continue;
            }

            if (previousWasSpace)
            {
                continue;
            }

            buffer.Add(' ');
            previousWasSpace = true;
        }

        return new string(buffer.ToArray()).Trim();
    }

    private static int ComputeLevenshteinDistance(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.Ordinal))
        {
            return 0;
        }

        if (source.Length == 0)
        {
            return target.Length;
        }

        if (target.Length == 0)
        {
            return source.Length;
        }

        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];

        for (var j = 0; j <= target.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var substitutionCost = source[i - 1] == target[j - 1] ? 0 : 1;
                var insertion = current[j - 1] + 1;
                var deletion = previous[j] + 1;
                var substitution = previous[j - 1] + substitutionCost;
                current[j] = Math.Min(Math.Min(insertion, deletion), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private static string ResolveVehicleClass(string car, decimal dailyRate)
    {
        var normalized = car.Trim().ToLowerInvariant();

        if (ContainsAny(normalized, "mx-5", "z4", "500c", "roadster", "cabrio"))
        {
            return "Кабріолет";
        }

        if (ContainsAny(normalized, "hilux", "ranger", "navara", "d-max", "dmax"))
        {
            return "Пікап";
        }

        if (ContainsAny(normalized, "leaf", "model 3", "model y", "ioniq 5", "ev6"))
        {
            return "Електромобіль";
        }

        if (ContainsAny(normalized, "transit", "sprinter", "vivaro"))
        {
            return "Комерційний";
        }

        if (ContainsAny(normalized, "trafi", "kangoo", "multivan"))
        {
            return "Мінівен";
        }

        if (ContainsAny(normalized, "prado", "x5", "q7", "gle", "rav4", "sportage", "x-trail", "outlander", "forester", "tiguan", "duster", "wrangler", "discovery", "cayenne"))
        {
            return "Позашляховик";
        }

        if (dailyRate >= 95m)
        {
            return "Преміум";
        }

        if (dailyRate >= 70m)
        {
            return "Бізнес";
        }

        return dailyRate >= 45m ? "Середній" : "Економ";
    }

    private static bool ContainsAny(string source, params string[] tokens)
    {
        return tokens.Any(token => source.Contains(token, StringComparison.Ordinal));
    }

    public async Task AddVehicleAsync(AddVehicleDraft draft)
    {
        StatusMessage = string.Empty;
        var result = await TryCreateVehicleAsync(draft);
        StatusMessage = result.Message;
        if (result.Success)
        {
            await RefreshAsync();
        }
    }

    private async Task<AddVehicleResult> TryCreateVehicleAsync(AddVehicleDraft draft)
    {
        if (!CanManageFleet)
        {
            return new AddVehicleResult(false, "Недостатньо прав для додавання авто.");
        }

        if (string.IsNullOrWhiteSpace(draft.Make) ||
            string.IsNullOrWhiteSpace(draft.Model) ||
            string.IsNullOrWhiteSpace(draft.LicensePlate))
        {
            return new AddVehicleResult(false, "Заповніть марку, модель і номер авто.");
        }

        if (!int.TryParse(draft.Mileage, out var mileage) || mileage < 0)
        {
            return new AddVehicleResult(false, "Некоректний пробіг.");
        }

        if (!TryParseDecimal(draft.DailyRate, out var dailyRate) || dailyRate <= 0)
        {
            return new AddVehicleResult(false, "Некоректна ціна за добу.");
        }

        if (!int.TryParse(draft.ServiceIntervalKm, out var serviceInterval) || serviceInterval <= 0)
        {
            return new AddVehicleResult(false, "Некоректний інтервал ТО.");
        }

        var catalogSeed = VehicleCatalogSeeds.TryFindByVehicle(draft.Make, draft.Model);
        var engineDisplay = ResolveVehicleSpecValue(draft.EngineDisplay, catalogSeed?.EngineDisplay);
        var fuelType = ResolveVehicleSpecValue(draft.FuelType, catalogSeed?.FuelType);
        var transmissionType = ResolveVehicleSpecValue(draft.TransmissionType, catalogSeed?.TransmissionType);
        var cargoCapacityDisplay = ResolveVehicleSpecValue(draft.CargoCapacityDisplay, catalogSeed?.CargoCapacityDisplay);
        var consumptionDisplay = ResolveVehicleSpecValue(draft.ConsumptionDisplay, catalogSeed?.ConsumptionDisplay);
        var doorsText = ResolveVehicleSpecValue(draft.DoorsCount, catalogSeed?.DoorsCount.ToString(CultureInfo.InvariantCulture));

        if (string.IsNullOrWhiteSpace(engineDisplay) ||
            string.IsNullOrWhiteSpace(fuelType) ||
            string.IsNullOrWhiteSpace(transmissionType) ||
            string.IsNullOrWhiteSpace(cargoCapacityDisplay) ||
            string.IsNullOrWhiteSpace(consumptionDisplay))
        {
            return new AddVehicleResult(false, "Заповніть характеристики авто або оберіть модель, яка вже є в каталозі.");
        }

        if (!int.TryParse(doorsText, out var doorsCount) || doorsCount is < 1 or > 8)
        {
            return new AddVehicleResult(false, "Некоректна кількість дверей.");
        }

        var normalizedPlate = draft.LicensePlate.Trim().ToUpperInvariant();
        var exists = await _dbContext.Vehicles
            .AnyAsync(item => item.LicensePlate.ToUpper() == normalizedPlate);
        if (exists)
        {
            return new AddVehicleResult(false, "Авто з таким номером вже існує.");
        }

        if (!TryStorePhotoCopy(draft.PhotoPath, normalizedPlate, out var storedPhotoPath, out var photoError))
        {
            return new AddVehicleResult(false, photoError);
        }

        var vehicle = new Vehicle
        {
            Make = draft.Make.Trim(),
            Model = draft.Model.Trim(),
            EngineDisplay = engineDisplay,
            FuelType = fuelType,
            TransmissionType = transmissionType,
            DoorsCount = doorsCount,
            CargoCapacityDisplay = cargoCapacityDisplay,
            ConsumptionDisplay = consumptionDisplay,
            HasAirConditioning = draft.HasAirConditioning,
            LicensePlate = normalizedPlate,
            Mileage = mileage,
            DailyRate = dailyRate,
            IsAvailable = true,
            ServiceIntervalKm = serviceInterval,
            PhotoPath = storedPhotoPath
        };

        _dbContext.Vehicles.Add(vehicle);
        await _dbContext.SaveChangesAsync();
        return new AddVehicleResult(true, "Авто додано в автопарк.");
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static string ResolveVehicleSpecValue(string? value, string? fallback)
    {
        var normalized = value?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return fallback?.Trim() ?? string.Empty;
    }

    private static bool TryStorePhotoCopy(
        string? sourcePhotoPath,
        string normalizedPlate,
        out string? storedPhotoPath,
        out string errorMessage)
    {
        storedPhotoPath = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(sourcePhotoPath))
        {
            return true;
        }

        var sourcePath = sourcePhotoPath.Trim();
        if (!File.Exists(sourcePath))
        {
            errorMessage = "Вибране фото не знайдено.";
            return false;
        }

        try
        {
            storedPhotoPath = SavePhotoToLocalStorage(sourcePath, normalizedPlate);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is ArgumentException)
        {
            errorMessage = $"Не вдалося зберегти фото: {ex.Message}";
            return false;
        }
    }

    private static string SavePhotoToLocalStorage(string sourcePath, string normalizedPlate)
    {
        var photosDirectory = GetVehiclePhotosDirectoryPath();
        Directory.CreateDirectory(photosDirectory);

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension) || !SupportedPhotoExtensions.Contains(extension))
        {
            extension = ".jpg";
        }

        var plateSlug = ToSlug(normalizedPlate).Replace("-", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(plateSlug))
        {
            plateSlug = Guid.NewGuid().ToString("N");
        }

        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{plateSlug}{extension.ToLowerInvariant()}";
        var destinationPath = Path.Combine(photosDirectory, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: false);

        return destinationPath;
    }

    private static string GetVehiclePhotosDirectoryPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return Path.Combine(localAppData, AppDataRootDirectoryName, VehiclePhotosDirectoryName);
    }

    private string ResolveVehicleImageSource(Vehicle vehicle)
    {
        if (VehiclePhotoCatalog.TryResolveStoredPhotoPath(vehicle.PhotoPath, out var fullPath) &&
            !string.IsNullOrWhiteSpace(fullPath))
        {
            return fullPath;
        }

        return string.Empty;
    }

    private static string ToSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .ToLowerInvariant()
            .Replace("+", "plus", StringComparison.Ordinal);

        var buffer = new List<char>(normalized.Length);
        var previousDash = false;
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(ch);
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                buffer.Add('-');
                previousDash = true;
            }
        }

        return new string(buffer.ToArray()).Trim('-');
    }

    public sealed record FleetRow(
        int Id,
        string Make,
        string Car,
        string CarClass,
        string LicensePlate,
        int Mileage,
        decimal DailyRate,
        int ServiceIntervalKm,
        string ImageSource,
        string Status)
    {
        public bool HasImage => !string.IsNullOrWhiteSpace(ImageSource);
    }

    public sealed record SearchFieldOption(string Key, string Label);

    private sealed record AddVehicleResult(bool Success, string Message);
}
