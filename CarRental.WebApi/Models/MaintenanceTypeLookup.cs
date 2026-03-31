namespace CarRental.WebApi.Models;

public sealed class MaintenanceTypeLookup
{
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<MaintenanceRecord> Records { get; set; } = new List<MaintenanceRecord>();
}
