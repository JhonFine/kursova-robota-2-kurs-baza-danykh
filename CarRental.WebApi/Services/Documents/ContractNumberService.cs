using CarRental.WebApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Globalization;

namespace CarRental.WebApi.Services.Documents;

public sealed class ContractNumberService(RentalDbContext dbContext) : IContractNumberService
{
    public async Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "ContractSequences" ("Year", "LastNumber") VALUES ({year}, 0) ON CONFLICT("Year") DO NOTHING;""",
            cancellationToken);

        var nextNumber = await ExecuteScalarIntAsync(
            """UPDATE "ContractSequences" SET "LastNumber" = "LastNumber" + 1 WHERE "Year" = @year RETURNING "LastNumber";""",
            year,
            cancellationToken);

        return $"CR-{year}-{nextNumber:000000}";
    }

    private async Task<int> ExecuteScalarIntAsync(string sql, int year, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

            var yearParameter = command.CreateParameter();
            yearParameter.ParameterName = "@year";
            yearParameter.Value = year;
            command.Parameters.Add(yearParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null || result is DBNull)
            {
                throw new InvalidOperationException("Unable to allocate next contract number.");
            }

            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
