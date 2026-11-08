
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/users")]
    [Authorize(AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme)]
    public class UserApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _dbContext;

        private static readonly HashSet<string> DisallowedRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "MobileWorker",
            "Executive",
            "Management"
        };

        public UserApiController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _dbContext = dbContext;
        }

        // =====================================================
        // DTOs
        // =====================================================

        public class UserDto
        {
            public string Id { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public IList<string> Roles { get; set; } =
                new List<string>();
            public bool IsLockedOut { get; set; }
            public DateTimeOffset? LockoutEnd { get; set; }
        }

        public class UserRolesDto
        {
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public IList<string> CurrentRoles { get; set; } =
                new List<string>();
            public IList<string> AvailableRoles { get; set; } =
                new List<string>();
        }

        // =====================================================
        // GET: api/v1/users/workers
        // Registered Field Workers for task assignment
        // =====================================================

        [HttpGet("workers")]
        [Authorize(Roles =
            "Admin,SystemAdmin,SiteManager,Site Manager,Manager,Management")]
        public async Task<ActionResult<IEnumerable<UserDto>>>
            GetWorkers()
        {
            var workerRoleNames = new[]
            {
                "FieldWorker",
                "Field Worker",
                "FIELD_WORKER"
            };

            var workersById =
                new Dictionary<string, ApplicationUser>();

            foreach (var roleName in workerRoleNames)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                    continue;

                var usersInRole =
                    await _userManager.GetUsersInRoleAsync(roleName);

                foreach (var user in usersInRole)
                {
                    workersById[user.Id] = user;
                }
            }

            var results = new List<UserDto>();

            foreach (var user in workersById.Values
                .OrderBy(u => u.UserName))
            {
                if (await _userManager.IsLockedOutAsync(user))
                    continue;

                var roles = await _userManager.GetRolesAsync(user);

                results.Add(new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Roles = roles.ToList(),
                    IsLockedOut = false,
                    LockoutEnd = user.LockoutEnd
                });
            }

            return Ok(results);
        }

        // =====================================================
        // GET: api/v1/users
        // Admin only
        // =====================================================

        [HttpGet("")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<ActionResult<IEnumerable<UserDto>>>
            GetUsers(
                [FromQuery] string? search = null,
                [FromQuery] string? role = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                usersQuery = usersQuery.Where(u =>
                    (u.UserName != null &&
                     u.UserName.Contains(search)) ||
                    (u.Email != null &&
                     u.Email.Contains(search)));
            }

            var users = await usersQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userDtos = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                userDtos.Add(new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    Roles = roles.ToList(),
                    IsLockedOut =
                        user.LockoutEnd.HasValue &&
                        user.LockoutEnd > DateTimeOffset.UtcNow,
                    LockoutEnd = user.LockoutEnd
                });
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                userDtos = userDtos.Where(u =>
                    u.Roles.Any(r =>
                        string.Equals(
                            r,
                            role,
                            StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            return Ok(userDtos);
        }

        // =====================================================
        // GET: api/v1/users/{id}
        // =====================================================

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<ActionResult<UserDto>> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList(),
                IsLockedOut =
                    user.LockoutEnd.HasValue &&
                    user.LockoutEnd > DateTimeOffset.UtcNow,
                LockoutEnd = user.LockoutEnd
            });
        }

        // =====================================================
        // POST: api/v1/users/{id}/deactivate
        // =====================================================

        [HttpPost("{id}/deactivate")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> DeactivateUser(
            string id,
            [FromBody] DeactivateRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            user.LockoutEnd =
                DateTimeOffset.UtcNow.AddYears(100);

            user.LockoutEnabled = true;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActionType = "DeactivateUser",
                PerformedBy = User.Identity?.Name ?? "System",
                TargetUserId = user.Id,
                Reason = request?.Reason,
                Details = $"User {user.UserName} deactivated.",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new MessageResponse
            {
                Message = "User deactivated successfully"
            });
        }

        // =====================================================
        // POST: api/v1/users/{id}/reactivate
        // =====================================================

        [HttpPost("{id}/reactivate")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> ReactivateUser(
            string id,
            [FromBody] ReactivateRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            user.LockoutEnd = null;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActionType = "ReactivateUser",
                PerformedBy = User.Identity?.Name ?? "System",
                TargetUserId = user.Id,
                Reason = request?.Reason,
                Details = $"User {user.UserName} reactivated.",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new MessageResponse
            {
                Message = "User reactivated successfully"
            });
        }

        // =====================================================
        // POST: api/v1/users/{id}/delete
        // =====================================================

        [HttpPost("{id}/delete")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> HardDeleteUser(
            string id,
            [FromBody] DeleteUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var currentAdmin = User.Identity?.Name ?? "System";
            if (string.Equals(user.UserName, currentAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "You cannot delete your own admin account." });
            }

            var username = user.UserName ?? id;
            var email = user.Email ?? "No email";
            var roles = await _userManager.GetRolesAsync(user);

            // 1. Audit log the hard delete
            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActionType = "HardDeleteUser",
                PerformedBy = currentAdmin,
                TargetUserId = user.Id,
                Reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Administrative hard delete" : request.Reason,
                Details = $"Permanently deleted user '{username}' ({email}) with roles: {string.Join(", ", roles)}.",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            // 2. Remove roles to ensure no orphaned role mappings
            if (roles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, roles);
            }

            // 3. Perform hard delete on Identity user
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                user.IsDeleted = true;
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                user.LockoutEnabled = true;
                await _userManager.UpdateAsync(user);
            }

            return Ok(new MessageResponse
            {
                Message = $"User '{username}' was permanently deleted."
            });
        }

        // =====================================================
        // GET: api/v1/users/{id}/roles
        // =====================================================

        [HttpGet("{id}/roles")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<ActionResult<UserRolesDto>>
            GetUserRoles(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var currentRoles =
                await _userManager.GetRolesAsync(user);

            var allRoles = await _roleManager.Roles
                .Select(r => r.Name)
                .ToListAsync();

            return Ok(new UserRolesDto
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                CurrentRoles = currentRoles.ToList(),
                AvailableRoles = allRoles
                    .Where(r => r != null && !DisallowedRoles.Contains(r))
                    .Select(r => r!)
                    .OrderBy(r => r)
                    .ToList()
            });
        }

        // =====================================================
        // POST: api/v1/users/{id}/roles
        // =====================================================

        [HttpPost("{id}/roles")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> UpdateUserRoles(
            string id,
            [FromBody] UpdateUserRolesRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var requestedRoles =
                request.Roles ?? new List<string>();

            var availableRoles = await _roleManager.Roles
                .Where(r => r.Name != null && !DisallowedRoles.Contains(r.Name))
                .Select(r => r.Name!)
                .ToListAsync();
            var availableByName = availableRoles
                .ToDictionary(role => role, StringComparer.OrdinalIgnoreCase);
            var normalizedRoles = requestedRoles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalizedRoles.Any())
            {
                return BadRequest(new { error = "At least one role must be assigned to the user." });
            }

            if (normalizedRoles.Any(role => !availableByName.ContainsKey(role)))
            {
                return BadRequest(new { error = "One or more requested roles do not exist." });
            }

            normalizedRoles = normalizedRoles
                .Select(role => availableByName[role])
                .ToList();

            var currentRoles =
                await _userManager.GetRolesAsync(user);

            var rolesToRemove =
                currentRoles.Except(normalizedRoles, StringComparer.OrdinalIgnoreCase).ToList();

            var rolesToAdd =
                normalizedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

            if (rolesToRemove.Any())
            {
                var removeResult =
                    await _userManager.RemoveFromRolesAsync(
                        user, rolesToRemove);

                if (!removeResult.Succeeded)
                    return BadRequest(new { error = string.Join("; ", removeResult.Errors.Select(e => e.Description)) });
            }

            if (rolesToAdd.Any())
            {
                var addResult =
                    await _userManager.AddToRolesAsync(
                        user, rolesToAdd);

                if (!addResult.Succeeded)
                    return BadRequest(new { error = string.Join("; ", addResult.Errors.Select(e => e.Description)) });
            }

            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActionType = "EditRoles",
                PerformedBy = User.Identity?.Name ?? "System",
                TargetUserId = user.Id,
                Reason = null,
                Details =
                    $"User roles updated to: {string.Join(", ", normalizedRoles)}",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new MessageResponse
            {
                Message = "User roles updated successfully"
            });
        }

        // =====================================================
        // GET: api/v1/users/export
        // =====================================================

        [HttpGet("export")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> ExportUsers(
            [FromQuery] string? search = null,
            [FromQuery] string? role = null)
        {
            var users = await _userManager.Users.ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                users = users.Where(u =>
                    (u.UserName?.Contains(search) ?? false) ||
                    (u.Email?.Contains(search) ?? false))
                    .ToList();
            }

            var userData = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                if (!string.IsNullOrWhiteSpace(role) &&
                    !roles.Contains(role))
                    continue;

                userData.Add(new
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email ?? string.Empty,
                    Roles = string.Join("|", roles),
                    LockoutEnd = user.LockoutEnd?.ToString()
                        ?? string.Empty,
                    CreatedAt = string.Empty
                });
            }

            var json =
                System.Text.Json.JsonSerializer.Serialize(userData);

            var bytes =
                System.Text.Encoding.UTF8.GetBytes(json);

            return File(
                bytes,
                "application/json",
                "users_export.json");
        }

        // =====================================================
        // POST: api/v1/users
        // =====================================================

        [HttpGet("roles")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> GetAvailableRoles()
        {
            var roles = await _roleManager.Roles
                .Select(r => r.Name)
                .ToListAsync();

            var available = roles
                .Where(r => !string.IsNullOrWhiteSpace(r) && !DisallowedRoles.Contains(r))
                .Select(r => r!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r)
                .ToList();

            if (!available.Any())
            {
                available = new List<string> { "Admin", "Client", "FieldWorker", "Manager", "Office", "SiteManager" };
            }

            return Ok(available);
        }

        [HttpPost("")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> CreateUser(
            [FromBody] CreateUserRequest req)
        {
            var user = new ApplicationUser
            {
                UserName = req.UserName,
                Email = req.Email,
                EmailConfirmed = true
            };

            var result =
                await _userManager.CreateAsync(user, req.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            if (req.Roles != null && req.Roles.Any())
            {
                foreach (var role in req.Roles.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    if (DisallowedRoles.Contains(role))
                        continue;

                    if (!await _roleManager.RoleExistsAsync(role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(role));
                    }
                    await _userManager.AddToRoleAsync(user, role);
                }
            }

            var assignedRoles = await _userManager.GetRolesAsync(user);

            return Ok(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Roles = assignedRoles.ToList()
            });
        }

        // =====================================================
        // PUT: api/v1/users/{id}
        // =====================================================

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> UpdateUser(
            string id,
            [FromBody] UpdateUserRequest req)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(req.UserName))
                user.UserName = req.UserName;

            if (!string.IsNullOrWhiteSpace(req.Email))
                user.Email = req.Email;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty
            });
        }

        // =====================================================
        // POST: api/v1/users/{id}/reset-password
        // =====================================================

        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = "Admin,SystemAdmin")]
        public async Task<IActionResult> ResetPassword(
            string id,
            [FromBody] ResetPasswordRequest req)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var token =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            var tempPassword = "TempPassword1!";

            var result =
                await _userManager.ResetPasswordAsync(
                    user, token, tempPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            var existingClaims = await _userManager.GetClaimsAsync(user);
            var resetClaim = existingClaims.FirstOrDefault(c => c.Type == "RequiresPasswordReset");
            if (resetClaim == null)
            {
                await _userManager.AddClaimAsync(
                    user,
                    new Claim("RequiresPasswordReset", "true"));
            }

            _dbContext.AuditLogs.Add(new AuditLog
            {
                ActionType = "ResetPassword",
                PerformedBy = User.Identity?.Name ?? "System",
                TargetUserId = user.Id,
                Reason = req?.Reason,
                Details =
                    $"Password reset initiated for user {user.UserName}.",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new MessageResponse
            {
                Message = tempPassword
            });
        }

        // =====================================================
        // POST: api/v1/users/{id}/change-password
        // =====================================================

        [HttpPost("{id}/change-password")]
        public async Task<IActionResult> ChangePassword(
            string id,
            [FromBody] ChangePasswordRequest req)
        {
            // A user can change only their own password.
            // Administrators must use the reset-password endpoint.
            var authenticatedUserId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(authenticatedUserId))
                return Unauthorized();

            if (authenticatedUserId != id)
                return Forbid();

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var result =
                await _userManager.ChangePasswordAsync(
                    user,
                    req.CurrentPassword,
                    req.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            var claims = await _userManager.GetClaimsAsync(user);

            var resetClaim = claims.FirstOrDefault(
                c => c.Type == "RequiresPasswordReset");

            if (resetClaim != null)
                await _userManager.RemoveClaimAsync(
                    user, resetClaim);

            return Ok(new MessageResponse
            {
                Message = "Password changed successfully."
            });
        }

        // =====================================================
        // Request models
        // =====================================================

        public class ChangePasswordRequest
        {
            public string CurrentPassword { get; set; }
                = string.Empty;

            public string NewPassword { get; set; }
                = string.Empty;
        }

        public class CreateUserRequest
        {
            [System.ComponentModel.DataAnnotations.Required]
            public string UserName { get; set; } = string.Empty;

            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.EmailAddress]
            public string Email { get; set; } = string.Empty;

            [System.ComponentModel.DataAnnotations.Required]
            public string Password { get; set; } = string.Empty;

            public List<string> Roles { get; set; } = new();
        }

        public class UpdateUserRequest
        {
            public string? UserName { get; set; }
            public string? Email { get; set; }
        }

        public class ResetPasswordRequest
        {
            public string? Reason { get; set; }
        }

        public class DeactivateRequest
        {
            public string? Reason { get; set; }
        }

        public class ReactivateRequest
        {
            public string? Reason { get; set; }
        }

        public class DeleteUserRequest
        {
            public string? Reason { get; set; }
        }

        public class UpdateUserRolesRequest
        {
            public IList<string> Roles { get; set; }
                = new List<string>();
        }

        public class MessageResponse
        {
            public string Message { get; set; } = string.Empty;
        }
    }
}
