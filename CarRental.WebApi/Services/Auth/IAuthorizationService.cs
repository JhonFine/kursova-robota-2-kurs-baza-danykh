using CarRental.WebApi.Models;

namespace CarRental.WebApi.Services.Auth;

public interface IAuthorizationService
{
    bool HasPermission(Employee employee, EmployeePermission permission);
}

