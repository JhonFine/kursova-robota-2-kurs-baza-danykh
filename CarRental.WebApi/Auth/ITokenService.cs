using CarRental.WebApi.Models;

namespace CarRental.WebApi.Auth;

public interface ITokenService
{
    TokenEnvelope Create(Employee employee);
}

public sealed record TokenEnvelope(string AccessToken, DateTime ExpiresAtUtc);
