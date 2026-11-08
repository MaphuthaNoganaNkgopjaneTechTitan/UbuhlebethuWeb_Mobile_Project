using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using UbuhlebethuConnectPro.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UbuhlebethuConnectPro.WebApp.Security;
using UbuhlebethuConnectPro.Core.Models.DTOs;

namespace UbuhlebethuConnectPro.WebApp.Controllers.Api
{
    public class LoginRequest
    {
        [Required, StringLength(100)] public string Username { get; set; } = string.Empty;
        [Required, StringLength(200)] public string Password { get; set; } = string.Empty;
    }

    public class RefreshRequest
    {
        [Required] public string Token { get; set; } = string.Empty;
        [Required] public string RefreshToken { get; set; } = string.Empty;
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly IJwtTokenService _tokens;
        private const string TokenProvider = "ConnectProAPI";
        private const string RefreshTokenName = "RefreshToken";

        public AuthApiController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IJwtTokenService tokens)
        {
            _users = users; _signIn = signIn; _tokens = tokens;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _users.FindByNameAsync(req.Username);
            if (user == null) return Unauthorized(new { error = "Invalid credentials." });
            
            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
                return StatusCode(StatusCodes.Status423Locked, new { error = "Account locked or deactivated." });

            var result = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
            if (result.IsLockedOut) return StatusCode(StatusCodes.Status423Locked, new { error = "Account locked or deactivated." });
            if (!result.Succeeded) return Unauthorized(new { error = "Invalid credentials." });

            var roles = await _users.GetRolesAsync(user);
            var jwt = await _tokens.CreateTokenAsync(user);
            var refreshToken = _tokens.GenerateRefreshToken();

            await _users.SetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName, refreshToken);

            var userClaims = await _users.GetClaimsAsync(user);
            bool requiresReset = userClaims.Any(c => c.Type == "RequiresPasswordReset" && c.Value == "true");

            return Ok(new LoginResponse { Token = jwt, RefreshToken = refreshToken, UserName = user.UserName, Roles = roles.ToList(), RequiresPasswordReset = requiresReset });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { error = "Email and password are required." });

            var existing = await _users.FindByEmailAsync(req.Email) ?? await _users.FindByNameAsync(req.Email);
            if (existing != null)
                return Conflict(new { error = "An account with this email address already exists." });

            var user = new ApplicationUser
            {
                UserName = req.Email,
                Email = req.Email,
                EmailConfirmed = true
            };

            var result = await _users.CreateAsync(user, req.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });
            }

            await _users.AddToRoleAsync(user, "Client");

            var jwt = await _tokens.CreateTokenAsync(user);
            var refreshToken = _tokens.GenerateRefreshToken();
            await _users.SetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName, refreshToken);

            return Ok(new LoginResponse
            {
                Token = jwt,
                RefreshToken = refreshToken,
                UserName = user.UserName,
                Roles = new List<string> { "Client" },
                RequiresPasswordReset = false
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(req.Token)) return BadRequest(new { error = "Invalid JWT format." });

            var jwtToken = handler.ReadJwtToken(req.Token);
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId)) return BadRequest(new { error = "Invalid JWT claims." });

            var user = await _users.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            // Identity's SetAuthenticationTokenAsync stores it in AspNetUserTokens. To verify it:
            var storedToken = await _users.GetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName);
            if (storedToken != req.RefreshToken) return Unauthorized(new { error = "Invalid refresh token." });

            // Rotate
            var newJwt = await _tokens.CreateTokenAsync(user);
            var newRefreshToken = _tokens.GenerateRefreshToken();
            await _users.SetAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName, newRefreshToken);

            return Ok(new RefreshResponse { Token = newJwt, RefreshToken = newRefreshToken });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                var user = await _users.FindByIdAsync(userId);
                if (user != null)
                {
                    await _users.RemoveAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName);
                }
            }
            return NoContent();
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] UbuhlebethuConnectPro.Core.Models.DTOs.ChangePasswordRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { error = "Current password and new password are required." });

            if (req.NewPassword.Length < 6)
                return BadRequest(new { error = "New password must be at least 6 characters long." });

            if (req.CurrentPassword == req.NewPassword)
                return BadRequest(new { error = "New password cannot be the same as your temporary password." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                         ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "User identity not found in token." });

            var user = await _users.FindByIdAsync(userId);
            if (user == null) return NotFound(new { error = "User not found." });

            var result = await _users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });
            }

            var claims = await _users.GetClaimsAsync(user);
            var resetClaim = claims.FirstOrDefault(c => c.Type == "RequiresPasswordReset");
            if (resetClaim != null)
            {
                await _users.RemoveClaimAsync(user, resetClaim);
            }

            return Ok(new { message = "Password changed successfully." });
        }
    }
}



