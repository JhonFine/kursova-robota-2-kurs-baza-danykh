using CarRental.Desktop.Models;
using CarRental.Shared.ReferenceData;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.IO;

namespace CarRental.Desktop.ViewModels;

public sealed partial class ClientsPageViewModel
{
    private async Task<DuplicateClientMatch?> FindDuplicateMatchAsync(
        int? currentClientId,
        string normalizedPhone,
        string passportData,
        string driverLicense)
    {
        var duplicatePhoneClientId = await _dbContext.Clients
            .AsNoTracking()
            .Where(item => item.Id != currentClientId && item.Phone == normalizedPhone)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync();
        if (duplicatePhoneClientId.HasValue)
        {
            return new DuplicateClientMatch(
                duplicatePhoneClientId.Value,
                "Клієнт з таким телефоном уже існує. Відкриваю наявний профіль.");
        }

        if (!string.IsNullOrWhiteSpace(passportData))
        {
            var passportUpper = passportData.ToUpperInvariant();
            var duplicatePassportClientId = await _dbContext.ClientDocuments
                .AsNoTracking()
                .Where(item =>
                    item.ClientId != currentClientId &&
                    item.DocumentTypeCode == ClientDocumentTypes.Passport &&
                    item.DocumentNumber.ToUpper() == passportUpper)
                .Select(item => (int?)item.ClientId)
                .FirstOrDefaultAsync();
            if (duplicatePassportClientId.HasValue)
            {
                return new DuplicateClientMatch(
                    duplicatePassportClientId.Value,
                    "Клієнт з таким номером паспорта вже існує. Відкриваю наявний профіль.");
            }
        }

        if (!string.IsNullOrWhiteSpace(driverLicense))
        {
            var driverLicenseUpper = driverLicense.ToUpperInvariant();
            var duplicateDriverLicenseClientId = await _dbContext.ClientDocuments
                .AsNoTracking()
                .Where(item =>
                    item.ClientId != currentClientId &&
                    item.DocumentTypeCode == ClientDocumentTypes.DriverLicense &&
                    item.DocumentNumber.ToUpper() == driverLicenseUpper)
                .Select(item => (int?)item.ClientId)
                .FirstOrDefaultAsync();
            if (duplicateDriverLicenseClientId.HasValue)
            {
                return new DuplicateClientMatch(
                    duplicateDriverLicenseClientId.Value,
                    "Клієнт з таким номером посвідчення вже існує. Відкриваю наявний профіль.");
            }
        }

        return null;
    }

    private string? ValidateEditor()
    {
        if (string.IsNullOrWhiteSpace(NormalizeFullName(Editor.FullName)))
        {
            return "Вкажіть ПІБ клієнта.";
        }

        if (ClientProfileConventions.TryNormalizePhone(Editor.Phone) is null)
        {
            return "Вкажіть коректний телефон (10-15 цифр).";
        }

        var passportData = NormalizeDocumentNumber(Editor.PassportData);
        if (string.IsNullOrWhiteSpace(passportData) &&
            (Editor.PassportExpirationDate.HasValue || Editor.HasPendingPassportSourceFile))
        {
            return "Вкажіть номер паспорта перед додаванням дати або файла.";
        }

        var driverLicense = NormalizeDocumentNumber(Editor.DriverLicense);
        if (string.IsNullOrWhiteSpace(driverLicense) &&
            (Editor.DriverLicenseExpirationDate.HasValue || Editor.HasPendingDriverLicenseSourceFile))
        {
            return "Вкажіть номер посвідчення перед додаванням дати або файла.";
        }

        if (!string.IsNullOrWhiteSpace(Editor.PassportSourceFilePath) &&
            !File.Exists(Editor.PassportSourceFilePath))
        {
            return "Вибраний файл паспорта не знайдено.";
        }

        if (!string.IsNullOrWhiteSpace(Editor.DriverLicenseSourceFilePath) &&
            !File.Exists(Editor.DriverLicenseSourceFilePath))
        {
            return "Вибраний файл посвідчення не знайдено.";
        }

        return null;
    }

    private DocumentMutation ApplyDocumentMetadata(
        Client client,
        string documentTypeCode,
        string documentNumber,
        DateTime? expirationDate,
        string? storedPath)
    {
        var existingDocument = client.Documents
            .FirstOrDefault(item =>
                !item.IsDeleted &&
                string.Equals(item.DocumentTypeCode, documentTypeCode, StringComparison.OrdinalIgnoreCase));
        var normalizedStoredPath = NormalizeStoredPath(storedPath);
        var hasAnyData = !string.IsNullOrWhiteSpace(documentNumber) ||
                         expirationDate.HasValue ||
                         !string.IsNullOrWhiteSpace(normalizedStoredPath);

        if (!hasAnyData)
        {
            if (existingDocument is not null)
            {
                existingDocument.IsDeleted = true;
                existingDocument.UpdatedAtUtc = DateTime.UtcNow;
            }

            return new DocumentMutation(
                null,
                existingDocument?.StoredPath);
        }

        if (existingDocument is null)
        {
            existingDocument = new ClientDocument
            {
                Client = client,
                DocumentTypeCode = documentTypeCode
            };
            client.Documents.Add(existingDocument);
        }

        existingDocument.IsDeleted = false;
        existingDocument.DocumentTypeCode = documentTypeCode;
        existingDocument.DocumentNumber = documentNumber;
        existingDocument.ExpirationDate = expirationDate?.Date;
        existingDocument.StoredPath = normalizedStoredPath;
        existingDocument.UpdatedAtUtc = DateTime.UtcNow;

        return new DocumentMutation(existingDocument, existingDocument.StoredPath);
    }

    private Task PersistDocumentFileAsync(
        DocumentMutation mutation,
        string sourceFilePath,
        int clientId,
        string documentTypeCode,
        ICollection<string> createdDocumentPaths,
        ISet<string> replacedDocumentPaths)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || mutation.Document is null)
        {
            return Task.CompletedTask;
        }

        var newStoredPath = _clientDocumentStorage.SaveDocumentCopy(sourceFilePath, clientId, documentTypeCode);
        createdDocumentPaths.Add(newStoredPath);

        if (_clientDocumentStorage.IsManagedStoredPath(mutation.PreviousStoredPath) &&
            !string.Equals(mutation.PreviousStoredPath, newStoredPath, StringComparison.OrdinalIgnoreCase))
        {
            replacedDocumentPaths.Add(mutation.PreviousStoredPath!);
        }

        mutation.Document.StoredPath = newStoredPath;
        mutation.Document.IsDeleted = false;
        mutation.Document.UpdatedAtUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    private void NotifySelectionStateChanged()
    {
        ToggleBlacklistCommand.NotifyCanExecuteChanged();
        DeleteClientCommand.NotifyCanExecuteChanged();
        EditClientCommand.NotifyCanExecuteChanged();
        OpenRentalsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowClientDetails));
        OnPropertyChanged(nameof(ShowEditorPanel));
        OnPropertyChanged(nameof(BlacklistButtonText));
        OnPropertyChanged(nameof(RightPanelHeader));
    }

    private void NotifySearchStateChanged()
    {
        QuickCreateFromSearchCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowQuickCreateFromSearch));
        OnPropertyChanged(nameof(QuickCreateFromSearchText));
    }

    private void Editor_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SaveClientCommand.NotifyCanExecuteChanged();
        ClearPassportSelectionCommand.NotifyCanExecuteChanged();
        ClearDriverLicenseSelectionCommand.NotifyCanExecuteChanged();
    }

    private bool CanMovePrevious() => !IsLoading && CurrentPage > 1;

    private bool CanMoveNext()
    {
        if (IsLoading)
        {
            return false;
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalClients, 0) / PageSize));
        return CurrentPage < totalPages;
    }

    private bool CanManageSelectedClient() => !IsLoading && SelectedClient is not null;

    private bool CanQuickCreateFromSearch()
        => !IsLoading && ClientProfileConventions.TryNormalizePhone(SearchText) is not null && Clients.Count == 0;

    private void SetSearchTextSilently(string value)
    {
        var normalized = value ?? string.Empty;
        if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _searchText = normalized;
        OnPropertyChanged(nameof(SearchText));
        NotifySearchStateChanged();
    }

    private static string FormatPhoneForDisplay(string? value)
        => ClientProfileConventions.TryNormalizePhone(value) ?? "Не вказано";

    private static string NormalizeFullName(string? value)
        => string.Join(' ', (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeDocumentNumber(string? value)
        => (value ?? string.Empty).Trim();

    private static string? NormalizeStoredPath(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record DuplicateClientMatch(int ClientId, string Message);

    private sealed record DocumentMutation(ClientDocument? Document, string? PreviousStoredPath);
}
