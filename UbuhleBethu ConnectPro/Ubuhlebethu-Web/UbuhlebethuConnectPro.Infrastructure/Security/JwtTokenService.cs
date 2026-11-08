using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using UbuhlebethuConnectPro.Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UbuhlebethuConnectPro.Core.Options;

namespace UbuhlebethuConnectPro.WebApp.Security
{
    public interface IJwtTokenService
    {
        Task<string> CreateTokenAsync(ApplicationUser user);
        string GenerateRefreshToken();
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _opts;
        private readonly UserManager<ApplicationUser> _users;

        public JwtTokenService(IOptions<JwtOptions> opts, UserManager<ApplicationUser> users)
        {
            _opts = opts.Value; _users = users;
        }

        public async Task<string> CreateTokenAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            foreach (var role in await _users.GetRolesAsync(user)) claims.Add(new Claim(ClaimTypes.Role, role));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
            var token = new JwtSecurityToken(_opts.Issuer, _opts.Audience, claims,
                expires: DateTime.UtcNow.AddMinutes(_opts.ExpiryMinutes),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}

