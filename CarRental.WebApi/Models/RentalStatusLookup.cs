namespace CarRental.WebApi.Models;

public sealed class RentalStatusLookup
{
    public RentalStatus Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();
}
