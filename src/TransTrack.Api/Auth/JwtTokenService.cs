using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TransTrack.Core;

namespace TransTrack.Api.Auth;

/// <summary>
/// Issues the three token shapes described in <see cref="TokenTypes"/>.
/// Mirrors <c>LoginViewModel.Stage</c> on the desktop — a restricted token
/// only ever authorizes the one or two endpoints its stage allows.
/// </summary>
public class JwtTokenService(IConfiguration config)
{
    public TimeSpan FullSessionLifetime { get; } =
        TimeSpan.FromHours(double.Parse(config["Jwt:FullSessionHours"] ?? "12"));

    private static readonly TimeSpan RestrictedLifetime = TimeSpan.FromMinutes(15);

    public string CreateFullSessionToken(User user) => Create(FullSessionLifetime,
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim(TokenTypes.ClaimType, TokenTypes.Full),
        new Claim(TokenTypes.CompanyIdClaimType, user.CompanyId.ToString()));

    public string CreateChangePasswordToken(Guid userId) => Create(RestrictedLifetime,
        new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new Claim(TokenTypes.ClaimType, TokenTypes.ChangePassword));

    public string CreateRecoveryToken() => Create(RestrictedLifetime,
        new Claim(TokenTypes.ClaimType, TokenTypes.Recovery));

    private string Create(TimeSpan lifetime, params Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
