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

[Authorize]
[Route("api/profile")]
public sealed class ProfileController(RentalDbContext dbContext) : ApiControllerBase
{
    [HttpGet("client")]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    // Профіль повертається для "поточного клієнта", а не за довільним id:
    // якщо claim clientId відсутній, ще є fallback через accountId поточного користувача.
    public async Task<IActionResult> GetClientProfile(CancellationToken cancellationToken)
    {
        var client = await GetCurrentClientAsync(cancellationToken);
        if (client is null)
        {
            return GetCurrentAccountId().HasValue ? NotFound() : Unauthorized();
        }

        return Ok(client.ToProfileDto(ClientProfileRules.IsProfileComplete(client)));
    }

    [HttpPut("client")]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    // Self-service не дає редагувати довільний набір полів напряму в Client.Documents:
    // спочатку валідовуємо profile payload, а потім зводимо його до canonical document model.
    public async Task<IActionResult> UpdateClientProfile(
        [FromBody] UpdateClientProfileRequest request,
        CancellationToken cancellationToken)
    {
        var client = await GetCurrentClientAsync(cancellationToken);
        if (client is null)
        {
            return GetCurrentAccountId().HasValue ? NotFound() : Unauthorized();
        }

        var validation = await ValidateProfileRequestAsync(client.Id, request, cancellationToken);
        if (!validation.Success)
        {
            if (!string.IsNullOrWhiteSpace(validation.ConflictMessage))
            {
                return Conflict(new { message = validation.ConflictMessage });
            }

            return BadRequest(new { message = validation.ErrorMessage ?? "Не вдалося перевірити дані профілю." });
        }

        client.FullName = request.FullName.Trim();
        client.Phone = validation.NormalizedPhone!;
        ClientProfileRules.UpsertDocuments(client, validation.Documents);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "Номер документа або телефон уже існує в системі." });
        }

        return Ok(client.ToProfileDto(ClientProfileRules.IsProfileComplete(client)));
    }

    [HttpPost("client/documents/{documentType}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProtectedDocumentStorage.MaxFileSizeBytes)]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    // Завантаження фото документа оновлює лише один активний документ потрібного типу
    // і після успішного save прибирає попередній файл зі storage.
    public async Task<IActionResult> UploadClientDocumentPhoto(
        string documentType,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var client = await GetCurrentClientAsync(cancellationToken);
        if (client is null)
        {
            return GetCurrentAccountId().HasValue ? NotFound() : Unauthorized();
        }

        if (!TryResolveClientDocumentCode(documentType, out var documentTypeCode, out var storageDocumentType))
        {
            return BadRequest(new { message = "Unknown document type. Use passport-photo or driver-license-photo." });
        }

        var storeResult = await ProtectedDocumentStorage.StoreDocumentPhotoAsync(
            file,
            client.Id,
            storageDocumentType,
            cancellationToken);
        if (!storeResult.Success || string.IsNullOrWhiteSpace(storeResult.StoredPath))
        {
            return BadRequest(new { message = storeResult.ErrorMessage ?? "Failed to store document photo." });
        }

        var previousPath = ClientProfileRules.GetActiveDocument(client, documentTypeCode)?.StoredPath;

        ClientProfileRules.UpsertDocuments(
            client,
            new[]
            {
                new ClientDocumentUpsertRequest
                {
                    DocumentTypeCode = ClientDocumentTypes.Passport,
                    DocumentNumber = client.PassportData,
                    ExpirationDate = client.PassportExpirationDate,
                    StoredPath = documentTypeCode == ClientDocumentTypes.Passport ? storeResult.StoredPath : client.PassportPhotoPath
                },
                new ClientDocumentUpsertRequest
                {
                    DocumentTypeCode = ClientDocumentTypes.DriverLicense,
                    DocumentNumber = client.DriverLicense,
                    ExpirationDate = client.DriverLicenseExpirationDate,
                    StoredPath = documentTypeCode == ClientDocumentTypes.DriverLicense ? storeResult.StoredPath : client.DriverLicensePhotoPath
                }
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        ProtectedDocumentStorage.TryDeleteStoredPhoto(previousPath);

        return Ok(client.ToProfileDto(ClientProfileRules.IsProfileComplete(client)));
    }

    [HttpGet("client/documents/{documentType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientDocumentPhoto(string documentType, CancellationToken cancellationToken)
    {
        var client = await GetCurrentClientAsync(cancellationToken);
        if (client is null)
        {
            return GetCurrentAccountId().HasValue ? NotFound() : Unauthorized();
        }

        if (!TryResolveClientDocumentCode(documentType, out var documentTypeCode, out _))
        {
            return NotFound();
        }

        var storedPath = ClientProfileRules.GetActiveDocument(client, documentTypeCode)?.StoredPath;
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

    private async Task<Client?> GetCurrentClientAsync(CancellationToken cancellationToken)
    {
        var clientId = GetCurrentClientId();
        var accountId = GetCurrentAccountId();
        if (!clientId.HasValue && !accountId.HasValue)
        {
            return null;
        }

        var query = dbContext.Clients
            .Include(item => item.Documents)
            .AsQueryable();

        if (clientId.HasValue)
        {
            return await query.FirstOrDefaultAsync(item => item.Id == clientId.Value, cancellationToken);
        }

        return await query.FirstOrDefaultAsync(item => item.AccountId == accountId!.Value, cancellationToken);
    }

    private async Task<ProfileRequestValidationResult> ValidateProfileRequestAsync(
        int clientId,
        UpdateClientProfileRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = ClientProfileRules.TryNormalizePhone(request.Phone);

        if (string.IsNullOrWhiteSpace(request.FullName.Trim()))
        {
            return new ProfileRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, "Вкажіть ПІБ.");
        }

        if (normalizedPhone is null)
        {
            return new ProfileRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, "Вкажіть коректний номер телефону у форматі +380671234567. Дозволено 10-15 цифр.");
        }

        if (!TryNormalizeDocuments(request.ResolveDocuments(), out var documents, out var errorMessage))
        {
            return new ProfileRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, errorMessage);
        }

        var driverLicenseDocument = documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense);
        if (driverLicenseDocument is null || string.IsNullOrWhiteSpace(driverLicenseDocument.DocumentNumber))
        {
            return new ProfileRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, "Вкажіть номер посвідчення водія.");
        }

        if (!driverLicenseDocument.ExpirationDate.HasValue)
        {
            return new ProfileRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, "Вкажіть дату, до якої чинне посвідчення водія.");
        }

        if (driverLicenseDocument.ExpirationDate.Value.Date < DateTime.UtcNow.Date)
        {
            return new ProfileRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), null, "Посвідчення водія має бути чинним на сьогодні або пізніше.");
        }

        foreach (var document in documents)
        {
            var duplicateExists = await dbContext.ClientDocuments
                .IgnoreQueryFilters()
                .AnyAsync(
                    item => !item.IsDeleted &&
                            item.ClientId != clientId &&
                            item.DocumentTypeCode == document.DocumentTypeCode &&
                            item.DocumentNumber == document.DocumentNumber,
                    cancellationToken);
            if (duplicateExists)
            {
                return new ProfileRequestValidationResult(
                    false,
                    null,
                    Array.Empty<ClientDocumentUpsertRequest>(),
                    document.DocumentTypeCode == ClientDocumentTypes.Passport
                        ? "Такий номер паспорта вже використовується іншим клієнтом."
                        : "Таке посвідчення водія вже використовується іншим клієнтом.",
                    null);
            }
        }

        var phoneExists = await dbContext.Clients
            .AnyAsync(item => item.Id != clientId && item.Phone == normalizedPhone, cancellationToken);
        if (phoneExists)
        {
            return new ProfileRequestValidationResult(false, null, Array.Empty<ClientDocumentUpsertRequest>(), "Цей номер телефону вже використовується іншим клієнтом.", null);
        }

        return new ProfileRequestValidationResult(true, normalizedPhone, documents, null, null);
    }

    private static bool TryNormalizeDocuments(
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
                errorMessage = $"Непідтримуваний тип документа '{request.DocumentTypeCode}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.DocumentNumber))
            {
                errorMessage = documentTypeCode == ClientDocumentTypes.Passport
                    ? "Вкажіть номер паспорта."
                    : "Вкажіть номер посвідчення водія.";
                return false;
            }

            if (!ProtectedDocumentStorage.TryNormalizeStoredPhotoPath(request.StoredPath, out var storedPath))
            {
                errorMessage = "Некоректний шлях до фото документа. Дозволені лише файли із захищеного сховища документів.";
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

    private sealed record ProfileRequestValidationResult(
        bool Success,
        string? NormalizedPhone,
        IReadOnlyList<ClientDocumentUpsertRequest> Documents,
        string? ConflictMessage,
        string? ErrorMessage);
}
