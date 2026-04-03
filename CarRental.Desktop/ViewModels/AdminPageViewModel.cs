using CarRental.Desktop.Data;
using CarRental.Desktop.Infrastructure;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace CarRental.Desktop.ViewModels;

// Адмін-сторінка концентрує керування доступом staff-акаунтів:
// фільтрацію, блокування, активацію та примусову зміну пароля без окремих діалогів.
public sealed class AdminPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private readonly RentalDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly IAuthorizationService _authorizationService;
    private readonly Employee _currentEmployee;

    private bool _isLoading;
    private EmployeeRow? _selectedEmployee;
    private string _statusMessage = string.Empty;
    private string _statusKind = "Info";
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _filterQuery = string.Empty;
    private int _totalEmployees;
    private int _activeEmployees;
    private int _managersCount;
    private int _lockedEmployees;
    private int _guideRequestId;

    public event Action? RequestPasswordFieldsClear;

    public AdminPageViewModel(
        RentalDbContext dbContext,
        IAuthService authService,
        IAuthorizationService authorizationService,
        Employee currentEmployee)
    {
        _dbContext = dbContext;
        _authService = authService;
        _authorizationService = authorizationService;
        _currentEmployee = currentEmployee;

        // CollectionView дає локальний фільтр без повторного походу в БД на кожне введення в поле пошуку.
        EmployeesView = CollectionViewSource.GetDefaultView(Employees);
        EmployeesView.Filter = FilterEmployee;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanExecuteRefresh);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, CanExecuteEmployeeAction);
        ToggleManagerRoleCommand = new AsyncRelayCommand(ToggleManagerRoleAsync, CanExecuteEmployeeAction);
        UnlockCommand = new AsyncRelayCommand(UnlockAsync, CanExecuteEmployeeAction);
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync, CanExecuteChangePassword);
        RequestGuideCommand = new RelayCommand(RequestGuide);

        SetInfo("Оберіть працівника у таблиці, щоб керувати його доступом.");
    }

    public ObservableCollection<EmployeeRow> Employees { get; } = [];

    public ICollectionView EmployeesView { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ToggleActiveCommand { get; }

    public IAsyncRelayCommand ToggleManagerRoleCommand { get; }

    public IAsyncRelayCommand UnlockCommand { get; }

    public IAsyncRelayCommand ChangePasswordCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public EmployeeRow? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            if (SetProperty(ref _selectedEmployee, value))
            {
                OnPropertyChanged(nameof(HasSelectedEmployee));
                OnPropertyChanged(nameof(SelectedEmployeeDisplayName));
                OnPropertyChanged(nameof(SelectedEmployeeRoleDisplay));
                OnPropertyChanged(nameof(SelectedEmployeeStateDisplay));
                OnPropertyChanged(nameof(SelectedEmployeeLockoutDisplay));
                OnPropertyChanged(nameof(SelectedEmployeeLastLoginDisplay));
                OnPropertyChanged(nameof(ActionHint));
                OnPropertyChanged(nameof(ToggleActiveActionLabel));
                OnPropertyChanged(nameof(ToggleManagerRoleActionLabel));
                NotifyCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string StatusKind
    {
        get => _statusKind;
        private set => SetProperty(ref _statusKind, value);
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set
        {
            if (SetProperty(ref _currentPassword, value))
            {
                ChangePasswordCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewPassword
    {
        get => _newPassword;
        set
        {
            if (SetProperty(ref _newPassword, value))
            {
                ChangePasswordCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string FilterQuery
    {
        get => _filterQuery;
        set
        {
            if (SetProperty(ref _filterQuery, value))
            {
                EmployeesView.Refresh();
                if (SelectedEmployee is not null && !FilterEmployee(SelectedEmployee))
                {
                    SelectedEmployee = null;
                }

                OnPropertyChanged(nameof(VisibleEmployeesCount));
            }
        }
    }

    public bool CanManageEmployees
        => _authorizationService.HasPermission(_currentEmployee, EmployeePermission.ManageEmployees);

    public int TotalEmployees
    {
        get => _totalEmployees;
        private set => SetProperty(ref _totalEmployees, value);
    }

    public int ActiveEmployees
    {
        get => _activeEmployees;
        private set => SetProperty(ref _activeEmployees, value);
    }

    public int ManagersCount
    {
        get => _managersCount;
        private set => SetProperty(ref _managersCount, value);
    }

    public int LockedEmployees
    {
        get => _lockedEmployees;
        private set => SetProperty(ref _lockedEmployees, value);
    }

    public int GuideRequestId
    {
        get => _guideRequestId;
        private set => SetProperty(ref _guideRequestId, value);
    }

    public int VisibleEmployeesCount => EmployeesView.Cast<object>().Count();

    public bool HasSelectedEmployee => SelectedEmployee is not null;

    public string SelectedEmployeeDisplayName => SelectedEmployee?.FullName ?? "Працівника не вибрано";

    public string SelectedEmployeeRoleDisplay => SelectedEmployee?.RoleDisplay ?? "—";

    public string SelectedEmployeeStateDisplay => SelectedEmployee?.ActivityDisplay ?? "—";

    public string SelectedEmployeeLockoutDisplay => SelectedEmployee?.LockoutDisplay ?? "—";

    public string SelectedEmployeeLastLoginDisplay => SelectedEmployee?.LastLoginDisplay ?? "—";

    public string ActionHint
    {
        get
        {
            if (!CanManageEmployees)
            {
                return "У вашого облікового запису немає прав на керування працівниками.";
            }

            if (SelectedEmployee is null)
            {
                return "Оберіть працівника в таблиці зліва, щоб активувати дії.";
            }

            if (SelectedEmployee.Id == _currentEmployee.Id)
            {
                return "Для власного акаунта недоступно вимкнення активності.";
            }

            return "Використовуйте кнопки нижче для швидкого керування роллю та блокуванням.";
        }
    }

    public string ToggleActiveActionLabel
        => SelectedEmployee?.IsActive == true ? "Вимкнути працівника" : "Активувати працівника";

    public string ToggleManagerRoleActionLabel
    {
        get
        {
            if (SelectedEmployee is null)
            {
                return "Змінити роль менеджера";
            }

            return SelectedEmployee.RoleId switch
            {
                UserRole.Admin => "Роль адміністратора не змінюється",
                UserRole.Manager => "Зробити користувачем",
                _ => "Зробити менеджером"
            };
        }
    }

    public override Task RefreshAsync()
        => RefreshInternalAsync(setStatusMessage: true);

    private async Task RefreshInternalAsync(bool setStatusMessage)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var selectedEmployeeId = SelectedEmployee?.Id;
            var employees = await StaffVisibilityQuery.VisibleStaff(_dbContext)
                .OrderBy(item => item.FullName)
                .Select(item => new EmployeeRow(
                    item.Id,
                    item.FullName,
                    item.Account != null ? item.Account.Login : string.Empty,
                    item.RoleId,
                    item.Account != null && item.Account.IsActive,
                    item.Account != null ? item.Account.LockoutUntilUtc : null,
                    item.Account != null ? item.Account.LastLoginUtc : null))
                .ToListAsync();

            Employees.Clear();
            foreach (var employee in employees)
            {
                Employees.Add(employee);
            }

            UpdateStatistics();
            EmployeesView.Refresh();
            OnPropertyChanged(nameof(VisibleEmployeesCount));

            SelectedEmployee = selectedEmployeeId.HasValue
                ? Employees.FirstOrDefault(item => item.Id == selectedEmployeeId.Value)
                : null;

            if (setStatusMessage)
            {
                SetInfo($"Список працівників оновлено. Усього записів: {TotalEmployees}.");
            }
            MarkDataLoaded();
        }
        catch (Exception exception)
        {
            SetError($"Не вдалося оновити список працівників: {exception.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleActiveAsync()
    {
        if (!CanManageEmployees)
        {
            SetError("Недостатньо прав для керування працівниками.");
            return;
        }

        if (SelectedEmployee is null)
        {
            SetInfo("Оберіть працівника для зміни активності.");
            return;
        }

        var employee = await _dbContext.Employees.FirstOrDefaultAsync(item => item.Id == SelectedEmployee.Id);
        if (employee is null)
        {
            SetError("Працівника не знайдено.");
            return;
        }

        if (employee.Id == _currentEmployee.Id && employee.IsActive)
        {
            SetError("Неможливо вимкнути власний обліковий запис.");
            return;
        }

        employee.IsActive = !employee.IsActive;
        await _dbContext.SaveChangesAsync();

        SetSuccess(employee.IsActive
            ? $"Працівника \"{employee.FullName}\" активовано."
            : $"Працівника \"{employee.FullName}\" вимкнено.");

        await RefreshInternalAsync(setStatusMessage: false);
    }

    private async Task ToggleManagerRoleAsync()
    {
        if (!CanManageEmployees)
        {
            SetError("Недостатньо прав для керування ролями.");
            return;
        }

        if (SelectedEmployee is null)
        {
            SetInfo("Оберіть працівника для зміни ролі.");
            return;
        }

        var employee = await _dbContext.Employees.FirstOrDefaultAsync(item => item.Id == SelectedEmployee.Id);
        if (employee is null)
        {
            SetError("Працівника не знайдено.");
            return;
        }

        if (employee.RoleId == UserRole.Admin)
        {
            SetInfo("Роль адміністратора не змінюється цим інструментом.");
            return;
        }

        employee.RoleId = employee.RoleId == UserRole.Manager ? UserRole.User : UserRole.Manager;
        await _dbContext.SaveChangesAsync();

        SetSuccess(employee.RoleId == UserRole.Manager
            ? $"Працівника \"{employee.FullName}\" призначено менеджером."
            : $"Працівника \"{employee.FullName}\" переведено в роль користувача.");

        await RefreshInternalAsync(setStatusMessage: false);
    }

    private async Task UnlockAsync()
    {
        if (!CanManageEmployees)
        {
            SetError("Недостатньо прав для розблокування.");
            return;
        }

        if (SelectedEmployee is null)
        {
            SetInfo("Оберіть працівника для розблокування.");
            return;
        }

        var employee = await _dbContext.Employees.FirstOrDefaultAsync(item => item.Id == SelectedEmployee.Id);
        if (employee is null)
        {
            SetError("Працівника не знайдено.");
            return;
        }

        var wasLocked = (employee.LockoutUntilUtc.HasValue && employee.LockoutUntilUtc > DateTime.UtcNow) ||
                        employee.FailedLoginAttempts > 0;

        employee.LockoutUntilUtc = null;
        employee.FailedLoginAttempts = 0;
        await _dbContext.SaveChangesAsync();

        SetSuccess(wasLocked
            ? $"Працівника \"{employee.FullName}\" розблоковано."
            : $"Для \"{employee.FullName}\" очищено лічильник невдалих входів.");

        await RefreshInternalAsync(setStatusMessage: false);
    }

    private async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            SetInfo("Заповніть поля поточного і нового пароля.");
            return;
        }

        var result = await _authService.ChangePasswordAsync(_currentEmployee.AccountId, CurrentPassword, NewPassword);
        if (result.Success)
        {
            SetSuccess(result.Message);
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            RequestPasswordFieldsClear?.Invoke();
            return;
        }

        SetError(result.Message);
    }

    private void UpdateStatistics()
    {
        TotalEmployees = Employees.Count;
        ActiveEmployees = Employees.Count(item => item.IsActive);
        ManagersCount = Employees.Count(item => item.RoleId == UserRole.Manager);
        LockedEmployees = Employees.Count(item => item.IsLocked);
    }

    private bool FilterEmployee(object item)
    {
        if (item is not EmployeeRow row)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FilterQuery))
        {
            return true;
        }

        var query = FilterQuery.Trim();
        return row.FullName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               row.Login.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               row.RoleDisplay.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool CanExecuteRefresh() => !IsLoading;

    private bool CanExecuteEmployeeAction()
        => !IsLoading && CanManageEmployees && SelectedEmployee is not null;

    private bool CanExecuteChangePassword()
        => !IsLoading &&
           !string.IsNullOrWhiteSpace(CurrentPassword) &&
           !string.IsNullOrWhiteSpace(NewPassword);

    private void NotifyCommandStates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
        ToggleManagerRoleCommand.NotifyCanExecuteChanged();
        UnlockCommand.NotifyCanExecuteChanged();
        ChangePasswordCommand.NotifyCanExecuteChanged();
    }

    private void SetInfo(string message)
    {
        StatusKind = "Info";
        StatusMessage = message;
    }

    private void SetSuccess(string message)
    {
        StatusKind = "Success";
        StatusMessage = message;
    }

    private void SetError(string message)
    {
        StatusKind = "Error";
        StatusMessage = message;
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        RequestPasswordFieldsClear?.Invoke();
    }

    public sealed record EmployeeRow(
        int Id,
        string FullName,
        string Login,
        UserRole RoleId,
        bool IsActive,
        DateTime? LockoutUntilUtc,
        DateTime? LastLoginUtc)
    {
        public bool IsLocked => LockoutUntilUtc.HasValue && LockoutUntilUtc.Value > DateTime.UtcNow;

        public string RoleDisplay => RoleId switch
        {
            UserRole.Admin => "Адміністратор",
            UserRole.Manager => "Менеджер",
            UserRole.User => "Користувач",
            _ => RoleId.ToString()
        };

        public string ActivityDisplay => IsActive ? "Активний" : "Вимкнений";

        public string LockoutDisplay
            => LockoutUntilUtc.HasValue ? $"{LockoutUntilUtc:dd.MM.yyyy HH:mm} UTC" : "Немає";

        public string LastLoginDisplay
            => LastLoginUtc.HasValue ? $"{LastLoginUtc:dd.MM.yyyy HH:mm} UTC" : "Немає даних";
    }
}






