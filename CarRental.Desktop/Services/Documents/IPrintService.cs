namespace CarRental.Desktop.Services.Documents;

public interface IPrintService
{
    bool TryPrint(string filePath, out string message);
}
