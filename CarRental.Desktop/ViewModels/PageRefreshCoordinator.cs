using System.Collections.Concurrent;

namespace CarRental.Desktop.ViewModels;

// РљРѕРѕСЂРґРёРЅР°С‚РѕСЂ refresh СЂРѕР·СЂРёРІР°С” РїСЂСЏРјС– Р·Р°Р»РµР¶РЅРѕСЃС‚С– РјС–Р¶ СЃС‚РѕСЂС–РЅРєР°РјРё:
// Р·РјС–РЅР° РІ РѕРґРЅРѕРјСѓ РјРѕРґСѓР»С– Р»РёС€Рµ С–РЅРІР°Р»С–РґРѕРІСѓС” area, Р° С„Р°РєС‚РёС‡РЅРµ РїРµСЂРµР·Р°РІР°РЅС‚Р°Р¶РµРЅРЅСЏ РІС–РґР±СѓРІР°С”С‚СЊСЃСЏ РїСЂРё РІС–РґРєСЂРёС‚С‚С– СЃС‚РѕСЂС–РЅРєРё.
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
        // РћСЂРµРЅРґРЅС– СЃС‚Р°С‚СѓСЃРё РІРїР»РёРІР°СЋС‚СЊ РѕРґСЂР°Р·Сѓ РЅР° staff rentals, self-service РєР°С‚Р°Р»РѕРі С– С–СЃС‚РѕСЂС–СЋ РєР»С–С”РЅС‚Р°.
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

        // РџРµСЂС€РёР№ РІРёРєР»РёРє РїСЂРѕРіСЂС–РІР°С” СЃС‚Р°РЅ availability; СѓСЃС– РЅР°СЃС‚СѓРїРЅС– РїСЂРѕС…РѕРґСЏС‚СЊ Р±РµР· РґСѓР±Р»СЋРІР°РЅРЅСЏ РІР°Р¶РєРѕРіРѕ sync.
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

