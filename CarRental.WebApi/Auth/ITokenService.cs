using CarRental.WebApi.Models;

namespace CarRental.WebApi.Auth;

public interface ITokenService
{
    TokenEnvelope Create(Account account, Employee? employee, Client? client, UserRole role);
}

public sealed record TokenEnvelope(string AccessToken, DateTime ExpiresAtUtc);
