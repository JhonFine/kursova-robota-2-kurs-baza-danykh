namespace CarRental.Desktop.Models;

[Flags]
public enum EmployeePermission
{
    None = 0,
    ManageRentals = 1 << 0,
    ManagePayments = 1 << 1,
    ManageClients = 1 << 2,
    ManageFleet = 1 << 3,
    ManagePricing = 1 << 4,
    ManageEmployees = 1 << 5,
    ManageMaintenance = 1 << 6,
    ManageDamages = 1 << 7,
    ExportReports = 1 << 8,
    GenerateDocuments = 1 << 9,
    DeleteRecords = 1 << 10,
    All = int.MaxValue
}

