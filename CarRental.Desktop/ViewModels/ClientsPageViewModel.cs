using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Services.Documents;
using CarRental.Shared.ReferenceData;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CarRental.Desktop.ViewModels;

public sealed partial class ClientsPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private const int PageSize = 40;

    private readonly RentalDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private readonly IClientDocumentStorage _clientDocumentStorage;
    private readonly Employee _currentEmployee;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private bool _isLoading;
    private ClientRow? _selectedClient;
    private string _statusMessage = string.Empty;
    private int _guideRequestId;
    private int _currentPage = 1;
    private int _totalClients;
    private string _searchText = string.Empty;
    private ClientPanelMode _panelMode = ClientPanelMode.Create;

    public ClientsPageViewModel(
        RentalDbContext dbContext,
        IAuthorizationService authorizationService,
        PageRefreshCoordinator refreshCoordinator,
        IClientDocumentStorage clientDocumentStorage,
        Employee currentEmployee)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _refreshCoordinator = refreshCoordinator;
        _clientDocumentStorage = clientDocumentStorage;
        _currentEmployee = currentEmployee;

        Editor = new ClientEditorDraft();
        Editor.PropertyChanged += Editor_OnPropertyChanged;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        ToggleBlacklistCommand = new AsyncRelayCommand(ToggleBlacklistAsync, CanManageSelectedClient);
        DeleteClientCommand = new AsyncRelayCommand(DeleteClientAsync, CanManageSelectedClient);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), CanMovePrevious);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), CanMoveNext);
        RequestGuideCommand = new RelayCommand(RequestGuide);
        StartCreateClientCommand = new RelayCommand(BeginCreateClient, () => !IsLoading);
        QuickCreateFromSearchCommand = new RelayCommand(BeginCreateClientFromSearch, CanQuickCreateFromSearch);
        EditClientCommand = new RelayCommand(BeginEditSelectedClient, CanManageSelectedClient);
        SaveClientCommand = new AsyncRelayCommand(SaveClientAsync, () => !IsLoading);
        CancelEditorCommand = new RelayCommand(CancelEditor, () => !IsLoading);
        OpenRentalsCommand = new AsyncRelayCommand(OpenRentalsAsync, CanManageSelectedClient);
        ClearPassportSelectionCommand = new RelayCommand(ClearPassportFileSelection, () => !IsLoading && Editor.HasPendingPassportSourceFile);
        ClearDriverLicenseSelectionCommand = new RelayCommand(ClearDriverLicenseFileSelection, () => !IsLoading && Editor.HasPendingDriverLicenseSourceFile);
    }

    public ObservableCollection<ClientRow> Clients { get; } = [];

    public ClientEditorDraft Editor { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ToggleBlacklistCommand { get; }

    public IAsyncRelayCommand DeleteClientCommand { get; }

    public IAsyncRelayCommand PreviousPageCommand { get; }

    public IAsyncRelayCommand NextPageCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public IRelayCommand StartCreateClientCommand { get; }

    public IRelayCommand QuickCreateFromSearchCommand { get; }

    public IRelayCommand EditClientCommand { get; }

    public IAsyncRelayCommand SaveClientCommand { get; }

    public IRelayCommand CancelEditorCommand { get; }

    public IAsyncRelayCommand OpenRentalsCommand { get; }

    public IRelayCommand ClearPassportSelectionCommand { get; }

    public IRelayCommand ClearDriverLicenseSelectionCommand { get; }

    public Func<int, Task>? OpenRentalsRequestedAsync { get; set; }

    public ClientRow? SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (SetProperty(ref _selectedClient, value))
            {
                if (value is not null)
                {
                    PanelMode = ClientPanelMode.Details;
                }

                NotifySelectionStateChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                NotifySearchStateChanged();
                _ = QueueSearchRefreshAsync();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ToggleBlacklistCommand.NotifyCanExecuteChanged();
                DeleteClientCommand.NotifyCanExecuteChanged();
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
                StartCreateClientCommand.NotifyCanExecuteChanged();
                QuickCreateFromSearchCommand.NotifyCanExecuteChanged();
                EditClientCommand.NotifyCanExecuteChanged();
                SaveClientCommand.NotifyCanExecuteChanged();
                CancelEditorCommand.NotifyCanExecuteChanged();
                OpenRentalsCommand.NotifyCanExecuteChanged();
                ClearPassportSelectionCommand.NotifyCanExecuteChanged();
                ClearDriverLicenseSelectionCommand.NotifyCanExecuteChanged();
                NotifySearchStateChanged();
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

    public int TotalClients
    {
        get => _totalClients;
        private set
        {
            if (SetProperty(ref _totalClients, value))
            {
                OnPropertyChanged(nameof(PageStatusText));
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
                NotifySearchStateChanged();
            }
        }
    }

    public ClientPanelMode PanelMode
    {
        get => _panelMode;
        private set
        {
            if (SetProperty(ref _panelMode, value))
            {
                OnPropertyChanged(nameof(IsCreateMode));
                OnPropertyChanged(nameof(IsEditMode));
                OnPropertyChanged(nameof(ShowClientDetails));
                OnPropertyChanged(nameof(ShowEditorPanel));
                OnPropertyChanged(nameof(RightPanelHeader));
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(EditorHint));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
    }

    public int GuideRequestId
    {
        get => _guideRequestId;
        private set => SetProperty(ref _guideRequestId, value);
    }

    public string PageStatusText
    {
        get
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalClients, 0) / PageSize));
            return $"Сторінка {CurrentPage}/{totalPages} • записів: {TotalClients}";
        }
    }

    public bool IsCreateMode => PanelMode == ClientPanelMode.Create;

    public bool IsEditMode => PanelMode == ClientPanelMode.Edit;

    public bool ShowClientDetails => SelectedClient is not null && PanelMode == ClientPanelMode.Details;

    public bool ShowEditorPanel => !ShowClientDetails;

    public string RightPanelHeader => ShowClientDetails ? "Картка клієнта" : EditorTitle;

    public string EditorTitle => PanelMode == ClientPanelMode.Edit
        ? "Редагування картки клієнта"
        : "Реєстрація нового клієнта за стійкою";

    public string EditorHint => PanelMode == ClientPanelMode.Edit
        ? "Оновіть базові дані клієнта та, за потреби, замініть вкладені документи."
        : "Створіть локальний профіль без web-акаунта, щоб одразу перейти до оформлення оренди.";

    public string SaveButtonText => PanelMode == ClientPanelMode.Edit ? "Зберегти зміни" : "Створити клієнта";

    public string BlacklistButtonText => SelectedClient?.IsBlacklisted == true
        ? "Прибрати з чорного списку"
        : "Додати до чорного списку";

    public bool ShowQuickCreateFromSearch => CanQuickCreateFromSearch();

    public string QuickCreateFromSearchText => ClientProfileConventions.TryNormalizePhone(SearchText) is { } normalizedPhone
        ? $"Клієнта не знайдено. Створити новий профіль з номером {normalizedPhone}?"
        : "Клієнта не знайдено. Створити новий профіль?";

    public override async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        await LoadClientsPageAsync(SelectedClient?.Id);
    }

    public void SelectPassportSourceFile(string filePath)
    {
        Editor.PassportSourceFilePath = filePath?.Trim() ?? string.Empty;
        StatusMessage = string.Empty;
    }

    public void SelectDriverLicenseSourceFile(string filePath)
    {
        Editor.DriverLicenseSourceFilePath = filePath?.Trim() ?? string.Empty;
        StatusMessage = string.Empty;
    }

    public void ReleaseTransientState()
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource = null;
        SelectedClient = null;
        StatusMessage = string.Empty;
        PanelMode = ClientPanelMode.Create;
        Editor.Reset();
    }

    public sealed record ClientRow(
        int Id,
        int? AccountId,
        string FullName,
        string Phone,
        string PassportData,
        DateTime? PassportExpirationDate,
        string? PassportStoredPath,
        string DriverLicense,
        DateTime? DriverLicenseExpirationDate,
        string? DriverLicenseStoredPath,
        bool IsBlacklisted)
    {
        public bool HasPassportAttachment => !string.IsNullOrWhiteSpace(PassportStoredPath);

        public bool HasDriverLicenseAttachment => !string.IsNullOrWhiteSpace(DriverLicenseStoredPath);

        public string PassportDataDisplay => string.IsNullOrWhiteSpace(PassportData) ? "Не вказано" : PassportData;

        public string DriverLicenseDisplay => string.IsNullOrWhiteSpace(DriverLicense) ? "Не вказано" : DriverLicense;

        public string PassportExpirationText => PassportExpirationDate?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "Не вказано";

        public string DriverLicenseExpirationText => DriverLicenseExpirationDate?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "Не вказано";

        public string AccessModeText => AccountId.HasValue ? "Прив'язаний web-акаунт" : "Локальний профіль";

        public string StatusText => IsBlacklisted ? "У чорному списку" : "Активний";

        public string PassportAttachmentText => HasPassportAttachment ? "Файл додано" : "Файл не додано";

        public string DriverLicenseAttachmentText => HasDriverLicenseAttachment ? "Файл додано" : "Файл не додано";
    }
}
