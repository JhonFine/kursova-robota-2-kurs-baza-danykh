namespace CarRental.Desktop.ViewModels;

public interface ITransientStateOwner
{
    void ReleaseTransientState();
}
