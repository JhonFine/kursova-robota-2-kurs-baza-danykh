using System.Runtime.CompilerServices;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.ViewModels;
using FluentAssertions;

namespace CarRental.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void CanSeeAdmin_ShouldBeFalse_ForManager()
    {
        var viewModel = CreateViewModel(UserRole.Manager);

        viewModel.CanSeeAdmin.Should().BeFalse();
    }

    [Fact]
    public void CanSeeAdmin_ShouldBeTrue_ForAdmin()
    {
        var viewModel = CreateViewModel(UserRole.Admin);

        viewModel.CanSeeAdmin.Should().BeTrue();
    }

    private static MainViewModel CreateViewModel(UserRole role)
    {
        var currentEmployee = new Employee
        {
            FullName = "Test Employee",
            Role = role,
            IsActive = true
        };

        return new MainViewModel(
            currentEmployee,
            new StubAuthorizationService(),
            new PageRefreshCoordinator(_ => Task.CompletedTask),
            CreatePage<FleetPageViewModel>(),
            CreatePage<ClientsPageViewModel>(),
            CreatePage<RentalsPageViewModel>(),
            CreatePage<ProkatPageViewModel>(),
            CreatePage<UserRentalsPageViewModel>(),
            CreatePage<ReportsPageViewModel>(),
            CreatePage<MaintenancePageViewModel>(),
            CreatePage<DamagesPageViewModel>(),
            CreatePage<AdminPageViewModel>());
    }

    private static TPage CreatePage<TPage>()
        where TPage : class
    {
        return (TPage)RuntimeHelpers.GetUninitializedObject(typeof(TPage));
    }

    private sealed class StubAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(Employee employee, EmployeePermission permission) => true;
    }
}
