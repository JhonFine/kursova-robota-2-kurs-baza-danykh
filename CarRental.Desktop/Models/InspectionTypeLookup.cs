namespace CarRental.Desktop.Models;

public sealed class InspectionTypeLookup
{
    public RentalInspectionType Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<RentalInspection> Inspections { get; set; } = new List<RentalInspection>();
}
