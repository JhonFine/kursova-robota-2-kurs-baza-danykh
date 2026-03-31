using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarRental.WebApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarRental.WebApi.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TokenEnvelope Create(Account account, Employee? employee, Client? client, UserRole role)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var displayName = employee?.FullName ?? client?.FullName ?? account.Login;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new("account_id", account.Id.ToString()),
            new(ClaimTypes.Name, account.Login),
            new("full_name", displayName),
            new(ClaimTypes.Role, role.ToString())
        };

        if (employee is not null)
        {
            claims.Add(new Claim("employee_id", employee.Id.ToString()));
        }

        if (client is not null)
        {
            claims.Add(new Claim("client_id", client.Id.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenEnvelope(encoded, expiresAt);
    }
}
