using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManageClients)]
[Route("api/clients")]
public sealed class ClientsController(RentalDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientDto>>(StatusCodes.Status200OK)]
    // Пошук по клієнтах охоплює і базові поля профілю, і номери документів,
    // тому server-side pagination завжди рахується вже по відфільтрованій множині.
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? blacklisted,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = PaginationExtensions.Normalize(page, pageSize);
        var query = dbContext.Clients
            .AsNoTracking()
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.FullName, pattern) ||
                EF.Functions.ILike(item.Phone, pattern) ||
                item.Documents.Any(document =>
                    !document.IsDeleted &&
                    EF.Functions.ILike(document.DocumentNumber, pattern)));
        }

        if (blacklisted.HasValue)
        {
            query = query.Where(item => item.IsBlacklisted == blacklisted.Value);
        }

        query = query.OrderBy(item => item.FullName);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var clients = await query
            .ApplyPagination(pagination)
            .ToListAsync(cancellationToken);

        return Ok(clients.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .AsNoTracking()
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return client is null ? NotFound() : Ok(client.ToDto());
    }

    [HttpPost]
    [ProducesResponseType<ClientDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    // Create і Update працюють через одну нормалізацію документів та телефону,
    // щоб CRM-режим і self-service профіль не роз'їжджалися по правилах збереження.
    public async Task<IActionResult> Create([FromBody] ClientUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveBlacklistActorId(request.IsBlacklisted, out var blacklistedByEmployeeId, out var actorErrorResult))
        {
            return actorErrorResult!;
        }

        var validation = await ValidateClientRequestAsync(
            request.FullName,
            request.Phone,
            request.ResolveDocuments(),
            null,
            cancellationToken);
        if (!validation.Success)
        {
            return BuildClientValidationResult(validation.ConflictMessage, validation.ErrorMessage);
        }

        var entity = new Client
        {
            FullName = request.FullName.Trim(),
            Phone = validation.NormalizedPhone!,
            IsBlacklisted = request.IsBlacklisted,
            BlacklistReason = request.IsBlacklisted ? request.BlacklistReason?.Trim() : null,
            BlacklistedAtUtc = request.IsBlacklisted ? DateTime.UtcNow : null,
            BlacklistedByEmployeeId = request.IsBlacklisted ? blacklistedByEmployeeId : null
        };

        ClientProfileRules.UpsertDocuments(entity, validation.Documents);

        dbContext.Clients.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "Phone or document number already exists." });
        }

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity.ToDto());
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] ClientUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var validation = await ValidateClientRequestAsync(
            request.FullName,
            request.Phone,
            request.ResolveDocuments(),
            id,
            cancellationToken);
        if (!validation.Success)
        {
            return BuildClientValidationResult(validation.ConflictMessage, validation.ErrorMessage);
        }

        if (!TryResolveBlacklistActorId(request.IsBlacklisted, out var blacklistedByEmployeeId, out var actorErrorResult))
        {
            return actorErrorResult!;
        }

        entity.FullName = request.FullName.Trim();
        entity.Phone = validation.NormalizedPhone!;
        entity.IsBlacklisted = request.IsBlacklisted;
        entity.BlacklistReason = request.IsBlacklisted ? request.BlacklistReason?.Trim() : null;
        entity.BlacklistedAtUtc = request.IsBlacklisted
            ? entity.BlacklistedAtUtc ?? DateTime.UtcNow
            : null;
        entity.BlacklistedByEmployeeId = request.IsBlacklisted
            ? entity.BlacklistedByEmployeeId ?? blacklistedByEmployeeId
            : null;
        ClientProfileRules.UpsertDocuments(entity, validation.Documents);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "Phone or document number already exists." });
        }

        return Ok(entity.ToDto());
    }

    [HttpPatch("{id:int}/blacklist")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBlacklist(int id, [FromBody] SetBlacklistRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!TryResolveBlacklistActorId(request.IsBlacklisted, out var blacklistedByEmployeeId, out var actorErrorResult))
        {
            return actorErrorResult!;
        }

        entity.IsBlacklisted = request.IsBlacklisted;
        entity.BlacklistReason = request.IsBlacklisted ? request.BlacklistReason?.Trim() : null;
        entity.BlacklistedAtUtc = request.IsBlacklisted
            ? entity.BlacklistedAtUtc ?? DateTime.UtcNow
            : null;
        entity.BlacklistedByEmployeeId = request.IsBlacklisted ? blacklistedByEmployeeId : null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity.ToDto());
    }

    [Authorize(Policy = ApiAuthorization.DeleteRecords)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        dbContext.Clients.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/documents/{documentType}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProtectedDocumentStorage.MaxFileSizeBytes)]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadDocumentPhoto(
        int id,
        string documentType,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!TryResolveClientDocumentCode(documentType, out var documentTypeCode, out var storageDocumentType))
        {
            return BadRequest(new { message = "Unknown document type. Use passport-photo or driver-license-photo." });
        }

        var storeResult = await ProtectedDocumentStorage.StoreDocumentPhotoAsync(
            file,
            entity.Id,
            storageDocumentType,
            cancellationToken);
        if (!storeResult.Success || string.IsNullOrWhiteSpace(storeResult.StoredPath))
        {
            return BadRequest(new { message = storeResult.ErrorMessage ?? "Failed to store document photo." });
        }

        var existing = ClientProfileRules.GetActiveDocument(entity, documentTypeCode);
        var previousPath = existing?.StoredPath;

        ClientProfileRules.UpsertDocuments(
            entity,
            new[]
            {
                new ClientDocumentUpsertRequest
                {
                    DocumentTypeCode = ClientDocumentTypes.Passport,
                    DocumentNumber = entity.PassportData,
                    ExpirationDate = entity.PassportExpirationDate,
                    StoredPath = documentTypeCode == ClientDocumentTypes.Passport ? storeResult.StoredPath : entity.PassportPhotoPath
                },
                new ClientDocumentUpsertRequest
                {
                    DocumentTypeCode = ClientDocumentTypes.DriverLicense,
                    DocumentNumber = entity.DriverLicense,
                    ExpirationDate = entity.DriverLicenseExpirationDate,
                    StoredPath = documentTypeCode == ClientDocumentTypes.DriverLicense ? storeResult.StoredPath : entity.DriverLicensePhotoPath
                }
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        ProtectedDocumentStorage.TryDeleteStoredPhoto(previousPath);

        return Ok(entity.ToDto());
    }

    [HttpGet("{id:int}/documents/{documentType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentPhoto(int id, string documentType, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients
            .AsNoTracking()
            .Include(item => item.Documents.Where(document => !document.IsDeleted))
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!TryResolveClientDocumentCode(documentType, out var documentTypeCode, out _))
        {
            return NotFound();
        }

        var storedPath = ClientProfileRules.GetActiveDocument(entity, documentTypeCode)?.StoredPath;
        if (!ProtectedDocumentStorage.TryResolveStoredPhotoPath(
                storedPath,
                requireFileExists: true,
                out var fullPath,
                out _)
            || string.IsNullOrWhiteSpace(fullPath))
        {
            return NotFound();
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, ProtectedDocumentStorage.ResolveContentType(fullPath));
    }

    private async Task<ClientRequestValidationResult> ValidateClientRequestAsync(
        string fullName,
        string phone,
        IReadOnlyList<ClientDocumentUpsertRequest> documentRequests,
        int? currentClientId,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = ClientProfileRules.TryNormalizePhone(phone);

        if (string.IsNullOrWhiteSpace(fullName.Trim()))
        {
            return new ClientRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, "Full name is required.");
        }

        if (normalizedPhone is null)
        {
            return new ClientRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, "Enter a valid phone number with 10-15 digits.");
        }

        if (!TryNormalizeClientDocuments(documentRequests, out var documents, out var errorMessage))
        {
            return new ClientRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, errorMessage);
        }

        foreach (var document in documents)
        {
            var duplicateExists = await dbContext.ClientDocuments
                .IgnoreQueryFilters()
                .AnyAsync(
                    item => !item.IsDeleted &&
                            item.ClientId != currentClientId &&
                            item.DocumentTypeCode == document.DocumentTypeCode &&
                            item.DocumentNumber == document.DocumentNumber,
                    cancellationToken);
            if (duplicateExists)
            {
                return new ClientRequestValidationResult(
                    false,
                    null,
                    Array.Empty<ClientDocumentUpsertRequest>(),
                    document.DocumentTypeCode == ClientDocumentTypes.Passport
                        ? "Passport number already exists."
                        : "Driver license already exists.",
                    null);
            }
        }

        var phoneExists = await dbContext.Clients
            .AnyAsync(item => item.Id != currentClientId && item.Phone == normalizedPhone, cancellationToken);
        if (phoneExists)
        {
            return new ClientRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), "Phone number already exists.", null);
        }

        return new ClientRequestValidationResult(true, normalizedPhone, documents, null, null);
    }

    private static bool TryNormalizeClientDocuments(
        IReadOnlyList<ClientDocumentUpsertRequest> requests,
        out IReadOnlyList<ClientDocumentUpsertRequest> normalized,
        out string? errorMessage)
    {
        normalized = Array.Empty<ClientDocumentUpsertRequest>();
        errorMessage = null;
        var documents = new List<ClientDocumentUpsertRequest>();

        foreach (var request in requests)
        {
            var documentTypeCode = request.DocumentTypeCode.Trim().ToUpperInvariant();
            if (documentTypeCode is not (ClientDocumentTypes.Passport or ClientDocumentTypes.DriverLicense))
            {
                errorMessage = $"Unsupported document type '{request.DocumentTypeCode}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
            {
                errorMessage = documentTypeCode == ClientDocumentTypes.Passport
                    ? "Passport number is required."
                    : "Driver license number is required.";
                return false;
            }

            if (!ProtectedDocumentStorage.TryNormalizeStoredPhotoPath(request.StoredPath, out var storedPath))
            {
                errorMessage = "Invalid document photo path. Only protected document storage paths are allowed.";
                return false;
            }

            documents.Add(new ClientDocumentUpsertRequest
            {
                DocumentTypeCode = documentTypeCode,
                DocumentNumber = request.DocumentNumber.Trim(),
                ExpirationDate = request.ExpirationDate?.Date,
                StoredPath = storedPath
            });
        }

        normalized = documents;
        return true;
    }

    private static bool TryResolveClientDocumentCode(
        string documentType,
        out string documentTypeCode,
        out ClientDocumentPhotoType storageDocumentType)
    {
        if (!ProtectedDocumentStorage.TryParseDocumentType(documentType, out storageDocumentType))
        {
            documentTypeCode = string.Empty;
            return false;
        }

        documentTypeCode = storageDocumentType == ClientDocumentPhotoType.Passport
            ? ClientDocumentTypes.Passport
            : ClientDocumentTypes.DriverLicense;
        return true;
    }

    private static IActionResult BuildClientValidationResult(string? conflictMessage, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(conflictMessage))
        {
            return new ConflictObjectResult(new { message = conflictMessage });
        }

        return new BadRequestObjectResult(new { message = errorMessage ?? "Client validation failed." });
    }

    private bool TryResolveBlacklistActorId(bool isBlacklisted, out int? employeeId, out IActionResult? errorResult)
    {
        employeeId = null;
        errorResult = null;

        if (!isBlacklisted)
        {
            return true;
        }

        employeeId = GetCurrentEmployeeId();
        if (employeeId.HasValue)
        {
            return true;
        }

        errorResult = BadRequest(new { message = "Blacklisting requires a staff actor linked to the current account." });
        return false;
    }

    private sealed record ClientRequestValidationResult(
        bool Success,
        string? NormalizedPhone,
        IReadOnlyList<ClientDocumentUpsertRequest> Documents,
        string? ConflictMessage,
        string? ErrorMessage);
}
