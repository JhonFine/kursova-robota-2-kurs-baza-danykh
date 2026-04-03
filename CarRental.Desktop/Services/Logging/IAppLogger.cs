namespace CarRental.Desktop.Services.Logging;

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}

