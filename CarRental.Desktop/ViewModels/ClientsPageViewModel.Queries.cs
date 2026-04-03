using CarRental.Shared.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.ViewModels;

public sealed partial class ClientsPageViewModel
{
    private async Task LoadClientsPageAsync(int? preferredClientId = null, CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var trimmedSearch = SearchText.Trim();
            var query = BuildClientListQuery(trimmedSearch);

            TotalClients = await query.CountAsync(cancellationToken);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)Math.Max(TotalClients, 0) / PageSize));
            if (preferredClientId.HasValue)
            {
                var preferredPage = await ResolvePageForClientAsync(preferredClientId.Value, trimmedSearch, cancellationToken);
                CurrentPage = preferredPage.HasValue
                    ? preferredPage.Value
                    : Math.Min(CurrentPage, totalPages);
            }
            else if (CurrentPage > totalPages)
            {
                CurrentPage = totalPages;
            }

            var rows = await query
                .OrderBy(item => item.FullName)
                .ThenBy(item => item.Id)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync(cancellationToken);

            Clients.Clear();
            foreach (var row in rows)
            {
                Clients.Add(new ClientRow(
                    row.Id,
                    row.AccountId,
                    row.FullName,
                    FormatPhoneForDisplay(row.Phone),
                    row.PassportData,
                    row.PassportExpirationDate,
                    row.PassportStoredPath,
                    row.DriverLicense,
                    row.DriverLicenseExpirationDate,
                    row.DriverLicenseStoredPath,
                    row.IsBlacklisted));
            }

            var preferredId = preferredClientId ?? SelectedClient?.Id;
            if (preferredId.HasValue)
            {
                SelectedClient = Clients.FirstOrDefault(item => item.Id == preferredId.Value);
            }
            else if (SelectedClient is not null && Clients.All(item => item.Id != SelectedClient.Id))
            {
                SelectedClient = null;
            }

            NotifySearchStateChanged();
            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private IQueryable<ClientListProjection> BuildClientListQuery(string searchText)
    {
        var passportDocuments = _dbContext.ClientDocuments
            .AsNoTracking()
            .Where(item => item.DocumentTypeCode == ClientDocumentTypes.Passport);
        var driverLicenseDocuments = _dbContext.ClientDocuments
            .AsNoTracking()
            .Where(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense);

        var query = _dbContext.Clients
            .AsNoTracking()
            .Select(client => new ClientListProjection
            {
                Id = client.Id,
                AccountId = client.AccountId,
                FullName = client.FullName,
                Phone = client.Phone,
                PassportData = passportDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.DocumentNumber)
                    .FirstOrDefault() ?? string.Empty,
                PassportExpirationDate = passportDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.ExpirationDate)
                    .FirstOrDefault(),
                PassportStoredPath = passportDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.StoredPath)
                    .FirstOrDefault(),
                DriverLicense = driverLicenseDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.DocumentNumber)
                    .FirstOrDefault() ?? string.Empty,
                DriverLicenseExpirationDate = driverLicenseDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.ExpirationDate)
                    .FirstOrDefault(),
                DriverLicenseStoredPath = driverLicenseDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.StoredPath)
                    .FirstOrDefault(),
                IsBlacklisted = client.IsBlacklisted
            });

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        var digits = string.Concat(searchText.Where(char.IsDigit));
        var hasDigits = !string.IsNullOrWhiteSpace(digits);

        return query.Where(client =>
            client.FullName.Contains(searchText) ||
            client.Phone.Contains(searchText) ||
            client.PassportData.Contains(searchText) ||
            client.DriverLicense.Contains(searchText) ||
            (hasDigits && client.Phone.Contains(digits)));
    }

    private async Task<int?> ResolvePageForClientAsync(int clientId, string searchText, CancellationToken cancellationToken)
    {
        var orderedIds = await BuildClientListQuery(searchText)
            .OrderBy(item => item.FullName)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var index = orderedIds.IndexOf(clientId);
        return index >= 0
            ? index / PageSize + 1
            : null;
    }

    private async Task QueueSearchRefreshAsync()
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();

        var cancellationTokenSource = new CancellationTokenSource();
        _searchCancellationTokenSource = cancellationTokenSource;

        try
        {
            await Task.Delay(220, cancellationTokenSource.Token);
            CurrentPage = 1;
            await RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            // Ignore outdated search requests.
        }
    }

    private sealed class ClientListProjection
    {
        public int Id { get; init; }

        public int? AccountId { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Phone { get; init; } = string.Empty;

        public string PassportData { get; init; } = string.Empty;

        public DateTime? PassportExpirationDate { get; init; }

        public string? PassportStoredPath { get; init; }

        public string DriverLicense { get; init; } = string.Empty;

        public DateTime? DriverLicenseExpirationDate { get; init; }

        public string? DriverLicenseStoredPath { get; init; }

        public bool IsBlacklisted { get; init; }
    }
}
