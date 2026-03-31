using CarRental.Desktop.Infrastructure;
using CarRental.Desktop.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class AdminPageViewModelTests
{
    [Fact]
    public async Task VisibleStaff_ShouldExcludeClientCompatibilityEmployeesFromAdminList()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var adminAccount = new Account
        {
            Login = "admin.staff",
            PasswordHash = "x",
            IsActive = true
        };
        var admin = new Employee
        {
            FullName = "Admin Staff",
            Role = UserRole.Admin,
            IsActive = true
        };
        var portalAccount = new Account
        {
            Login = "portal.user",
            PasswordHash = "x",
            IsActive = true
        };
        var portalClient = new Client
        {
            FullName = "Portal User",
            Phone = "+380501112233",
            PassportData = "PP2",
            DriverLicense = "DL2",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5)
        };
        var compatibilityEmployee = new Employee
        {
            FullName = "Portal User",
            Role = UserRole.User,
            IsActive = true
        };

        dbContext.Accounts.Add(adminAccount);
        dbContext.Accounts.Add(portalAccount);
        await dbContext.SaveChangesAsync();

        admin.AccountId = adminAccount.Id;
        portalClient.AccountId = portalAccount.Id;
        compatibilityEmployee.AccountId = portalAccount.Id;

        dbContext.Employees.Add(admin);
        dbContext.Clients.Add(portalClient);
        dbContext.Employees.Add(compatibilityEmployee);
        await dbContext.SaveChangesAsync();

        var employeeNames = await StaffVisibilityQuery.VisibleStaff(dbContext)
            .OrderBy(item => item.FullName)
            .Select(item => item.FullName)
            .ToListAsync();

        employeeNames.Should().Contain("Admin Staff");
        employeeNames.Should().NotContain("Portal User");
    }
}
