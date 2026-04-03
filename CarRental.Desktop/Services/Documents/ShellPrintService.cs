using System.Diagnostics;
using System.IO;

namespace CarRental.Desktop.Services.Documents;

public sealed class ShellPrintService : IPrintService
{
    public bool TryPrint(string filePath, out string message)
    {
        if (!File.Exists(filePath))
        {
            message = "Файл не знайдено.";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                Verb = "print",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            Process.Start(psi);
            message = "Команду друку передано в ОС.";
            return true;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return false;
        }
    }
}

