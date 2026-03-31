using CarRental.Desktop.ViewModels;

namespace CarRental.Desktop.Views.Pages;

// Спільна точка для очищення transient state у сторінок, щоб code-behind не дублював однотипні перевірки.
internal static class PageLifecycleUtilities
{
    public static void ReleaseTransientState(object? dataContext)
    {
        if (dataContext is ITransientStateOwner stateOwner)
        {
            stateOwner.ReleaseTransientState();
        }
    }
}
