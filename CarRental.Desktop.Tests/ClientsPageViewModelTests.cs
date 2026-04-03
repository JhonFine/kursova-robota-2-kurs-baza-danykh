using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Services.Documents;
using CarRental.Desktop.Services.Payments;
using CarRental.Desktop.Services.Rentals;
using CarRental.Desktop.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class ClientsPageViewModelTests
{
    [Fact]
    public void ClientDocumentStorage_ShouldSaveResolveAndDeleteManagedDocument()
    {
        var storage = new ClientDocumentStorage();
        var sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);

        string storedPath = string.Empty;
        try
        {
            storedPath = storage.SaveDocumentCopy(sourcePath, 42, "PASSPORT");

            storedPath.Should().StartWith("client-documents/");
            storage.TryResolvePath(storedPath, out var fullPath).Should().BeTrue();
            fullPath.Should().NotBeNullOrWhiteSpace();
            File.Exists(fullPath).Should().BeTrue();

            storage.TryDeleteManagedDocument(storedPath).Should().BeTrue();
            File.Exists(fullPath).Should().BeFalse();
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(storedPath))
            {
                storage.TryDeleteManagedDocument(storedPath);
            }

            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
    }

    [Fact]
    public async Task SaveClientCommand_ShouldCreateLocalClientProfile_AndPersistManagedPassportAttachment()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedStaffAsync(dbContext);
        var storage = new ClientDocumentStorage();
        var viewModel = CreateClientsViewModel(dbContext, storage, currentEmployee);
        var sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(sourcePath, [10, 20, 30, 40]);

        string? storedPath = null;
        try
        {
            viewModel.Editor.FullName = "Олена Тест";
            viewModel.Editor.Phone = "+380671112233";
            viewModel.Editor.PassportData = "AA123456";
            viewModel.SelectPassportSourceFile(sourcePath);

            await viewModel.SaveClientCommand.ExecuteAsync(null);

            var client = await dbContext.Clients
                .Include(item => item.Documents)
                .SingleAsync(item => item.Phone == "+380671112233");

            client.AccountId.Should().BeNull();
            client.FullName.Should().Be("Олена Тест");
            client.Documents.Should().ContainSingle(item => item.DocumentTypeCode == "PASSPORT");
            storedPath = client.Documents.Single(item => item.DocumentTypeCode == "PASSPORT").StoredPath;
            storedPath.Should().StartWith("client-documents/");
            storage.TryResolvePath(storedPath, out var fullPath).Should().BeTrue();
            File.Exists(fullPath).Should().BeTrue();

            viewModel.SelectedClient.Should().NotBeNull();
            viewModel.SelectedClient!.Id.Should().Be(client.Id);
            viewModel.ShowClientDetails.Should().BeTrue();
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(storedPath))
            {
                storage.TryDeleteManagedDocument(storedPath);
            }

            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
    }

    [Fact]
    public async Task SaveClientCommand_ShouldFocusExistingClient_WhenPhoneDuplicates()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedStaffAsync(dbContext);
        var existingClient = new Client
        {
            FullName = "Існуючий клієнт",
            Phone = "+380671112233"
        };
        dbContext.Clients.Add(existingClient);
        await dbContext.SaveChangesAsync();

        var viewModel = CreateClientsViewModel(dbContext, new ClientDocumentStorage(), currentEmployee);
        viewModel.Editor.FullName = "Новий дубль";
        viewModel.Editor.Phone = "38067 111 22 33";

        await viewModel.SaveClientCommand.ExecuteAsync(null);

        viewModel.SelectedClient.Should().NotBeNull();
        viewModel.SelectedClient!.Id.Should().Be(existingClient.Id);
        (await dbContext.Clients.CountAsync(item => item.Phone == "+380671112233")).Should().Be(1);
    }

    [Fact]
    public async Task RefreshAsync_ShouldSearchByPassport_AndOfferQuickCreateForUnknownPhone()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedStaffAsync(dbContext);

        dbContext.Clients.Add(new Client
        {
            FullName = "Паспортний клієнт",
            Phone = "+380631234567",
            PassportData = "PP-778899"
        });
        await dbContext.SaveChangesAsync();

        var viewModel = CreateClientsViewModel(dbContext, new ClientDocumentStorage(), currentEmployee);
        viewModel.SearchText = "PP-778899";
        await viewModel.RefreshAsync();

        viewModel.Clients.Should().ContainSingle();
        viewModel.Clients[0].PassportData.Should().Be("PP-778899");

        viewModel.SearchText = "+380991234567";
        await viewModel.RefreshAsync();

        viewModel.Clients.Should().BeEmpty();
        viewModel.ShowQuickCreateFromSearch.Should().BeTrue();
    }

    [Fact]
    public async Task PrepareForClientAsync_ShouldSelectPreferredClient()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedStaffAsync(dbContext);

        var firstClient = new Client
        {
            FullName = "Перший клієнт",
            Phone = "+380631111111",
            DriverLicense = "DL-001"
        };
        var secondClient = new Client
        {
            FullName = "Другий клієнт",
            Phone = "+380632222222",
            DriverLicense = "DL-002"
        };
        dbContext.Clients.AddRange(firstClient, secondClient);
        await dbContext.SaveChangesAsync();

        var viewModel = new RentalsPageViewModel(
            dbContext,
            new StubRentalService(),
            new StubDocumentGenerator(),
            new StubPrintService(),
            new StubPaymentService(),
            new StubAuthorizationService(),
            new PageRefreshCoordinator(_ => Task.CompletedTask),
            currentEmployee);

        await viewModel.PrepareForClientAsync(secondClient.Id);

        viewModel.IsCreateRentalDialogOpen.Should().BeTrue();
        viewModel.CreateDraft.SelectedClient.Should().NotBeNull();
        viewModel.CreateDraft.SelectedClient!.Id.Should().Be(secondClient.Id);
    }

    private static ClientsPageViewModel CreateClientsViewModel(
        CarRental.Desktop.Data.RentalDbContext dbContext,
        IClientDocumentStorage clientDocumentStorage,
        Employee currentEmployee)
    {
        return new ClientsPageViewModel(
            dbContext,
            new StubAuthorizationService(),
            new PageRefreshCoordinator(_ => Task.CompletedTask),
            clientDocumentStorage,
            currentEmployee);
    }

    private static async Task<Employee> SeedStaffAsync(CarRental.Desktop.Data.RentalDbContext dbContext)
    {
        var account = new Account
        {
            Login = $"manager-{Guid.NewGuid():N}",
            PasswordHash = "x",
            IsActive = true
        };
        var employee = new Employee
        {
            FullName = "Test Manager",
            RoleId = UserRole.Manager,
            Account = account
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        return employee;
    }

    private sealed class StubAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(Employee employee, EmployeePermission permission) => true;
    }

    private sealed class StubRentalService : IRentalService
    {
        public Task<bool> HasDateConflictAsync(int vehicleId, DateTime startDate, DateTime endDate, int? excludeRentalId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<CreateRentalResult> CreateRentalAsync(CreateRentalRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CreateRentalResult> CreateRentalWithPaymentAsync(CreateRentalWithPaymentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CloseRentalResult> CloseRentalAsync(CloseRentalRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CancelRentalResult> CancelRentalAsync(CancelRentalRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RescheduleRentalResult> RescheduleRentalAsync(RescheduleRentalRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SettleRentalBalanceResult> SettleRentalBalanceAsync(SettleRentalBalanceRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PickupInspectionResult> CompletePickupInspectionAsync(PickupInspectionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RefreshStatusesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubDocumentGenerator : IDocumentGenerator
    {
        public Task<GeneratedContractFiles> GenerateRentalContractAsync(ContractData data, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubPrintService : IPrintService
    {
        public bool TryPrint(string filePath, out string message)
        {
            message = "N/A";
            return false;
        }
    }

    private sealed class StubPaymentService : IPaymentService
    {
        public Task<PaymentResult> AddPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Payment>> GetRentalPaymentsAsync(int rentalId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Payment>>([]);

        public Task<decimal> GetRentalBalanceAsync(int rentalId, CancellationToken cancellationToken = default)
            => Task.FromResult(0m);
    }
}
