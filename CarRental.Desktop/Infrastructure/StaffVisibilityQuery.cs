using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Infrastructure;

public static class StaffVisibilityQuery
{
    public static IQueryable<Employee> VisibleStaff(RentalDbContext dbContext)
        => dbContext.Employees.AsNoTracking();
}

