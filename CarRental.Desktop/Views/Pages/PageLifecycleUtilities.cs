using CarRental.Desktop.ViewModels;

namespace CarRental.Desktop.Views.Pages;

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
