using System.Collections.Concurrent;

namespace CarRental.Desktop.ViewModels;

// Координатор refresh розриває прямі залежності між сторінками:
// зміна в одному модулі лише інвалідовує area, а фактичне перезавантаження відбувається при відкритті сторінки.
public sealed class PageRefreshCoordinator(Func<CancellationToken, Task> refreshRentalStatusesAsync)
{
    private readonly ConcurrentDictionary<PageRefreshArea, IPageDataLifecycle> _pages = new();
    private readonly SemaphoreSlim _rentalStatusLock = new(1, 1);
    private bool _rentalStatusesInitialized;

    public void Register(PageRefreshArea area, IPageDataLifecycle page)
    {
        _pages[area] = page;
    }

    public async Task EnsurePageDataAsync(
        PageRefreshArea area,
        IPageDataLifecycle page,
        CancellationToken cancellationToken = default)
    {
        // Орендні статуси впливають одразу на staff rentals, self-service каталог і історію клієнта.
        if ((area & (PageRefreshArea.Rentals | PageRefreshArea.Prokat | PageRefreshArea.UserRentals)) != 0)
        {
            await EnsureRentalStatusesAsync(cancellationToken);
        }

        await page.EnsureDataAsync();
    }

    public void Invalidate(PageRefreshArea areas)
    {
        if (areas == PageRefreshArea.None)
        {
            return;
        }

        foreach (var entry in _pages)
        {
            if ((areas & entry.Key) != 0)
            {
                entry.Value.InvalidateData();
            }
        }
    }

    public async Task EnsureRentalStatusesAsync(CancellationToken cancellationToken = default)
    {
        if (_rentalStatusesInitialized)
        {
            return;
        }

        // Перший виклик прогріває стан availability; усі наступні проходять без дублювання важкого sync.
        await _rentalStatusLock.WaitAsync(cancellationToken);
        try
        {
            if (_rentalStatusesInitialized)
            {
                return;
            }

            await refreshRentalStatusesAsync(cancellationToken);
            _rentalStatusesInitialized = true;
        }
        finally
        {
            _rentalStatusLock.Release();
        }
    }
}
