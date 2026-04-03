namespace CarRental.Desktop.Services.Documents;

public interface IContractNumberService
{
    Task<string> NextNumberAsync(CancellationToken cancellationToken = default);
}

