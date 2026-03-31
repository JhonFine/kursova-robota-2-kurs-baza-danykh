namespace CarRental.WebApi.Models;

public sealed class DamageStatusLookup
{
    public DamageStatus Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Damage> Damages { get; set; } = new List<Damage>();
}
