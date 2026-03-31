using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Damages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManageDamages)]
[Route("api/damages")]
public sealed class DamagesController(
    RentalDbContext dbContext,
    IDamageService damageService,
    ILogger<DamagesController>? logger = null) : ApiControllerBase
{
    private readonly ILogger<DamagesController> logger = logger ?? NullLogger<DamagesController>.Instance;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DamageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DamageDto>>> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = PaginationExtensions.Normalize(page, pageSize);
        var query = dbContext.Damages
            .AsNoTracking()
            .Include(item => item.Vehicle)
            .Include(item => item.Rental)
            .Include(item => item.ReportedByEmployee)
            .Include(item => item.Photos)
            .OrderByDescending(item => item.DateReported);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var damages = await query
            .ApplyPagination(pagination)
            .ToListAsync(cancellationToken);

        return Ok(damages.Select(item => item.ToDto()).ToList());
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<DamageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Add([FromBody] AddDamageRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var createResult = await CreateDamageAsync(request, employeeId.Value, cancellationToken);
        if (!createResult.Success)
        {
            return BadRequest(new { message = createResult.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetAll), createResult.Damage!.ToDto());
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProtectedDamagePhotoStorage.MaxRequestSizeBytes)]
    [ProducesResponseType<DamageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddMultipart([FromForm] AddDamageMultipartRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        if (request.Photos.Count > ProtectedDamagePhotoStorage.MaxFilesPerDamage)
        {
            return BadRequest(new
            {
                message = $"Можна додати не більше {ProtectedDamagePhotoStorage.MaxFilesPerDamage} фото до одного акту."
            });
        }

        var storedPhotoPaths = new List<string>();

        try
        {
            foreach (var photo in request.Photos.Where(item => item is not null))
            {
                var storeResult = await ProtectedDamagePhotoStorage.StoreDamagePhotoAsync(
                    photo,
                    request.VehicleId,
                    cancellationToken);
                if (!storeResult.Success || string.IsNullOrWhiteSpace(storeResult.StoredPath))
                {
                    foreach (var storedPath in storedPhotoPaths)
                    {
                        ProtectedDamagePhotoStorage.TryDeleteStoredPhoto(storedPath);
                    }

                    return BadRequest(new { message = storeResult.ErrorMessage ?? "Не вдалося зберегти фото пошкодження." });
                }

                storedPhotoPaths.Add(storeResult.StoredPath);
            }

            var createResult = await CreateDamageAsync(
                new AddDamageRequest
                {
                    VehicleId = request.VehicleId,
                    RentalId = request.RentalId,
                    Description = request.Description,
                    RepairCost = request.RepairCost,
                    AutoChargeToRental = request.AutoChargeToRental,
                    Photos = storedPhotoPaths
                        .Select((path, index) => new MediaAssetUpsertRequest
                        {
                            StoredPath = path,
                            SortOrder = index
                        })
                        .ToList()
                },
                employeeId.Value,
                cancellationToken);

            if (!createResult.Success)
            {
                foreach (var storedPath in storedPhotoPaths)
                {
                    ProtectedDamagePhotoStorage.TryDeleteStoredPhoto(storedPath);
                }

                return BadRequest(new { message = createResult.ErrorMessage });
            }

            return CreatedAtAction(nameof(GetAll), createResult.Damage!.ToDto());
        }
        catch
        {
            foreach (var storedPath in storedPhotoPaths)
            {
                ProtectedDamagePhotoStorage.TryDeleteStoredPhoto(storedPath);
            }

            throw;
        }
    }

    [HttpGet("{damageId:int}/photos/{photoId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(int damageId, int photoId, CancellationToken cancellationToken)
    {
        var photoPath = await dbContext.DamagePhotos
            .AsNoTracking()
            .Where(item => item.DamageId == damageId && item.Id == photoId)
            .Select(item => item.StoredPath)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ProtectedDamagePhotoStorage.TryResolveStoredPhotoPath(
                photoPath,
                requireFileExists: true,
                out var fullPath,
                out _)
            || string.IsNullOrWhiteSpace(fullPath))
        {
            return NotFound();
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, ProtectedDamagePhotoStorage.ResolveContentType(fullPath));
    }

    private async Task<CreateDamageResult> CreateDamageAsync(
        AddDamageRequest request,
        int reportedByEmployeeId,
        CancellationToken cancellationToken)
    {
        var resolvedPhotos = request.ResolvePhotos()
            .Where(item => !string.IsNullOrWhiteSpace(item.StoredPath))
            .Select(item => item.StoredPath.Trim())
            .ToList();

        logger.LogInformation(
            "Creating damage act request. TraceId={TraceId} VehicleId={VehicleId} RentalId={RentalId} ReportedByEmployeeId={ReportedByEmployeeId} AutoChargeToRental={AutoChargeToRental} PhotoCount={PhotoCount}",
            HttpContext.TraceIdentifier,
            request.VehicleId,
            request.RentalId,
            reportedByEmployeeId,
            request.AutoChargeToRental,
            resolvedPhotos.Count);

        var result = await damageService.AddDamageAsync(
            new DamageRequest(
                VehicleId: request.VehicleId,
                RentalId: request.RentalId,
                Description: request.Description,
                RepairCost: request.RepairCost,
                AutoChargeToRental: request.AutoChargeToRental,
                ReportedByEmployeeId: reportedByEmployeeId,
                PhotoPaths: resolvedPhotos),
            cancellationToken);

        if (!result.Success)
        {
            logger.LogWarning(
                "Damage act request rejected. TraceId={TraceId} VehicleId={VehicleId} RentalId={RentalId} ReportedByEmployeeId={ReportedByEmployeeId} Error={Error}",
                HttpContext.TraceIdentifier,
                request.VehicleId,
                request.RentalId,
                reportedByEmployeeId,
                result.Message);
            return new CreateDamageResult(false, null, result.Message);
        }

        var damage = await dbContext.Damages
            .AsNoTracking()
            .Include(item => item.Vehicle)
            .Include(item => item.Rental)
            .Include(item => item.ReportedByEmployee)
            .Include(item => item.Photos)
            .FirstOrDefaultAsync(item => item.Id == result.DamageId, cancellationToken);

        if (damage is null)
        {
            logger.LogError(
                "Damage act was created but could not be reloaded. TraceId={TraceId} DamageId={DamageId}",
                HttpContext.TraceIdentifier,
                result.DamageId);
            return new CreateDamageResult(false, null, "Акт збережено, але не вдалося оновити журнал пошкоджень. Оновіть сторінку.");
        }

        return new CreateDamageResult(true, damage, null);
    }

    public sealed class AddDamageMultipartRequest
    {
        [Range(1, int.MaxValue)]
        public int VehicleId { get; set; }

        public int? RentalId { get; set; }

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [DecimalRangeInvariant("0.01", "1000000")]
        public decimal RepairCost { get; set; }

        public bool AutoChargeToRental { get; set; }

        public List<IFormFile> Photos { get; set; } = new();
    }

    private sealed record CreateDamageResult(bool Success, Damage? Damage, string? ErrorMessage);
}
