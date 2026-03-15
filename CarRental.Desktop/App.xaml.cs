using CarRental.Desktop.Data;
using CarRental.Desktop.Services.Analytics;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Services.Damages;
using CarRental.Desktop.Services.Documents;
using CarRental.Desktop.Services.Logging;
using CarRental.Desktop.Services.Maintenance;
using CarRental.Desktop.Services.Payments;
using CarRental.Desktop.Services.Rentals;
using CarRental.Desktop.Models;
using CarRental.Desktop.ViewModels;
using CarRental.Desktop.Views;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace CarRental.Desktop;

public partial class App : Application
{
    private readonly List<RentalDbContext> _openDbContexts = [];
    private IAppLogger? _logger;
    private DatabaseConnectionOptions? _databaseConnectionOptions;

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureLocalization();
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var appDataDir = GetApplicationDataDirectory();
            var contractsDir = Path.Combine(appDataDir, "Contracts");
            var exportsDir = Path.Combine(appDataDir, "Exports");
            var logsDir = Path.Combine(appDataDir, "Logs");
            _databaseConnectionOptions = DatabaseConnectionOptions.Load();
            Directory.CreateDirectory(appDataDir);
            Directory.CreateDirectory(contractsDir);
            Directory.CreateDirectory(exportsDir);
            Directory.CreateDirectory(logsDir);

            _logger = new FileAppLogger(logsDir);
            RegisterGlobalExceptionHandlers();
            _logger.Info("Запуск застосунку.");

            using (var bootstrapDbContext = CreateDbContext(_databaseConnectionOptions, trackLifetime: false))
            {
                EnsurePostgresSchemaReady(bootstrapDbContext);
                var seededCredentials = DatabaseInitializer.Seed(bootstrapDbContext);
                ShowGeneratedSeedCredentials(seededCredentials);
            }

            RunSessionLoop(_databaseConnectionOptions, contractsDir, exportsDir);
        }
        catch (Exception exception)
        {
            _logger?.Error("Помилка запуску.", exception);
            MessageBox.Show(
                $"Помилка запуску: {exception.Message}",
                "Помилка запуску",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void RunSessionLoop(
        DatabaseConnectionOptions databaseConnectionOptions,
        string contractsDir,
        string exportsDir)
    {
        while (true)
        {
            var authenticatedEmployee = ShowLoginDialog(databaseConnectionOptions);
            if (authenticatedEmployee is null)
            {
                _logger?.Info("Вхід скасовано або неуспішний.");
                Shutdown();
                return;
            }

            var session = CreateMainSession(
                databaseConnectionOptions,
                contractsDir,
                exportsDir,
                authenticatedEmployee);

            var logoutRequested = false;
            void HandleLogoutRequested()
            {
                logoutRequested = true;
                session.Window.Close();
            }

            session.ViewModel.LogoutRequested += HandleLogoutRequested;
            try
            {
                MainWindow = session.Window;
                session.Window.ShowDialog();
            }
            finally
            {
                session.ViewModel.LogoutRequested -= HandleLogoutRequested;
                MainWindow = null;

                foreach (var dbContext in session.DbContexts)
                {
                    dbContext.Dispose();
                }
            }

            if (!logoutRequested)
            {
                Shutdown();
                return;
            }
        }
    }

    private Employee? ShowLoginDialog(DatabaseConnectionOptions databaseConnectionOptions)
    {
        using var loginDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var loginAuthService = new AuthService(loginDbContext);
        var loginViewModel = new LoginViewModel(loginAuthService);
        var loginWindow = new LoginWindow
        {
            DataContext = loginViewModel
        };

        var loginResult = loginWindow.ShowDialog();
        if (loginResult != true || loginViewModel.AuthenticatedEmployee is null)
        {
            return null;
        }

        return loginViewModel.AuthenticatedEmployee;
    }

    private MainSession CreateMainSession(
        DatabaseConnectionOptions databaseConnectionOptions,
        string contractsDir,
        string exportsDir,
        Employee authenticatedEmployee)
    {
        var authorizationService = new AuthorizationService();

        var fleetDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var clientsDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var rentalsDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var prokatDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var reportsDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var maintenanceDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var damagesDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);
        var adminDbContext = CreateDbContext(databaseConnectionOptions, trackLifetime: false);

        var sessionDbContexts = new List<RentalDbContext>
        {
            fleetDbContext,
            clientsDbContext,
            rentalsDbContext,
            prokatDbContext,
            reportsDbContext,
            maintenanceDbContext,
            damagesDbContext,
            adminDbContext
        };

        var rentalsContractNumberService = new ContractNumberService(rentalsDbContext);
        var rentalsService = new RentalService(rentalsDbContext, rentalsContractNumberService);
        var paymentsService = new PaymentService(rentalsDbContext);
        var refreshCoordinator = new PageRefreshCoordinator(ct => rentalsService.RefreshStatusesAsync(ct));

        var prokatContractNumberService = new ContractNumberService(prokatDbContext);
        var prokatRentalService = new RentalService(prokatDbContext, prokatContractNumberService);

        var maintenanceService = new MaintenanceService(maintenanceDbContext);
        var damageService = new DamageService(damagesDbContext);
        var documentGenerator = new SimpleContractGenerator(contractsDir);
        var printService = new ShellPrintService();
        var exportService = new AnalyticsExportService(reportsDbContext, exportsDir);
        var adminAuthService = new AuthService(adminDbContext);

        var fleetPageViewModel = new FleetPageViewModel(fleetDbContext, authorizationService, refreshCoordinator, authenticatedEmployee);
        var clientsPageViewModel = new ClientsPageViewModel(clientsDbContext, authorizationService, refreshCoordinator, authenticatedEmployee);
        var rentalsPageViewModel = new RentalsPageViewModel(
            rentalsDbContext,
            rentalsService,
            documentGenerator,
            printService,
            paymentsService,
            authorizationService,
            refreshCoordinator,
            authenticatedEmployee);
        var prokatPageViewModel = new ProkatPageViewModel(
            prokatDbContext,
            prokatRentalService,
            refreshCoordinator,
            authenticatedEmployee);
        var userRentalsPageViewModel = new UserRentalsPageViewModel(
            prokatDbContext,
            prokatRentalService,
            refreshCoordinator,
            authenticatedEmployee);
        var reportsPageViewModel = new ReportsPageViewModel(reportsDbContext, exportService);
        var maintenancePageViewModel = new MaintenancePageViewModel(maintenanceDbContext, maintenanceService, refreshCoordinator);
        var damagesPageViewModel = new DamagesPageViewModel(damagesDbContext, damageService, refreshCoordinator);
        var adminPageViewModel = new AdminPageViewModel(
            adminDbContext,
            adminAuthService,
            authorizationService,
            authenticatedEmployee);

        var mainViewModel = new MainViewModel(
            authenticatedEmployee,
            authorizationService,
            refreshCoordinator,
            fleetPageViewModel,
            clientsPageViewModel,
            rentalsPageViewModel,
            prokatPageViewModel,
            userRentalsPageViewModel,
            reportsPageViewModel,
            maintenancePageViewModel,
            damagesPageViewModel,
            adminPageViewModel);

        userRentalsPageViewModel.OpenCatalogRequestedAsync = mainViewModel.NavigateToProkatAsync;
        userRentalsPageViewModel.RebookRequestedAsync = mainViewModel.NavigateToRebookingAsync;

        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };

        return new MainSession(mainWindow, mainViewModel, sessionDbContexts);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        foreach (var dbContext in _openDbContexts)
        {
            dbContext.Dispose();
        }

        _openDbContexts.Clear();

        _logger?.Info("Завершення роботи застосунку.");
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("Необроблений виняток інтерфейсу.", e.Exception);
        MessageBox.Show(
            "Сталася неочікувана помилка. Деталі записано у файл журналу AppData/Logs.",
            "Помилка застосунку",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;

        if (MainWindow is null || !MainWindow.IsLoaded)
        {
            Shutdown(-1);
        }
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.Error("Необроблений доменний виняток.", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Error("Необроблений виняток у фоновому завданні.", e.Exception);
        e.SetObserved();
    }

    private static void ConfigureLocalization()
    {
        var culture = CultureInfo.GetCultureInfo("uk-UA");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    private static string GetApplicationDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return Path.Combine(localAppData, "CarRentalSystem");
    }

    private RentalDbContext CreateDbContext(DatabaseConnectionOptions databaseConnectionOptions, bool trackLifetime = true)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RentalDbContext>();
        optionsBuilder.UseNpgsql(databaseConnectionOptions.PostgresConnectionString);

        var options = optionsBuilder.Options;

        var dbContext = new RentalDbContext(options);
        if (trackLifetime)
        {
            _openDbContexts.Add(dbContext);
        }

        return dbContext;
    }

    private static void EnsurePostgresSchemaReady(RentalDbContext dbContext)
    {
        try
        {
            dbContext.Database.ExecuteSqlRaw("SELECT 1 FROM \"Employees\" LIMIT 1;");
            dbContext.Database.ExecuteSqlRaw("SELECT 1 FROM \"Clients\" LIMIT 1;");
            dbContext.Database.ExecuteSqlRaw("SELECT 1 FROM \"Vehicles\" LIMIT 1;");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "PostgreSQL schema is not initialized. Run RunWeb.ps1 or FactoryReset.ps1 to apply WebApi migrations, then retry desktop startup.",
                exception);
        }
    }

    private void ShowGeneratedSeedCredentials(DatabaseInitializer.SeedCredentials? seededCredentials)
    {
        if (seededCredentials is null)
        {
            return;
        }

        var generatedCredentials = new List<string>();
        if (seededCredentials.AdminPasswordGenerated)
        {
            generatedCredentials.Add($"{seededCredentials.AdminLogin} / {seededCredentials.AdminPassword}");
        }

        if (seededCredentials.ManagerPasswordGenerated)
        {
            generatedCredentials.Add($"{seededCredentials.ManagerLogin} / {seededCredentials.ManagerPassword}");
        }

        if (generatedCredentials.Count == 0)
        {
            _logger?.Info("Початкові облікові записи створено зі стандартними паролями або паролями з environment variables.");
            return;
        }

        _logger?.Info("Початкові облікові записи створено з генерацією тимчасових паролів.");
        MessageBox.Show(
            "Створено початкові облікові записи. Тимчасові паролі:\n" +
            string.Join(Environment.NewLine, generatedCredentials) +
            "\n\nЗмініть паролі в модулі адміністрування.",
            "Початкові облікові дані",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private sealed record MainSession(
        MainWindow Window,
        MainViewModel ViewModel,
        IReadOnlyList<RentalDbContext> DbContexts);
}
