using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CommunityToolkit.Mvvm.Input;

namespace CarRental.Desktop.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private string _lastName = string.Empty;
    private string _firstName = string.Empty;
    private string _middleName = string.Empty;
    private string _login = string.Empty;
    private string _phone = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _isRegistrationMode;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => !IsBusy);
    }

    public event Action<bool>? RequestClose;

    public IAsyncRelayCommand LoginCommand { get; }

    public IAsyncRelayCommand RegisterCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public Employee? AuthenticatedEmployee { get; private set; }

    public string LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value))
            {
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
            {
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string MiddleName
    {
        get => _middleName;
        set
        {
            if (SetProperty(ref _middleName, value))
            {
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Login
    {
        get => _login;
        set
        {
            if (SetProperty(ref _login, value))
            {
                LoginCommand.NotifyCanExecuteChanged();
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set
        {
            if (SetProperty(ref _phone, value))
            {
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                LoginCommand.NotifyCanExecuteChanged();
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
            {
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoginCommand.NotifyCanExecuteChanged();
                RegisterCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsRegistrationMode
    {
        get => _isRegistrationMode;
        set
        {
            if (SetProperty(ref _isRegistrationMode, value))
            {
                OnPropertyChanged(nameof(RegisterButtonText));
                LoginCommand.NotifyCanExecuteChanged();
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string RegisterButtonText => IsRegistrationMode ? "Підтвердити" : "Зареєструватися";

    private void Cancel()
    {
        ErrorMessage = string.Empty;

        if (IsRegistrationMode)
        {
            IsRegistrationMode = false;
            return;
        }

        RequestClose?.Invoke(false);
    }

    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (IsRegistrationMode)
        {
            await RegisterAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введіть логін і пароль.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.AuthenticateAsync(Login, Password);
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            AuthenticatedEmployee = BuildSessionEmployee(result.Account, result.Employee, result.Client, result.Role);
            if (AuthenticatedEmployee is null)
            {
                ErrorMessage = "Не вдалося підготувати профіль користувача для сесії.";
                return;
            }

            RequestClose?.Invoke(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;

        if (!IsRegistrationMode)
        {
            IsRegistrationMode = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(LastName) ||
            string.IsNullOrWhiteSpace(FirstName) ||
            string.IsNullOrWhiteSpace(MiddleName))
        {
            ErrorMessage = "Заповніть прізвище, ім'я та по батькові.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Login))
        {
            ErrorMessage = "Вкажіть логін.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = "Вкажіть номер телефону.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 8)
        {
            ErrorMessage = "Пароль має містити щонайменше 8 символів.";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Паролі не збігаються.";
            return;
        }

        var fullName = $"{LastName.Trim()} {FirstName.Trim()} {MiddleName.Trim()}";
        IsBusy = true;
        try
        {
            var result = await _authService.RegisterAsync(fullName, Login, Phone, Password);
            if (!result.Success)
            {
                ErrorMessage = result.Message;
                return;
            }

            AuthenticatedEmployee = BuildSessionEmployee(result.Account, result.Employee, result.Client, result.Role);
            if (AuthenticatedEmployee is null)
            {
                ErrorMessage = "Не вдалося підготувати профіль користувача для сесії.";
                return;
            }

            RequestClose?.Invoke(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() => !IsBusy && !IsRegistrationMode;

    private static Employee? BuildSessionEmployee(Account? account, Employee? employee, Client? client, UserRole role)
    {
        if (employee is not null)
        {
            return employee;
        }

        if (account is null)
        {
            return null;
        }

        var fullName = !string.IsNullOrWhiteSpace(client?.FullName)
            ? client.FullName
            : account.Login;

        return new Employee
        {
            Account = account,
            AccountId = account.Id,
            FullName = fullName,
            RoleId = role
        };
    }
}

