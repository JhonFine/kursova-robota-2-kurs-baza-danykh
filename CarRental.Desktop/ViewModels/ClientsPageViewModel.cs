using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace CarRental.Desktop.ViewModels;

public sealed class ClientsPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private const int PageSize = 40;

    private readonly RentalDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private readonly Employee _currentEmployee;
    private bool _isLoading;
    private ClientRow? _selectedClient;
    private string _statusMessage = string.Empty;
    private int _guideRequestId;
    private int _currentPage = 1;
    private int _totalClients;

    public ClientsPageViewModel(
        RentalDbContext dbContext,
        IAuthorizationService authorizationService,
        PageRefreshCoordinator refreshCoordinator,
        Employee currentEmployee)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _refreshCoordinator = refreshCoordinator;
        _currentEmployee = currentEmployee;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        ToggleBlacklistCommand = new AsyncRelayCommand(ToggleBlacklistAsync, () => !IsLoading);
        DeleteClientCommand = new AsyncRelayCommand(DeleteClientAsync, () => !IsLoading);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), CanMovePrevious);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), CanMoveNext);
        RequestGuideCommand = new RelayCommand(RequestGuide);
    }

    public ObservableCollection<ClientRow> Clients { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ToggleBlacklistCommand { get; }

    public IAsyncRelayCommand DeleteClientCommand { get; }

    public IAsyncRelayCommand PreviousPageCommand { get; }

    public IAsyncRelayCommand NextPageCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public ClientRow? SelectedClient
    {
        get => _selectedClient;
        set => SetProperty(ref _selectedClient, value);
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

    public override async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            TotalClients = await _dbContext.Clients
                .AsNoTracking()
                .CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalClients, 0) / PageSize));
            if (CurrentPage > totalPages)
            {
                CurrentPage = totalPages;
            }

            var clients = await _dbContext.Clients
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            Clients.Clear();
            foreach (var client in clients)
            {
                Clients.Add(new ClientRow(
                    client.Id,
                    client.FullName,
                    FormatPhoneForDisplay(client.Phone),
                    client.DriverLicense,
                    client.Blacklisted));
            }

            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleBlacklistAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            StatusMessage = "Оберіть клієнта.";
            return;
        }

        var client = await _dbContext.Clients.FirstOrDefaultAsync(item => item.Id == SelectedClient.Id);
        if (client is null)
        {
            StatusMessage = "Клієнта не знайдено.";
            return;
        }

        client.Blacklisted = !client.Blacklisted;
        await _dbContext.SaveChangesAsync();
        _refreshCoordinator.Invalidate(PageRefreshArea.Rentals);
        StatusMessage = client.Blacklisted ? "Клієнта додано до чорного списку." : "Клієнта прибрано з чорного списку.";
        await RefreshAsync();
    }

    private async Task DeleteClientAsync()
    {
        StatusMessage = string.Empty;
        if (!_authorizationService.HasPermission(_currentEmployee, EmployeePermission.DeleteRecords))
        {
            StatusMessage = "Недостатньо прав.";
            return;
        }

        if (SelectedClient is null)
        {
            StatusMessage = "Оберіть клієнта.";
            return;
        }

        var hasRentals = await _dbContext.Rentals.AnyAsync(item => item.ClientId == SelectedClient.Id);
        if (hasRentals)
        {
            StatusMessage = "Клієнт має оренди, видалення неможливе.";
            return;
        }

        var client = await _dbContext.Clients.FirstOrDefaultAsync(item => item.Id == SelectedClient.Id);
        if (client is null)
        {
            StatusMessage = "Клієнта не знайдено.";
            return;
        }

        _dbContext.Clients.Remove(client);
        await _dbContext.SaveChangesAsync();
        _refreshCoordinator.Invalidate(PageRefreshArea.Rentals);
        if (Clients.Count == 1 && CurrentPage > 1)
        {
            CurrentPage--;
        }
        StatusMessage = "Клієнта видалено.";
        await RefreshAsync();
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        SelectedClient = null;
        StatusMessage = string.Empty;
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

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalClients, 0) / PageSize));
        return CurrentPage < totalPages;
    }

    public sealed record ClientRow(
        int Id,
        string FullName,
        string Phone,
        string DriverLicense,
        bool Blacklisted);

    private static string FormatPhoneForDisplay(string? value)
    {
        var normalized = TryNormalizePhone(value);
        return normalized ?? "Не вказано";
    }

    private static string? TryNormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            return null;
        }

        return "+" + digits;
    }
}
