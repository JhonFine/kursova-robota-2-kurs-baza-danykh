namespace CarRental.Desktop.ViewModels;

public abstract class PageDataViewModelBase : ViewModelBase, IPageDataLifecycle
{
    private bool _isInitialized;
    private bool _needsRefresh = true;

    public bool IsInitialized
    {
        get => _isInitialized;
        private set => SetProperty(ref _isInitialized, value);
    }

    public bool NeedsRefresh
    {
        get => _needsRefresh;
        private set => SetProperty(ref _needsRefresh, value);
    }

    public Task EnsureDataAsync()
    {
        if (IsInitialized && !NeedsRefresh)
        {
            return Task.CompletedTask;
        }

        return RefreshAsync();
    }

    public void InvalidateData()
    {
        NeedsRefresh = true;
    }

    protected void MarkDataLoaded()
    {
        IsInitialized = true;
        NeedsRefresh = false;
    }

    public abstract Task RefreshAsync();
}

