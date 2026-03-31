namespace CarRental.WebApi.Models;

public sealed class EmployeeRoleLookup
{
    public UserRole Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
