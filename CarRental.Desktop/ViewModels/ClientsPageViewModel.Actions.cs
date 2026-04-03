using CarRental.Desktop.Models;
using CarRental.Shared.ReferenceData;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace CarRental.Desktop.ViewModels;

public sealed partial class ClientsPageViewModel
{
    private async Task ToggleBlacklistAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            StatusMessage = "Оберіть клієнта.";
            return;
        }

        var client = await _dbContext.Clients.FirstOrDefaultAsync(item => item.Id == SelectedClient.Id);
        if (client is null)
        {
            StatusMessage = "Клієнта не знайдено.";
            return;
        }

        if (client.IsBlacklisted)
        {
            client.IsBlacklisted = false;
            client.BlacklistReason = null;
            client.BlacklistedAtUtc = null;
            client.BlacklistedByEmployeeId = null;
        }
        else
        {
            client.IsBlacklisted = true;
            client.BlacklistReason = "Blocked by staff";
            client.BlacklistedAtUtc = DateTime.UtcNow;
            client.BlacklistedByEmployeeId = _currentEmployee.Id;
        }

        await _dbContext.SaveChangesAsync();
        _refreshCoordinator.Invalidate(PageRefreshArea.Rentals | PageRefreshArea.UserRentals);
        await LoadClientsPageAsync(client.Id);
        StatusMessage = client.IsBlacklisted
            ? "Клієнта додано до чорного списку."
            : "Клієнта прибрано з чорного списку.";
    }

    private async Task DeleteClientAsync()
    {
        StatusMessage = string.Empty;
        if (!_authorizationService.HasPermission(_currentEmployee, EmployeePermission.DeleteRecords))
        {
            StatusMessage = "Недостатньо прав.";
            return;
        }

        if (SelectedClient is null)
        {
            StatusMessage = "Оберіть клієнта.";
            return;
        }

        var hasRentals = await _dbContext.Rentals.AnyAsync(item => item.ClientId == SelectedClient.Id);
        if (hasRentals)
        {
            StatusMessage = "Клієнт має оренди, видалення неможливе.";
            return;
        }

        var client = await _dbContext.Clients
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == SelectedClient.Id);
        if (client is null)
        {
            StatusMessage = "Клієнта не знайдено.";
            return;
        }

        var managedDocumentPaths = client.Documents
            .Where(item => !item.IsDeleted)
            .Select(item => item.StoredPath)
            .Where(path => _clientDocumentStorage.IsManagedStoredPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _dbContext.Clients.Remove(client);
        await _dbContext.SaveChangesAsync();

        foreach (var storedPath in managedDocumentPaths)
        {
            _clientDocumentStorage.TryDeleteManagedDocument(storedPath);
        }

        _refreshCoordinator.Invalidate(PageRefreshArea.Rentals | PageRefreshArea.UserRentals);
        SelectedClient = null;
        PanelMode = ClientPanelMode.Create;
        Editor.Reset();

        if (Clients.Count == 1 && CurrentPage > 1)
        {
            CurrentPage--;
        }

        await LoadClientsPageAsync();
        StatusMessage = "Клієнта видалено.";
    }

    private void BeginCreateClient()
    {
        StatusMessage = string.Empty;
        SelectedClient = null;
        PanelMode = ClientPanelMode.Create;
        Editor.Reset();
    }

    private void BeginCreateClientFromSearch()
    {
        var normalizedPhone = ClientProfileConventions.TryNormalizePhone(SearchText);
        BeginCreateClient();
        Editor.Phone = normalizedPhone ?? SearchText.Trim();
    }

    private void BeginEditSelectedClient()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            StatusMessage = "Оберіть клієнта.";
            return;
        }

        Editor.LoadFrom(SelectedClient);
        PanelMode = ClientPanelMode.Edit;
    }

    private void CancelEditor()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is not null)
        {
            PanelMode = ClientPanelMode.Details;
            Editor.LoadFrom(SelectedClient);
            return;
        }

        PanelMode = ClientPanelMode.Create;
        Editor.Reset();
    }

    private async Task SaveClientAsync()
    {
        StatusMessage = string.Empty;

        var validationError = ValidateEditor();
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            StatusMessage = validationError;
            return;
        }

        var fullName = NormalizeFullName(Editor.FullName);
        var normalizedPhone = ClientProfileConventions.TryNormalizePhone(Editor.Phone)!;
        var passportData = NormalizeDocumentNumber(Editor.PassportData);
        var driverLicense = NormalizeDocumentNumber(Editor.DriverLicense);
        var currentClientId = PanelMode == ClientPanelMode.Edit ? SelectedClient?.Id : null;

        var duplicateBeforeSave = await FindDuplicateMatchAsync(currentClientId, normalizedPhone, passportData, driverLicense);
        if (duplicateBeforeSave is not null)
        {
            await FocusClientAsync(duplicateBeforeSave.ClientId, duplicateBeforeSave.Message);
            return;
        }

        var isCreate = PanelMode != ClientPanelMode.Edit || !currentClientId.HasValue;
        var createdDocumentPaths = new List<string>();
        var replacedDocumentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DuplicateClientMatch? duplicateAfterFailure = null;
        string? failureMessage = null;
        int? savedClientId = null;
        var saveSucceeded = false;

        IsLoading = true;
        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var client = isCreate
                ? new Client()
                : await _dbContext.Clients
                    .Include(item => item.Documents)
                    .FirstOrDefaultAsync(item => item.Id == currentClientId!.Value);
            if (client is null)
            {
                failureMessage = "Клієнта не знайдено.";
                return;
            }

            if (isCreate)
            {
                _dbContext.Clients.Add(client);
            }

            client.FullName = fullName;
            client.Phone = normalizedPhone;

            var passportMutation = ApplyDocumentMetadata(
                client,
                ClientDocumentTypes.Passport,
                passportData,
                Editor.PassportExpirationDate,
                Editor.PassportStoredPath);
            var driverLicenseMutation = ApplyDocumentMetadata(
                client,
                ClientDocumentTypes.DriverLicense,
                driverLicense,
                Editor.DriverLicenseExpirationDate,
                Editor.DriverLicenseStoredPath);

            await _dbContext.SaveChangesAsync();
            savedClientId = client.Id;

            await PersistDocumentFileAsync(
                passportMutation,
                Editor.PassportSourceFilePath,
                client.Id,
                ClientDocumentTypes.Passport,
                createdDocumentPaths,
                replacedDocumentPaths);
            await PersistDocumentFileAsync(
                driverLicenseMutation,
                Editor.DriverLicenseSourceFilePath,
                client.Id,
                ClientDocumentTypes.DriverLicense,
                createdDocumentPaths,
                replacedDocumentPaths);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            saveSucceeded = true;
        }
        catch (DbUpdateException)
        {
            duplicateAfterFailure = await FindDuplicateMatchAsync(currentClientId, normalizedPhone, passportData, driverLicense);
            failureMessage = duplicateAfterFailure is null
                ? "Не вдалося зберегти картку клієнта."
                : null;
        }
        catch (Exception exception)
        {
            failureMessage = exception is InvalidOperationException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileNotFoundException
                ? exception.Message
                : $"Не вдалося зберегти картку клієнта: {exception.Message}";
        }
        finally
        {
            if (!saveSucceeded)
            {
                foreach (var storedPath in createdDocumentPaths)
                {
                    _clientDocumentStorage.TryDeleteManagedDocument(storedPath);
                }
            }

            IsLoading = false;
        }

        if (duplicateAfterFailure is not null)
        {
            await FocusClientAsync(duplicateAfterFailure.ClientId, duplicateAfterFailure.Message);
            return;
        }

        if (!saveSucceeded || !savedClientId.HasValue)
        {
            StatusMessage = failureMessage ?? "Не вдалося зберегти картку клієнта.";
            return;
        }

        foreach (var storedPath in replacedDocumentPaths)
        {
            _clientDocumentStorage.TryDeleteManagedDocument(storedPath);
        }

        PanelMode = ClientPanelMode.Details;
        _refreshCoordinator.Invalidate(PageRefreshArea.Rentals | PageRefreshArea.UserRentals);
        await LoadClientsPageAsync(savedClientId.Value);
        StatusMessage = isCreate
            ? "Клієнта створено. Можна переходити до оформлення оренди."
            : "Картку клієнта оновлено.";
    }

    private async Task OpenRentalsAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedClient is null)
        {
            StatusMessage = "Оберіть клієнта.";
            return;
        }

        if (OpenRentalsRequestedAsync is null)
        {
            StatusMessage = "Перехід до оформлення оренди зараз недоступний.";
            return;
        }

        await OpenRentalsRequestedAsync(SelectedClient.Id);
    }

    private async Task FocusClientAsync(int clientId, string message)
    {
        SetSearchTextSilently(string.Empty);
        await LoadClientsPageAsync(clientId);
        PanelMode = ClientPanelMode.Details;
        StatusMessage = message;
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    private void ClearPassportFileSelection()
        => Editor.PassportSourceFilePath = string.Empty;

    private void ClearDriverLicenseFileSelection()
        => Editor.DriverLicenseSourceFilePath = string.Empty;

    private async Task ChangePageAsync(int delta)
    {
        var nextPage = Math.Max(1, CurrentPage + delta);
        if (nextPage == CurrentPage)
        {
            return;
        }

        CurrentPage = nextPage;
        await RefreshAsync();
    }
}
