using CarRental.Desktop.Models;

namespace CarRental.Desktop.Services.Auth;

public interface IAuthorizationService
{
    bool HasPermission(Employee employee, EmployeePermission permission);
}

