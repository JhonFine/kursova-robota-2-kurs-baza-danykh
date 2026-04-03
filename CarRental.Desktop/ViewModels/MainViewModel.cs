using CarRental.Desktop.Localization;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CommunityToolkit.Mvvm.Input;

namespace CarRental.Desktop.ViewModels;

// Центральний shell desktop-клієнта: тримає активну сторінку, синхронізує навігацію з роллю
// та делегує refresh тільки тій області, яку користувач реально відкрив.
public sealed class MainViewModel : ViewModelBase
{
    private readonly IAuthorizationService _authorizationService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private object? _currentPage;
    private string _header = "Автопарк";
    private bool _isProkatPageActive;
    private bool _isUserRentalsPageActive;
    private bool _isFleetPageActive;
    private bool _isClientsPageActive;
    private bool _isReportsPageActive;
    private bool _isMaintenancePageActive;
    private bool _isDamagesPageActive;
    private bool _isAdminPageActive;
    private bool _isRentalsPageActive;

    public MainViewModel(
        Employee currentEmployee,
        IAuthorizationService authorizationService,
        PageRefreshCoordinator refreshCoordinator,
        FleetPageViewModel fleetPage,
        ClientsPageViewModel clientsPage,
        RentalsPageViewModel rentalsPage,
        ProkatPageViewModel prokatPage,
        UserRentalsPageViewModel userRentalsPage,
        ReportsPageViewModel reportsPage,
        MaintenancePageViewModel maintenancePage,
        DamagesPageViewModel damagesPage,
        AdminPageViewModel adminPage)
    {
        _authorizationService = authorizationService;
        _refreshCoordinator = refreshCoordinator;
        CurrentEmployee = currentEmployee;
        FleetPage = fleetPage;
        ClientsPage = clientsPage;
        RentalsPage = rentalsPage;
        ProkatPage = prokatPage;
        UserRentalsPage = userRentalsPage;
        ReportsPage = reportsPage;
        MaintenancePage = maintenancePage;
        DamagesPage = damagesPage;
        AdminPage = adminPage;

        ShowFleetCommand = new RelayCommand(ShowFleet);
        ShowClientsCommand = new RelayCommand(ShowClients);
        ShowRentalsCommand = new RelayCommand(ShowRentals);
        ShowProkatCommand = new RelayCommand(ShowProkat);
        ShowUserRentalsCommand = new RelayCommand(ShowUserRentals);
        ShowReportsCommand = new RelayCommand(ShowReports);
        ShowMaintenanceCommand = new RelayCommand(ShowMaintenance);
        ShowDamagesCommand = new RelayCommand(ShowDamages);
        ShowAdminCommand = new RelayCommand(ShowAdmin);
        ChangeSelfRoleCommand = new RelayCommand<string?>(_ => { });
        LogoutCommand = new RelayCommand(RequestLogout);

        // Координатор refresh знає про залежності між сторінками, тому реєстрація виконується один раз у shell.
        _refreshCoordinator.Register(PageRefreshArea.Fleet, FleetPage);
        _refreshCoordinator.Register(PageRefreshArea.Clients, ClientsPage);
        _refreshCoordinator.Register(PageRefreshArea.Rentals, RentalsPage);
        _refreshCoordinator.Register(PageRefreshArea.Prokat, ProkatPage);
        _refreshCoordinator.Register(PageRefreshArea.UserRentals, UserRentalsPage);
        _refreshCoordinator.Register(PageRefreshArea.Reports, ReportsPage);
        _refreshCoordinator.Register(PageRefreshArea.Maintenance, MaintenancePage);
        _refreshCoordinator.Register(PageRefreshArea.Damages, DamagesPage);
        _refreshCoordinator.Register(PageRefreshArea.Admin, AdminPage);

        // Користувач self-service потрапляє одразу в каталог, staff бачить операційний інтерфейс автопарку.
        if (IsUser)
        {
            ActivateProkatPage();
        }
        else
        {
            ActivateFleetPage();
        }
    }

    public Employee CurrentEmployee { get; }

    public string CurrentEmployeeRole => CurrentEmployee.RoleId.ToDisplay();

    public FleetPageViewModel FleetPage { get; }

    public ClientsPageViewModel ClientsPage { get; }

    public RentalsPageViewModel RentalsPage { get; }

    public ProkatPageViewModel ProkatPage { get; }

    public UserRentalsPageViewModel UserRentalsPage { get; }

    public ReportsPageViewModel ReportsPage { get; }

    public MaintenancePageViewModel MaintenancePage { get; }

    public DamagesPageViewModel DamagesPage { get; }

    public AdminPageViewModel AdminPage { get; }

    public IRelayCommand ShowFleetCommand { get; }

    public IRelayCommand ShowClientsCommand { get; }

    public IRelayCommand ShowRentalsCommand { get; }

    public IRelayCommand ShowProkatCommand { get; }

    public IRelayCommand ShowUserRentalsCommand { get; }

    public IRelayCommand ShowReportsCommand { get; }

    public IRelayCommand ShowMaintenanceCommand { get; }

    public IRelayCommand ShowDamagesCommand { get; }

    public IRelayCommand ShowAdminCommand { get; }

    public IRelayCommand<string?> ChangeSelfRoleCommand { get; }

    public IRelayCommand LogoutCommand { get; }

    public event Action? LogoutRequested;

    public bool IsUser => CurrentEmployee.RoleId == UserRole.User;

    public bool CanSelfManageRole
        => false;

    public string SelfRoleMenuHeader => $"Змінити мою роль ({CurrentEmployeeRole})";

    public bool ShowNavigationSidebar => !IsUser;

    public bool CanSeeFleet => !IsUser;

    public bool CanSeeClients => !IsUser;

    public bool CanSeeRentals => !IsUser;

    public bool CanSeeProkat => IsUser;

    public bool CanSeeUserRentals => IsUser;

    public bool CanSeeReports => !IsUser;

    public bool CanSeeMaintenance => !IsUser;

    public bool CanSeeDamages => !IsUser;

    public bool CanSeeAdmin => CurrentEmployee.RoleId == UserRole.Admin;

    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public bool IsRentalsPageActive
    {
        get => _isRentalsPageActive;
        private set => SetProperty(ref _isRentalsPageActive, value);
    }

    public bool IsProkatPageActive
    {
        get => _isProkatPageActive;
        private set => SetProperty(ref _isProkatPageActive, value);
    }

    public bool IsUserRentalsPageActive
    {
        get => _isUserRentalsPageActive;
        private set => SetProperty(ref _isUserRentalsPageActive, value);
    }

    public bool IsFleetPageActive
    {
        get => _isFleetPageActive;
        private set => SetProperty(ref _isFleetPageActive, value);
    }

    public bool IsClientsPageActive
    {
        get => _isClientsPageActive;
        private set => SetProperty(ref _isClientsPageActive, value);
    }

    public bool IsReportsPageActive
    {
        get => _isReportsPageActive;
        private set => SetProperty(ref _isReportsPageActive, value);
    }

    public bool IsMaintenancePageActive
    {
        get => _isMaintenancePageActive;
        private set => SetProperty(ref _isMaintenancePageActive, value);
    }

    public bool IsDamagesPageActive
    {
        get => _isDamagesPageActive;
        private set => SetProperty(ref _isDamagesPageActive, value);
    }

    public bool IsAdminPageActive
    {
        get => _isAdminPageActive;
        private set => SetProperty(ref _isAdminPageActive, value);
    }

    public async Task InitializeAsync()
    {
        await EnsureCurrentPageDataAsync();
    }

    private async void ShowFleet()
    {
        if (!CanSeeFleet)
        {
            return;
        }

        ActivateFleetPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Fleet, FleetPage);
    }

    private async void ShowClients()
    {
        if (!CanSeeClients)
        {
            return;
        }

        ActivateClientsPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Clients, ClientsPage);
    }

    private async void ShowRentals()
    {
        if (!CanSeeRentals)
        {
            return;
        }

        ActivateRentalsPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Rentals, RentalsPage);
    }

    private async void ShowProkat()
    {
        if (!CanSeeProkat)
        {
            return;
        }

        await NavigateToProkatAsync();
    }

    private async void ShowUserRentals()
    {
        if (!CanSeeUserRentals)
        {
            return;
        }

        ActivateUserRentalsPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.UserRentals, UserRentalsPage);
    }

    private async void ShowReports()
    {
        if (!CanSeeReports)
        {
            return;
        }

        ActivateReportsPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Reports, ReportsPage);
    }

    private async void ShowMaintenance()
    {
        if (!CanSeeMaintenance)
        {
            return;
        }

        ActivateMaintenancePage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Maintenance, MaintenancePage);
    }

    private async void ShowDamages()
    {
        if (!CanSeeDamages)
        {
            return;
        }

        ActivateDamagesPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Damages, DamagesPage);
    }

    private async void ShowAdmin()
    {
        if (!CanSeeAdmin)
        {
            return;
        }

        ActivateAdminPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Admin, AdminPage);
    }

    private Task EnsureCurrentPageDataAsync()
    {
        return CurrentPage switch
        {
            FleetPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Fleet, FleetPage),
            ClientsPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Clients, ClientsPage),
            RentalsPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Rentals, RentalsPage),
            ProkatPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Prokat, ProkatPage),
            UserRentalsPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.UserRentals, UserRentalsPage),
            ReportsPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Reports, ReportsPage),
            MaintenancePageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Maintenance, MaintenancePage),
            DamagesPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Damages, DamagesPage),
            AdminPageViewModel => _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Admin, AdminPage),
            _ => Task.CompletedTask
        };
    }

    private void ActivateFleetPage()
    {
        Header = "Автопарк";
        CurrentPage = FleetPage;
        SetActiveFlags(isFleet: true);
    }

    private void ActivateClientsPage()
    {
        Header = "Клієнти";
        CurrentPage = ClientsPage;
        SetActiveFlags(isClients: true);
    }

    private void ActivateRentalsPage()
    {
        Header = "Оренди";
        CurrentPage = RentalsPage;
        SetActiveFlags(isRentals: true);
    }

    private void ActivateProkatPage()
    {
        Header = "Прокат";
        CurrentPage = ProkatPage;
        SetActiveFlags(isProkat: true);
    }

    private void ActivateUserRentalsPage()
    {
        Header = "Мої бронювання та оренди";
        CurrentPage = UserRentalsPage;
        SetActiveFlags(isUserRentals: true);
    }

    private void ActivateReportsPage()
    {
        Header = "Звіти";
        CurrentPage = ReportsPage;
        SetActiveFlags(isReports: true);
    }

    private void ActivateMaintenancePage()
    {
        Header = "Техобслуговування";
        CurrentPage = MaintenancePage;
        SetActiveFlags(isMaintenance: true);
    }

    private void ActivateDamagesPage()
    {
        Header = "Пошкодження";
        CurrentPage = DamagesPage;
        SetActiveFlags(isDamages: true);
    }

    private void ActivateAdminPage()
    {
        Header = "Адміністрування";
        CurrentPage = AdminPage;
        SetActiveFlags(isAdmin: true);
    }

    private void SetActiveFlags(
        bool isProkat = false,
        bool isUserRentals = false,
        bool isFleet = false,
        bool isClients = false,
        bool isReports = false,
        bool isMaintenance = false,
        bool isDamages = false,
        bool isAdmin = false,
        bool isRentals = false)
    {
        IsFleetPageActive = false;
        IsClientsPageActive = false;
        IsProkatPageActive = false;
        IsUserRentalsPageActive = false;
        IsReportsPageActive = false;
        IsMaintenancePageActive = false;
        IsDamagesPageActive = false;
        IsAdminPageActive = false;
        IsRentalsPageActive = false;

        IsFleetPageActive = isFleet;
        IsClientsPageActive = isClients;
        IsProkatPageActive = isProkat;
        IsUserRentalsPageActive = isUserRentals;
        IsReportsPageActive = isReports;
        IsMaintenancePageActive = isMaintenance;
        IsDamagesPageActive = isDamages;
        IsAdminPageActive = isAdmin;
        IsRentalsPageActive = isRentals;
    }

    private void RequestLogout()
    {
        LogoutRequested?.Invoke();
    }

    public async Task NavigateToProkatAsync()
    {
        if (!CanSeeProkat)
        {
            return;
        }

        ActivateProkatPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Prokat, ProkatPage);
    }

    public async Task NavigateToRebookingAsync(int vehicleId)
    {
        if (!CanSeeProkat)
        {
            return;
        }

        ActivateProkatPage();
        await _refreshCoordinator.EnsurePageDataAsync(PageRefreshArea.Prokat, ProkatPage);
        await ProkatPage.PrepareRebookingAsync(vehicleId);
    }

    public async Task NavigateToRentalsAsync(int preferredClientId)
    {
        if (!CanSeeRentals)
        {
            return;
        }

        ActivateRentalsPage();
        await RentalsPage.PrepareForClientAsync(preferredClientId);
    }

    public async Task NavigateToFleetAsync(int preferredVehicleId, bool openDetails = true)
    {
        if (!CanSeeFleet)
        {
            return;
        }

        ActivateFleetPage();
        await FleetPage.PrepareForVehicleAsync(preferredVehicleId, openDetails);
    }
}

