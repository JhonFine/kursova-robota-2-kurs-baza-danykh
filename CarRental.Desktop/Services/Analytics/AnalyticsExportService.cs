using CarRental.Desktop.Data;
using CarRental.Desktop.Localization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;
using System.Text;

namespace CarRental.Desktop.Services.Analytics;

// Експорт будується з одного запиту-джерела для CSV і Excel,
// щоб різні формати не роз'їжджались по складу колонок і правилах фільтрації.
public sealed class AnalyticsExportService(RentalDbContext dbContext, string exportDirectory) : IAnalyticsExportService
{
    public async Task<string> ExportRentalsCsvAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        var rows = await QueryRowsAsync(request, cancellationToken);
        Directory.CreateDirectory(exportDirectory);

        var path = Path.Combine(exportDirectory, $"rentals_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        var builder = new StringBuilder();
        builder.AppendLine("НомерДоговору,ДатаПочатку,ДатаКінця,Клієнт,Авто,Менеджер,Статус,Сума");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",",
                Escape(row.ContractNumber),
                row.StartDate.ToString("yyyy-MM-dd"),
                row.EndDate.ToString("yyyy-MM-dd"),
                Escape(row.Client),
                Escape(row.Vehicle),
                Escape(row.Manager),
                row.Status,
                row.TotalAmount.ToString(CultureInfo.InvariantCulture)));
        }

        await File.WriteAllTextAsync(
            path,
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            cancellationToken);
        return path;
    }

    public async Task<string> ExportRentalsExcelAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        var rows = await QueryRowsAsync(request, cancellationToken);
        Directory.CreateDirectory(exportDirectory);
        var path = Path.Combine(exportDirectory, $"rentals_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Оренди");
        ws.Cell(1, 1).Value = "Договір";
        ws.Cell(1, 2).Value = "Початок";
        ws.Cell(1, 3).Value = "Кінець";
        ws.Cell(1, 4).Value = "Клієнт";
        ws.Cell(1, 5).Value = "Авто";
        ws.Cell(1, 6).Value = "Менеджер";
        ws.Cell(1, 7).Value = "Статус";
        ws.Cell(1, 8).Value = "Сума";

        var rowIndex = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowIndex, 1).Value = row.ContractNumber;
            ws.Cell(rowIndex, 2).Value = row.StartDate;
            ws.Cell(rowIndex, 3).Value = row.EndDate;
            ws.Cell(rowIndex, 4).Value = row.Client;
            ws.Cell(rowIndex, 5).Value = row.Vehicle;
            ws.Cell(rowIndex, 6).Value = row.Manager;
            ws.Cell(rowIndex, 7).Value = row.Status;
            ws.Cell(rowIndex, 8).Value = (double)row.TotalAmount;
            rowIndex++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(path);
        await Task.CompletedTask;
        return path;
    }

    private async Task<List<ExportRow>> QueryRowsAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        var from = request.FromDate.Date;
        var to = request.ToDate.Date;
        // Для аналітики беремо IgnoreQueryFilters, щоб soft-deleted клієнти/авто не ламали історичні рядки звіту.
        var clients = dbContext.Clients
            .AsNoTracking()
            .IgnoreQueryFilters();
        var vehicles = dbContext.Vehicles
            .AsNoTracking()
            .IgnoreQueryFilters();

        var query = dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.StartDate >= from && item.EndDate <= to);

        if (request.VehicleId.HasValue)
        {
            query = query.Where(item => item.VehicleId == request.VehicleId.Value);
        }

        if (request.EmployeeId.HasValue)
        {
            query = query.Where(item => item.EmployeeId == request.EmployeeId.Value);
        }

        return await query
            .OrderByDescending(item => item.StartDate)
            .Select(item => new ExportRow(
                item.ContractNumber,
                item.StartDate,
                item.EndDate,
                clients
                    .Where(client => client.Id == item.ClientId)
                    .Select(client => client.FullName)
                    .FirstOrDefault() ?? string.Empty,
                vehicles
                    .Where(vehicle => vehicle.Id == item.VehicleId)
                    .Select(vehicle => vehicle.Make + " " + vehicle.Model)
                    .FirstOrDefault() ?? string.Empty,
                item.Employee != null ? item.Employee.FullName : string.Empty,
                item.Status.ToDisplay(),
                item.TotalAmount))
            .ToListAsync(cancellationToken);
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed record ExportRow(
        string ContractNumber,
        DateTime StartDate,
        DateTime EndDate,
        string Client,
        string Vehicle,
        string Manager,
        string Status,
        decimal TotalAmount);
}
