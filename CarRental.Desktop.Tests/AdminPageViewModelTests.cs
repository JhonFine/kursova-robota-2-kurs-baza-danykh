using CarRental.Desktop.Infrastructure;
using CarRental.Desktop.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class AdminPageViewModelTests
{
    [Fact]
    public async Task VisibleStaff_ShouldReturnAllEmployees()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var adminAccount = new Account
        {
            Login = "admin.staff",
            PasswordHash = "x",
            IsActive = true
        };
        var managerAccount = new Account
        {
            Login = "manager.staff",
            PasswordHash = "x",
            IsActive = true
        };

        dbContext.Accounts.AddRange(adminAccount, managerAccount);
        dbContext.Employees.AddRange(
            new Employee
            {
                FullName = "Admin Staff",
                RoleId = UserRole.Admin,
                Account = adminAccount
            },
            new Employee
            {
                FullName = "Manager Staff",
                RoleId = UserRole.Manager,
                Account = managerAccount
            });

        await dbContext.SaveChangesAsync();

        var employeeNames = await StaffVisibilityQuery.VisibleStaff(dbContext)
            .OrderBy(item => item.FullName)
            .Select(item => item.FullName)
            .ToListAsync();

        employeeNames.Should().Equal("Admin Staff", "Manager Staff");
    }
}
