using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace CarRental.WebApi.Controllers;

[Authorize]
[Route("api/vehicles")]
public sealed class VehiclesController(RentalDbContext dbContext) : ApiControllerBase
{
    [Authorize(Policy = ApiAuthorization.ManageFleet)]
    [HttpGet("makes")]
    [ProducesResponseType<IReadOnlyList<VehicleMakeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleMakeDto>>> GetMakes(CancellationToken cancellationToken)
    {
        var makes = await dbContext.VehicleMakes
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new VehicleMakeDto(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        return Ok(makes);
    }

    [Authorize(Policy = ApiAuthorization.ManageFleet)]
    [HttpGet("models")]
    [ProducesResponseType<IReadOnlyList<VehicleModelDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleModelDto>>> GetModels(
        [FromQuery] int? makeId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.VehicleModels
            .AsNoTracking()
            .AsQueryable();

        if (makeId.HasValue)
        {
            query = query.Where(item => item.MakeId == makeId.Value);
        }

        var models = await query
            .OrderBy(item => item.Name)
            .Select(item => new VehicleModelDto(item.Id, item.MakeId, item.Name))
            .ToListAsync(cancellationToken);

        return Ok(models);
    }

    [Authorize(Policy = ApiAuthorization.ManageRentals)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehicleDto>>(StatusCodes.Status200OK)]
    // Доступність авто складається з двох джерел істини: поточного статусу машини
    // і факту активної оренди, тому обидва критерії враховуємо прямо у вибірці.
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
        var activeRentalVehicleIdsQuery = dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.StatusId == RentalStatus.Active)
            .Select(item => item.VehicleId);
        var query = dbContext.Vehicles
            .AsNoTracking()
            .Include(item => item.Photos)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.MakeLookup!.Name, pattern) ||
                EF.Functions.ILike(item.ModelLookup!.Name, pattern) ||
                EF.Functions.ILike(item.LicensePlate, pattern));
        }

        if (availability.HasValue)
        {
            query = availability.Value
                ? query.Where(item =>
                    !item.IsDeleted &&
                    item.VehicleStatusCode == VehicleStatuses.Ready &&
                    !activeRentalVehicleIdsQuery.Contains(item.Id))
                : query.Where(item =>
                    item.IsDeleted ||
                    item.VehicleStatusCode != VehicleStatuses.Ready ||
                    activeRentalVehicleIdsQuery.Contains(item.Id));
        }

        query = ApplyVehicleClassFilter(query, vehicleClass);
        query = ApplyVehicleSorting(query, sortBy, sortDir);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var activeRentalVehicleIds = (await activeRentalVehicleIdsQuery.ToListAsync(cancellationToken)).ToHashSet();
        var vehicles = await query
            .ApplyPagination(pagination)
            .ToListAsync(cancellationToken);

        return Ok(vehicles.Select(item => item.ToDto(IsVehicleAvailable(item, activeRentalVehicleIds))).ToList());
    }

    [Authorize(Policy = ApiAuthorization.ManageRentals)]
    [HttpGet("{id:int}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var activeRentalVehicleIds = (await dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.StatusId == RentalStatus.Active)
            .Select(item => item.VehicleId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var vehicle = await dbContext.Vehicles
            .AsNoTracking()
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return vehicle is null
            ? NotFound()
            : Ok(vehicle.ToDto(IsVehicleAvailable(vehicle, activeRentalVehicleIds)));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    // Фото авто роздається окремим публічним endpoint, але шлях до файлу
    // все одно проходить через safe resolver, а не довіряється значенню з БД напряму.
    public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken)
    {
        var photoPath = await dbContext.VehiclePhotos
            .AsNoTracking()
            .Where(item => item.VehicleId == id)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.SortOrder)
            .Select(item => item.StoredPath)
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    // Create і Update використовують один і той самий validation flow,
    // щоб правила номерних знаків, довідників і фото не розходилися між сценаріями.
    public async Task<IActionResult> Create([FromBody] VehicleUpsertRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateVehicleRequestAsync(request, null, cancellationToken);
        if (!validation.Success)
        {
            return BuildVehicleValidationResult(validation.ConflictMessage, validation.ErrorMessage);
        }

        var entity = new Vehicle
        {
            MakeId = request.MakeId,
            ModelId = request.ModelId,
            PowertrainCapacityValue = request.PowertrainCapacityValue,
            PowertrainCapacityUnit = validation.PowertrainUnit,
            FuelTypeCode = validation.FuelTypeCode!,
            TransmissionTypeCode = validation.TransmissionTypeCode!,
            VehicleStatusCode = validation.VehicleStatusCode!,
            DoorsCount = request.DoorsCount,
            CargoCapacityValue = request.CargoCapacityValue,
            CargoCapacityUnit = validation.CargoCapacityUnit,
            ConsumptionValue = request.ConsumptionValue,
            ConsumptionUnit = validation.ConsumptionUnit,
            HasAirConditioning = request.HasAirConditioning,
            LicensePlate = validation.NormalizedPlate!,
            Mileage = request.Mileage,
            DailyRate = request.DailyRate,
            ServiceIntervalKm = request.ServiceIntervalKm,
            Photos = validation.Photos.Select((item, index) => new VehiclePhoto
            {
                StoredPath = item.StoredPath,
                SortOrder = item.SortOrder == 0 && validation.Photos.Count == 1 ? 0 : item.SortOrder,
                IsPrimary = item.IsPrimary || (validation.Photos.Count == 1 && index == 0),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }).ToList()
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

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity.ToDto(entity.IsAvailable));
    }

    [Authorize(Policy = ApiAuthorization.ManageFleet)]
    [HttpPut("{id:int}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] VehicleUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Vehicles
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var validation = await ValidateVehicleRequestAsync(request, id, cancellationToken);
        if (!validation.Success)
        {
            return BuildVehicleValidationResult(validation.ConflictMessage, validation.ErrorMessage);
        }

        entity.MakeId = request.MakeId;
        entity.ModelId = request.ModelId;
        entity.PowertrainCapacityValue = request.PowertrainCapacityValue;
        entity.PowertrainCapacityUnit = validation.PowertrainUnit;
        entity.FuelTypeCode = validation.FuelTypeCode!;
        entity.TransmissionTypeCode = validation.TransmissionTypeCode!;
        entity.VehicleStatusCode = validation.VehicleStatusCode!;
        entity.DoorsCount = request.DoorsCount;
        entity.CargoCapacityValue = request.CargoCapacityValue;
        entity.CargoCapacityUnit = validation.CargoCapacityUnit;
        entity.ConsumptionValue = request.ConsumptionValue;
        entity.ConsumptionUnit = validation.ConsumptionUnit;
        entity.HasAirConditioning = request.HasAirConditioning;
        entity.LicensePlate = validation.NormalizedPlate!;
        entity.Mileage = request.Mileage;
        entity.DailyRate = request.DailyRate;
        entity.ServiceIntervalKm = request.ServiceIntervalKm;
        entity.ReconcilePhotos(validation.Photos.Select((item, index) => (
            item.StoredPath,
            item.SortOrder == 0 && validation.Photos.Count == 1 ? 0 : item.SortOrder,
            item.IsPrimary || (validation.Photos.Count == 1 && index == 0))));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "License plate already exists." });
        }

        var hasActiveRental = await HasActiveRentalAsync(id, cancellationToken);
        return Ok(entity.ToDto(!hasActiveRental &&
                               !entity.IsDeleted &&
                               string.Equals(entity.VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase)));
    }

    [Authorize(Policy = ApiAuthorization.ManagePricing)]
    [HttpPatch("{id:int}/rate")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRate(int id, [FromBody] UpdateVehicleRateRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Vehicles
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.DailyRate = request.DailyRate;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.ToDto(!await HasActiveRentalAsync(id, cancellationToken) &&
                               !entity.IsDeleted &&
                               string.Equals(entity.VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase)));
    }

    [Authorize(Policy = ApiAuthorization.DeleteRecords)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        dbContext.Vehicles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<VehicleRequestValidationResult> ValidateVehicleRequestAsync(
        VehicleUpsertRequest request,
        int? currentVehicleId,
        CancellationToken cancellationToken)
    {
        var normalizedPlate = VehicleDomainRules.NormalizeLicensePlate(request.LicensePlate);
        var normalizedFuelTypeCode = request.ResolveFuelTypeCode();
        var normalizedTransmissionTypeCode = request.ResolveTransmissionTypeCode();
        var normalizedVehicleStatusCode = request.ResolveVehicleStatusCode();
        var powertrainUnit = request.PowertrainCapacityUnit.Trim().ToUpperInvariant();
        var cargoCapacityUnit = request.CargoCapacityUnit.Trim().ToUpperInvariant();
        var consumptionUnit = request.ConsumptionUnit.Trim().ToUpperInvariant();

        if (!VehicleDomainRules.IsValidLicensePlate(normalizedPlate))
        {
            return VehicleRequestValidationResult.Fail("Invalid license plate format. Use AA1234BB.");
        }

        if (!VehicleSpecificationUnits.IsValidPowertrainUnit(powertrainUnit))
        {
            return VehicleRequestValidationResult.Fail("Invalid powertrain capacity unit.");
        }

        if (!VehicleSpecificationUnits.IsValidCargoUnit(cargoCapacityUnit))
        {
            return VehicleRequestValidationResult.Fail("Invalid cargo capacity unit.");
        }

        if (!VehicleSpecificationUnits.IsValidConsumptionUnit(consumptionUnit))
        {
            return VehicleRequestValidationResult.Fail("Invalid consumption unit.");
        }

        if (string.IsNullOrWhiteSpace(normalizedFuelTypeCode) ||
            !await dbContext.FuelTypes.AnyAsync(item => item.Code == normalizedFuelTypeCode, cancellationToken))
        {
            return VehicleRequestValidationResult.Fail("Unknown fuel type.");
        }

        if (string.IsNullOrWhiteSpace(normalizedTransmissionTypeCode) ||
            !await dbContext.TransmissionTypes.AnyAsync(item => item.Code == normalizedTransmissionTypeCode, cancellationToken))
        {
            return VehicleRequestValidationResult.Fail("Unknown transmission type.");
        }

        if (string.IsNullOrWhiteSpace(normalizedVehicleStatusCode) ||
            !await dbContext.VehicleStatuses.AnyAsync(item => item.Code == normalizedVehicleStatusCode, cancellationToken))
        {
            return VehicleRequestValidationResult.Fail("Unknown vehicle status.");
        }

        var makeExists = await dbContext.VehicleMakes
            .AnyAsync(item => item.Id == request.MakeId, cancellationToken);
        if (!makeExists)
        {
            return VehicleRequestValidationResult.Fail("Unknown vehicle make.");
        }

        var modelExists = await dbContext.VehicleModels
            .AnyAsync(item => item.Id == request.ModelId && item.MakeId == request.MakeId, cancellationToken);
        if (!modelExists)
        {
            return VehicleRequestValidationResult.Fail("Unknown vehicle model.");
        }

        var plateExists = await dbContext.Vehicles
            .AnyAsync(item => item.Id != currentVehicleId && item.LicensePlate == normalizedPlate, cancellationToken);
        if (plateExists)
        {
            return VehicleRequestValidationResult.Conflict("License plate already exists.");
        }

        if (!TryNormalizeVehiclePhotos(request.ResolvePhotos(), out var photos, out var errorMessage))
        {
            return VehicleRequestValidationResult.Fail(errorMessage ?? "Vehicle photos are invalid.");
        }

        return VehicleRequestValidationResult.SuccessResult(
            normalizedPlate,
            normalizedFuelTypeCode,
            normalizedTransmissionTypeCode,
            normalizedVehicleStatusCode,
            powertrainUnit,
            cargoCapacityUnit,
            consumptionUnit,
            photos);
    }

    private static bool TryNormalizeVehiclePhotos(
        IReadOnlyList<MediaAssetUpsertRequest> requests,
        out IReadOnlyList<MediaAssetUpsertRequest> normalized,
        out string? errorMessage)
    {
        normalized = Array.Empty<MediaAssetUpsertRequest>();
        errorMessage = null;
        var photos = new List<MediaAssetUpsertRequest>();

        foreach (var request in requests)
        {
            if (!VehiclePhotoCatalog.TryNormalizeStoredPhotoPath(request.StoredPath, requireFileExists: true, out var storedPath))
            {
                errorMessage = "Invalid photo path. Only files from /images/vehicles are allowed.";
                return false;
            }

            photos.Add(new MediaAssetUpsertRequest
            {
                StoredPath = storedPath!,
                SortOrder = request.SortOrder,
                IsPrimary = request.IsPrimary
            });
        }

        if (photos.Count > 0 && photos.All(item => !item.IsPrimary))
        {
            photos[0].IsPrimary = true;
        }

        normalized = photos;
        return true;
    }

    private static bool IsVehicleAvailable(Vehicle vehicle, IReadOnlySet<int> activeRentalVehicleIds)
        => !vehicle.IsDeleted &&
           string.Equals(vehicle.VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase) &&
           !activeRentalVehicleIds.Contains(vehicle.Id);

    private Task<bool> HasActiveRentalAsync(int vehicleId, CancellationToken cancellationToken)
        => dbContext.Rentals.AnyAsync(
            item => item.VehicleId == vehicleId && item.StatusId == RentalStatus.Active,
            cancellationToken);

    private static IActionResult BuildVehicleValidationResult(string? conflictMessage, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(conflictMessage))
        {
            return new ConflictObjectResult(new { message = conflictMessage });
        }

        return new BadRequestObjectResult(new { message = errorMessage ?? "Vehicle validation failed." });
    }

    private sealed record VehicleRequestValidationResult(
        bool Success,
        string? NormalizedPlate,
        string? FuelTypeCode,
        string? TransmissionTypeCode,
        string? VehicleStatusCode,
        string PowertrainUnit,
        string CargoCapacityUnit,
        string ConsumptionUnit,
        IReadOnlyList<MediaAssetUpsertRequest> Photos,
        string? ConflictMessage,
        string? ErrorMessage)
    {
        public static VehicleRequestValidationResult Fail(string errorMessage)
            => new(false, null, null, null, null, string.Empty, string.Empty, string.Empty, Array.Empty<MediaAssetUpsertRequest>(), null, errorMessage);

        public static VehicleRequestValidationResult Conflict(string conflictMessage)
            => new(false, null, null, null, null, string.Empty, string.Empty, string.Empty, Array.Empty<MediaAssetUpsertRequest>(), conflictMessage, null);

        public static VehicleRequestValidationResult SuccessResult(
            string normalizedPlate,
            string fuelTypeCode,
            string transmissionTypeCode,
            string vehicleStatusCode,
            string powertrainUnit,
            string cargoCapacityUnit,
            string consumptionUnit,
            IReadOnlyList<MediaAssetUpsertRequest> photos)
            => new(true, normalizedPlate, fuelTypeCode, transmissionTypeCode, vehicleStatusCode, powertrainUnit, cargoCapacityUnit, consumptionUnit, photos, null, null);
    }

    private static IQueryable<Vehicle> ApplyVehicleClassFilter(IQueryable<Vehicle> query, string? vehicleClass)
    {
        return NormalizeVehicleClass(vehicleClass) switch
        {
            "Economy" => query.Where(item => item.DailyRate < VehicleDomainRules.EconomyUpperBound),
            "Mid" => query.Where(item => item.DailyRate >= VehicleDomainRules.EconomyUpperBound && item.DailyRate < VehicleDomainRules.MidUpperBound),
            "Business" => query.Where(item => item.DailyRate >= VehicleDomainRules.MidUpperBound && item.DailyRate < VehicleDomainRules.BusinessUpperBound),
            "Premium" => query.Where(item => item.DailyRate >= VehicleDomainRules.BusinessUpperBound),
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
            ("dailyrate", false) => query.OrderBy(item => item.DailyRate).ThenBy(item => item.MakeLookup!.Name).ThenBy(item => item.ModelLookup!.Name),
            ("dailyrate", true) => query.OrderByDescending(item => item.DailyRate).ThenBy(item => item.MakeLookup!.Name).ThenBy(item => item.ModelLookup!.Name),
            ("mileage", false) => query.OrderBy(item => item.Mileage).ThenBy(item => item.MakeLookup!.Name).ThenBy(item => item.ModelLookup!.Name),
            ("mileage", true) => query.OrderByDescending(item => item.Mileage).ThenBy(item => item.MakeLookup!.Name).ThenBy(item => item.ModelLookup!.Name),
            ("name", true) => query.OrderByDescending(item => item.MakeLookup!.Name).ThenByDescending(item => item.ModelLookup!.Name),
            _ => query.OrderBy(item => item.MakeLookup!.Name).ThenBy(item => item.ModelLookup!.Name)
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
