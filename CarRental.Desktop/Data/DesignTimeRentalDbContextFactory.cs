using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarRental.Desktop.Data;

public sealed class DesignTimeRentalDbContextFactory : IDesignTimeDbContextFactory<RentalDbContext>
{
    public RentalDbContext CreateDbContext(string[] args)
    {
        var connectionOptions = DatabaseConnectionOptions.Load();
        var options = new DbContextOptionsBuilder<RentalDbContext>()
            .UseNpgsql(connectionOptions.PostgresConnectionString)
            .Options;

        return new RentalDbContext(options);
    }
}
