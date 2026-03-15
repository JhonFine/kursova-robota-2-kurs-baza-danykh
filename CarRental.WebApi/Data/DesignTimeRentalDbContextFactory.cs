using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarRental.WebApi.Data;

public sealed class DesignTimeRentalDbContextFactory : IDesignTimeDbContextFactory<RentalDbContext>
{
    public RentalDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CAR_RENTAL_POSTGRES_CONNECTION") ??
            "Host=localhost;Port=5432;Database=car_rental_dev;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<RentalDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RentalDbContext(options);
    }
}
