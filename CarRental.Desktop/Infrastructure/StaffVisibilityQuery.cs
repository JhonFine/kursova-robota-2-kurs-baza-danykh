using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Infrastructure;

public static class StaffVisibilityQuery
{
    public static IQueryable<Employee> VisibleStaff(RentalDbContext dbContext)
    {
        var clientAccountIds = dbContext.Clients
            .AsNoTracking()
            .Where(item => item.AccountId.HasValue)
            .Select(item => item.AccountId!.Value);

        return dbContext.Employees
            .AsNoTracking()
            .Where(item => !clientAccountIds.Contains(item.AccountId));
    }
}
