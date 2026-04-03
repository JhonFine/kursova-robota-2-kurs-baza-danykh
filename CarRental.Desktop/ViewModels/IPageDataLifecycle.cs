namespace CarRental.Desktop.ViewModels;

public interface IPageDataLifecycle
{
    bool IsInitialized { get; }

    bool NeedsRefresh { get; }

    Task EnsureDataAsync();

    void InvalidateData();
}

