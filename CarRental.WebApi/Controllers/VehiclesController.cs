using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.IO;

namespace CarRental.WebApi.Controllers;

[Authorize]
[Route("api/vehicles")]
public sealed class VehiclesController(RentalDbContext dbContext) : ApiControllerBase
{
    [Authorize(Policy = ApiAuthorization.ManageRentals)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehicleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? availability,
        [FromQuery] string? vehicleClass,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = PaginationExtensions.Normalize(page, pageSize);
        var query = dbContext.Vehicles
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.Make, pattern) ||
                EF.Functions.ILike(item.Model, pattern) ||
                EF.Functions.ILike(item.LicensePlate, pattern));
        }

        if (availability.HasValue)
        {
            query = query.Where(item => item.IsAvailable == availability.Value);
        }

        query = ApplyVehicleClassFilter(query, vehicleClass);
        query = ApplyVehicleSorting(query, sortBy, sortDir);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var vehicles = await query
            .ApplyPagination(pagination)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);

        return Ok(vehicles);
    }

    [Authorize(Policy = ApiAuthorization.ManageRentals)]
    [HttpGet("{id:int}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => ToDto(item))
            .FirstOrDefaultAsync(cancellationToken);

        return vehicle is null ? NotFound() : Ok(vehicle);
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken)
    {
        var photoPath = await dbContext.Vehicles
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.PhotoPath)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return NotFound();
        }

        if (!VehiclePhotoCatalog.TryResolveStoredPhotoPath(photoPath, out var fullPath) ||
            string.IsNullOrWhiteSpace(fullPath))
        {
            return NotFound();
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, ResolveImageContentType(fullPath));
    }

    [Authorize(Policy = ApiAuthorization.ManageFleet)]
    [HttpPost]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] VehicleUpsertRequest request, CancellationToken cancellationToken)
    {
        var normalizedPlate = request.LicensePlate.Trim().ToUpperInvariant();
        var plateExists = await dbContext.Vehicles
            .AnyAsync(item => item.LicensePlate == normalizedPlate, cancellationToken);
        if (plateExists)
        {
            return Conflict(new { message = "License plate already exists." });
        }

        if (!VehiclePhotoCatalog.TryNormalizeStoredPhotoPath(request.PhotoPath, requireFileExists: true, out var normalizedPhotoPath))
        {
            return BadRequest(new { message = "Invalid photo path. Only files from /images/vehicles are allowed." });
        }

        var entity = new Vehicle
        {
            Make = request.Make.Trim(),
            Model = request.Model.Trim(),
            EngineDisplay = request.EngineDisplay.Trim(),
            FuelType = request.FuelType.Trim(),
            TransmissionType = request.TransmissionType.Trim(),
            DoorsCount = request.DoorsCount,
            CargoCapacityDisplay = request.CargoCapacityDisplay.Trim(),
            ConsumptionDisplay = request.ConsumptionDisplay.Trim(),
            HasAirConditioning = request.HasAirConditioning,
            LicensePlate = normalizedPlate,
            Mileage = request.Mileage,
            DailyRate = request.DailyRate,
            IsAvailable = request.IsAvailable,
            ServiceIntervalKm = request.ServiceIntervalKm,
            PhotoPath = normalizedPhotoPath
        };

        dbContext.Vehicles.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "License plate already exists." });
        }

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    [Authorize(Policy = ApiAuthorization.ManageFleet)]
    [HttpPut("{id:int}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] VehicleUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var normalizedPlate = request.LicensePlate.Trim().ToUpperInvariant();
        var plateExists = await dbContext.Vehicles
            .AnyAsync(item => item.Id != id && item.LicensePlate == normalizedPlate, cancellationToken);
        if (plateExists)
        {
            return Conflict(new { message = "License plate already exists." });
        }

        if (!VehiclePhotoCatalog.TryNormalizeStoredPhotoPath(request.PhotoPath, requireFileExists: true, out var normalizedPhotoPath))
        {
            return BadRequest(new { message = "Invalid photo path. Only files from /images/vehicles are allowed." });
        }

        entity.Make = request.Make.Trim();
        entity.Model = request.Model.Trim();
        entity.EngineDisplay = request.EngineDisplay.Trim();
        entity.FuelType = request.FuelType.Trim();
        entity.TransmissionType = request.TransmissionType.Trim();
        entity.DoorsCount = request.DoorsCount;
        entity.CargoCapacityDisplay = request.CargoCapacityDisplay.Trim();
        entity.ConsumptionDisplay = request.ConsumptionDisplay.Trim();
        entity.HasAirConditioning = request.HasAirConditioning;
        entity.LicensePlate = normalizedPlate;
        entity.Mileage = request.Mileage;
        entity.DailyRate = request.DailyRate;
        entity.IsAvailable = request.IsAvailable;
        entity.ServiceIntervalKm = request.ServiceIntervalKm;
        entity.PhotoPath = normalizedPhotoPath;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "License plate already exists." });
        }

        return Ok(ToDto(entity));
    }

    [Authorize(Policy = ApiAuthorization.ManagePricing)]
    [HttpPatch("{id:int}/rate")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRate(int id, [FromBody] UpdateVehicleRateRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.DailyRate = request.DailyRate;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(entity));
    }

    [Authorize(Policy = ApiAuthorization.DeleteRecords)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var hasRentals = await dbContext.Rentals.AnyAsync(item => item.VehicleId == id, cancellationToken);
        if (hasRentals)
        {
            return Conflict(new { message = "Cannot delete vehicle with rentals history." });
        }

        dbContext.Vehicles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static VehicleDto ToDto(Vehicle vehicle)
        => new(
            vehicle.Id,
            vehicle.Make,
            vehicle.Model,
            vehicle.EngineDisplay,
            vehicle.FuelType,
            vehicle.TransmissionType,
            vehicle.DoorsCount,
            vehicle.CargoCapacityDisplay,
            vehicle.ConsumptionDisplay,
            vehicle.HasAirConditioning,
            vehicle.LicensePlate,
            vehicle.Mileage,
            vehicle.DailyRate,
            vehicle.IsAvailable,
            vehicle.ServiceIntervalKm,
            vehicle.PhotoPath);

    private static IQueryable<Vehicle> ApplyVehicleClassFilter(IQueryable<Vehicle> query, string? vehicleClass)
    {
        return NormalizeVehicleClass(vehicleClass) switch
        {
            "Economy" => query.Where(item => item.DailyRate < 45m),
            "Mid" => query.Where(item => item.DailyRate >= 45m && item.DailyRate < 70m),
            "Business" => query.Where(item => item.DailyRate >= 70m && item.DailyRate < 95m),
            "Premium" => query.Where(item => item.DailyRate >= 95m),
            _ => query
        };
    }

    private static IQueryable<Vehicle> ApplyVehicleSorting(
        IQueryable<Vehicle> query,
        string? sortBy,
        string? sortDir)
    {
        var normalizedSortBy = (sortBy ?? "name").Trim().ToLowerInvariant();
        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        return (normalizedSortBy, descending) switch
        {
            ("dailyrate", false) => query.OrderBy(item => item.DailyRate).ThenBy(item => item.Make).ThenBy(item => item.Model),
            ("dailyrate", true) => query.OrderByDescending(item => item.DailyRate).ThenBy(item => item.Make).ThenBy(item => item.Model),
            ("mileage", false) => query.OrderBy(item => item.Mileage).ThenBy(item => item.Make).ThenBy(item => item.Model),
            ("mileage", true) => query.OrderByDescending(item => item.Mileage).ThenBy(item => item.Make).ThenBy(item => item.Model),
            ("name", true) => query.OrderByDescending(item => item.Make).ThenByDescending(item => item.Model),
            _ => query.OrderBy(item => item.Make).ThenBy(item => item.Model)
        };
    }

    private static string? NormalizeVehicleClass(string? vehicleClass)
    {
        return (vehicleClass ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "economy" => "Economy",
            "mid" => "Mid",
            "business" => "Business",
            "premium" => "Premium",
            _ => null
        };
    }

    private static string ResolveImageContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

}
