using CarRental.Desktop.ViewModels;

namespace CarRental.Desktop.Views.Pages;

// РЎРїС–Р»СЊРЅР° С‚РѕС‡РєР° РґР»СЏ РѕС‡РёС‰РµРЅРЅСЏ transient state Сѓ СЃС‚РѕСЂС–РЅРѕРє, С‰РѕР± code-behind РЅРµ РґСѓР±Р»СЋРІР°РІ РѕРґРЅРѕС‚РёРїРЅС– РїРµСЂРµРІС–СЂРєРё.
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

