namespace CarRental.WebApi.Services.Documents;

public interface IContractNumberService
{
    Task<string> NextNumberAsync(CancellationToken cancellationToken = default);
}

