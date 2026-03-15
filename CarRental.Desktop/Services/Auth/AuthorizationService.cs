using CarRental.Desktop.Models;

namespace CarRental.Desktop.Services.Auth;

public sealed class AuthorizationService : IAuthorizationService
{
    private static readonly EmployeePermission UserPermissions =
        EmployeePermission.ManageRentals |
        EmployeePermission.GenerateDocuments;

    private static readonly EmployeePermission ManagerPermissions =
        UserPermissions |
        EmployeePermission.ManagePayments |
        EmployeePermission.ManageClients |
        EmployeePermission.ManageFleet |
        EmployeePermission.ManageMaintenance |
        EmployeePermission.ManageDamages |
        EmployeePermission.GenerateDocuments |
        EmployeePermission.ExportReports;

    public bool HasPermission(Employee employee, EmployeePermission permission)
    {
        var permissions = employee.Role switch
        {
            UserRole.Admin => EmployeePermission.All,
            UserRole.Manager => ManagerPermissions,
            UserRole.User => UserPermissions,
            _ => EmployeePermission.None
        };

        return (permissions & permission) == permission;
    }
}
