using CarRental.Desktop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Globalization;

namespace CarRental.Desktop.Services.Documents;

// Номер договору резервується прямо в БД по роках,
// щоб паралельні створення оренд не породжували дублікати в desktop і web потоках.
public sealed class ContractNumberService(RentalDbContext dbContext) : IContractNumberService
{
    public async Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
    {
        var year = DateTime.UtcNow.Year;

        // Рядок sequence створюється ідемпотентно, після чого той самий запис атомарно інкрементується через SQL.
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
        // Використовуємо поточну EF-транзакцію, якщо вона вже відкрита навколо створення оренди.
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
