using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize(Roles = "Admin")]

    public class AdminController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        private static readonly HashSet<string> DisallowedRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "MobileWorker",
            "Executive",
            "Management"
        };


        public AdminController(IConnectProApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // Enhanced index with search and role filter
        public async Task<IActionResult> Index(string? search, string? role)
        {
            var userDtos = await _apiClient.GetUsersAsync(search, role, 1, int.MaxValue);
            
            var model = new List<AdminUserViewModel>();
            foreach (var dto in userDtos)
            {
                model.Add(new AdminUserViewModel { 
                    Id = dto.Id, 
                    UserName = dto.UserName, 
                    Email = dto.Email, 
                    Roles = dto.Roles 
                });
            }

            ViewBag.Search = search;
            ViewBag.Role = role;
            // Removed rolemanager, hardcoded some default roles or just leave empty if it's dynamic from API. But since I can't easily fetch all roles without an endpoint, I will just provide a static list or empty list.
            ViewBag.AvailableRoles = new List<string> { "Admin", "Client", "Office", "Manager", "SiteManager", "FieldWorker" };
            return View(model);
        }

        // Export users CSV
        public async Task<IActionResult> ExportUsers()
        {
            var userDtos = await _apiClient.GetUsersAsync(null, null, 1, int.MaxValue);
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,UserName,Email,Roles,LockoutEnd");
            foreach (var dto in userDtos)
            {
                var rolesCsv = string.Join("|", dto.Roles);
                var lockout = dto.IsLockedOut ? "9999-12-31 23:59:59Z" : string.Empty;
                sb.AppendLine($"\"{dto.Id}\",\"{dto.UserName}\",\"{dto.Email}\",\"{rolesCsv}\",\"{lockout}\"");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "users_export.csv");
        }

        // Create user with role assignment
        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var availableRoles = await _apiClient.GetAvailableRolesAsync();
            var vm = new CreateUserViewModel
            {
                Roles = availableRoles
                    .Select(r => new RoleCheckbox { RoleName = r, Selected = r == "FieldWorker" })
                    .ToList()
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                if (model.Roles == null || !model.Roles.Any())
                {
                    var availableRoles = await _apiClient.GetAvailableRolesAsync();
                    model.Roles = availableRoles.Select(r => new RoleCheckbox { RoleName = r }).ToList();
                }
                return View(model);
            }

            try
            {
                var selectedRoles = model.Roles?
                    .Where(r => r.Selected)
                    .Select(r => r.RoleName)
                    .ToList() ?? new List<string>();

                var request = new UbuhlebethuConnectPro.WebApp.ApiClient.CreateUserRequest
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    Password = model.Password,
                    Roles = selectedRoles
                };

                await _apiClient.CreateUserAsync(request);
                TempData["Success"] = $"User '{model.UserName}' created successfully with assigned role(s).";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to create user: " + ex.Message);
                if (model.Roles == null || !model.Roles.Any())
                {
                    var availableRoles = await _apiClient.GetAvailableRolesAsync();
                    model.Roles = availableRoles.Select(r => new RoleCheckbox { RoleName = r }).ToList();
                }
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "The user could not be identified.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var user = await _apiClient.GetUserAsync(id);
                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                var roleInfo = await _apiClient.GetUserRolesAsync(user.Id);
                var rolesModel = CreateEditRolesModel(user.Id, user.UserName, roleInfo);

                var vm = new EditUserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Roles = rolesModel.Roles,
                    StaleRoles = rolesModel.StaleRoles
                };
                ViewBag.UserId = id;
                return View(vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load user: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, EditUserViewModel model)
        {
            if (string.IsNullOrWhiteSpace(id)) id = model?.Id ?? string.Empty;
            if (model != null) model.Id = id;
            ViewBag.UserId = id;

            if (model == null || string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "The user could not be identified.";
                return RedirectToAction(nameof(Index));
            }

            var selectedRoles = (model.Roles ?? new List<RoleCheckbox>())
                .Where(r => r.Selected && !string.IsNullOrWhiteSpace(r.RoleName))
                .Select(r => r.RoleName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!selectedRoles.Any())
            {
                ModelState.AddModelError(string.Empty, "At least one role must be assigned to the user.");
            }

            if (!ModelState.IsValid)
            {
                await RepopulateEditUserModelAsync(id, model, selectedRoles);
                return View(model);
            }

            try
            {
                // 1. Update User basic info
                await _apiClient.UpdateUserAsync(id, new UbuhlebethuConnectPro.WebApp.ApiClient.UpdateUserRequest
                {
                    UserName = model.UserName,
                    Email = model.Email
                });

                // 2. Update User roles
                await _apiClient.UpdateUserRolesAsync(id, new UbuhlebethuConnectPro.WebApp.ApiClient.UpdateUserRolesRequest
                {
                    Roles = selectedRoles
                });

                TempData["Success"] = "User details and roles updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiRejectedException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await RepopulateEditUserModelAsync(id, model, selectedRoles);
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Failed to update user: " + ex.Message);
                await RepopulateEditUserModelAsync(id, model, selectedRoles);
                return View(model);
            }
        }

        private async Task RepopulateEditUserModelAsync(string id, EditUserViewModel model, IEnumerable<string>? selectedRoles = null)
        {
            try
            {
                var roleInfo = await _apiClient.GetUserRolesAsync(id);
                var selectedSet = new HashSet<string>(
                    selectedRoles ?? (model.Roles ?? new List<RoleCheckbox>())
                        .Where(r => r.Selected && !string.IsNullOrWhiteSpace(r.RoleName))
                        .Select(r => r.RoleName.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                var availableRoles = (roleInfo.AvailableRoles ?? new List<string>())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r)
                    .ToList();

                model.Roles = availableRoles
                    .Select(r => new RoleCheckbox
                    {
                        RoleName = r,
                        Selected = selectedSet.Contains(r)
                    })
                    .ToList();

                var currentRoles = roleInfo.CurrentRoles ?? new List<string>();
                model.StaleRoles = currentRoles
                    .Where(r => !availableRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                if (model.Roles == null || !model.Roles.Any())
                {
                    var fallback = new[] { "Admin", "Client", "FieldWorker", "Manager", "Office", "SiteManager" };
                    model.Roles = fallback.Select(r => new RoleCheckbox { RoleName = r }).ToList();
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string id, string reason)
        {
            try
            {
                var req = new UbuhlebethuConnectPro.WebApp.ApiClient.ResetPasswordRequest { Reason = reason };
                var password = await _apiClient.ResetPasswordAsync(id, req);
                TempData["Success"] = $"Password reset to one-time password: {password}. User will be prompted to change it on next login.";
                return RedirectToAction(nameof(Details), new { id = id });
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to reset password.";
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }

        public async Task<IActionResult> Details(string id)
        {
            var userDto = await _apiClient.GetUserAsync(id);
            if (userDto == null) return NotFound();
             
            var model = new AdminUserViewModel 
            { 
                Id = userDto.Id, 
                UserName = userDto.UserName, 
                Email = userDto.Email, 
                Roles = userDto.Roles 
            };
            ViewBag.LockoutEnd = userDto.LockoutEnd;

            // load recent audit logs for this user (most recent 20)
            var auditLogsDto = await _apiClient.GetAuditLogsByUserIdAsync(id, 1, 20);
            model.AuditLogs = auditLogsDto.Items.Select(log => new AuditLog
            {
                Id = Guid.NewGuid(),
                ActionType = log.ActionType,
                PerformedBy = log.PerformedBy,
                TargetUserId = log.TargetUserId,
                Reason = log.Reason ?? string.Empty,
                Details = log.Details ?? string.Empty,
                Timestamp = log.Timestamp
            }).ToList();
 
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id, string? reason)
        {
            if (!string.IsNullOrEmpty(reason) && reason.Length > 500)
            {
                TempData["Error"] = "Deactivate reason must be 500 characters or fewer.";
                return RedirectToAction("Details", new { id });
            }
            
            try 
            {
                await _apiClient.DeactivateUserAsync(id, new DeactivateRequest { Reason = reason });
                TempData["Success"] = "User successfully deactivated.";
            } 
            catch (Exception)
            {
                TempData["Error"] = "Failed to deactivate user. Please try again.";
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(string id, string? reason)
        {
            if (!string.IsNullOrEmpty(reason) && reason.Length > 500)
            {
                TempData["Error"] = "Reactivate reason must be 500 characters or fewer.";
                return RedirectToAction("Details", new { id });
            }
             
            try 
            {
                await _apiClient.ReactivateUserAsync(id, new ReactivateRequest { Reason = reason });
                TempData["Success"] = "User successfully reactivated.";
            } 
            catch (Exception)
            {
                TempData["Error"] = "Failed to reactivate user. Please try again.";
            }
            return RedirectToAction("Details", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "The user could not be identified.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var user = await _apiClient.GetUserAsync(id);
                if (user == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.Equals(user.UserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "You cannot delete your own logged-in admin account.";
                    return RedirectToAction(nameof(Index));
                }

                var model = new AdminUserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Roles = user.Roles
                };
                ViewBag.LockoutEnd = user.LockoutEnd;
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to load user: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id, string reason)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "The user could not be identified.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A deletion reason is required for the audit log.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            if (reason.Length > 500)
            {
                TempData["Error"] = "Deletion reason must be 500 characters or fewer.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            try
            {
                await _apiClient.HardDeleteUserAsync(id, new DeleteUserRequest { Reason = reason.Trim() });
                TempData["Success"] = "User was permanently deleted from the system and recorded in the audit log.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to delete user: " + ex.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditRoles(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "The user could not be identified.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var user = await _apiClient.GetUserAsync(id);
                if (user == null)
                {
                    TempData["Error"] = "That user could not be found.";
                    return RedirectToAction(nameof(Index));
                }

                var roleInfo = await _apiClient.GetUserRolesAsync(user.Id);
                return View(CreateEditRolesModel(user.Id, user.UserName, roleInfo));
            }
            catch (Exception)
            {
                TempData["Error"] = "The role editor could not be loaded. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoles(EditRolesViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserId))
            {
                TempData["Error"] = "The role update could not be processed because the user was not identified.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var user = await _apiClient.GetUserAsync(model.UserId);
                if (user == null)
                {
                    TempData["Error"] = "That user could not be found. No roles were changed.";
                    return RedirectToAction(nameof(Index));
                }

                var roleInfo = await _apiClient.GetUserRolesAsync(model.UserId);
                var availableRoles = roleInfo.AvailableRoles
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var availableByName = availableRoles.ToDictionary(role => role, StringComparer.OrdinalIgnoreCase);
                var selectedNames = (model.Roles ?? new List<RoleCheckbox>())
                    .Where(role => role.Selected && !string.IsNullOrWhiteSpace(role.RoleName))
                    .Select(role => role.RoleName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var invalidRoles = selectedNames
                    .Where(role => !availableByName.ContainsKey(role))
                    .ToList();

                if (invalidRoles.Any())
                {
                    ModelState.AddModelError(string.Empty, "One or more selected roles are no longer available. Review the available roles and try again.");
                }

                var selectedRoles = selectedNames
                    .Where(role => availableByName.ContainsKey(role))
                    .Select(role => availableByName[role])
                    .ToList();

                if (!selectedRoles.Any())
                {
                    ModelState.AddModelError(string.Empty, "Please select at least one available role.");
                }

                var editModel = CreateEditRolesModel(model.UserId, user.UserName, roleInfo, selectedRoles);
                if (!ModelState.IsValid)
                    return View(editModel);

                await _apiClient.UpdateUserRolesAsync(model.UserId, new UpdateUserRolesRequest { Roles = selectedRoles });
                TempData["Success"] = "Roles updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiRejectedException ex)
            {
                ModelState.AddModelError(string.Empty, $"The roles could not be updated: {ex.Message}");
                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "The roles could not be updated. No confirmation was received from the server. Please try again.");
                return View(model);
            }
        }

        private static EditRolesViewModel CreateEditRolesModel(
            string userId,
            string? userName,
            UserRolesDto roleInfo,
            IEnumerable<string>? selectedRoles = null)
        {
            var availableRoles = roleInfo.AvailableRoles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(role => role)
                .ToList();
            var currentRoles = new HashSet<string>(roleInfo.CurrentRoles, StringComparer.OrdinalIgnoreCase);
            var selected = selectedRoles == null
                ? currentRoles
                : new HashSet<string>(selectedRoles, StringComparer.OrdinalIgnoreCase);

            return new EditRolesViewModel
            {
                UserId = userId,
                UserName = userName ?? roleInfo.UserName,
                Roles = availableRoles
                    .Select(role => new RoleCheckbox { RoleName = role, Selected = selected.Contains(role) })
                    .ToList(),
                StaleRoles = currentRoles
                    .Where(role => !availableRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(role => role)
                    .ToList()
            };
        }

        // List audit logs with simple paging/filtering
        public async Task<IActionResult> AuditLogs(string? q, int page = 1)
        {
            const int pageSize = 20;
            var auditLogsDto = await _apiClient.GetAuditLogsAsync(q, page, pageSize);
             
            var vm = new AuditLogsListViewModel
            {
                Items = auditLogsDto.Items.Select(log => new AuditLog
                {
                    Id = Guid.NewGuid(),
                    ActionType = log.ActionType,
                    PerformedBy = log.PerformedBy,
                    TargetUserId = log.TargetUserId,
                    Reason = log.Reason ?? string.Empty,
                    Details = log.Details ?? string.Empty,
                    Timestamp = log.Timestamp
                }).ToList(),
                Page = auditLogsDto.Page,
                PageSize = auditLogsDto.PageSize,
                TotalPages = auditLogsDto.TotalPages,
                Query = auditLogsDto.Query
            };

            return View(vm);
        }

        // Export audit logs CSV (respects optional filter q)
        public async Task<IActionResult> ExportAuditLogs(string? q)
        {
            return await _apiClient.ExportAuditLogsAsync(q);
        }

        [HttpGet]
        public async Task<IActionResult> DeactivateConfirm(string id)
        {
            var userDto = await _apiClient.GetUserAsync(id);
            if (userDto == null) return NotFound();
            ViewBag.UserId = id;
            return View("Deactivate", userDto.UserName);
        }

        [HttpGet]
        public IActionResult SystemSettings() => View();

        [HttpGet]
        public async Task<IActionResult> Health()
        {
            var health = await _apiClient.GetHealthCheckAsync();
            return View(health);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveSettings()
        {
            TempData["SuccessMessage"] = "Settings saved successfully.";
            return RedirectToAction("SystemSettings");
        }
    }

    // View models
    public class AdminUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IEnumerable<string> Roles { get; set; } = new List<string>();
        public List<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    }

    public class EditRolesViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public List<RoleCheckbox> Roles { get; set; } = new List<RoleCheckbox>();
        public List<string> StaleRoles { get; set; } = new List<string>();
    }

    public class CreateUserViewModel
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Username is required.")]
        public string UserName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required.")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;

        public List<RoleCheckbox> Roles { get; set; } = new List<RoleCheckbox>();
    }

    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Username is required.")]
        public string UserName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Email is required.")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = string.Empty;

        public List<RoleCheckbox> Roles { get; set; } = new List<RoleCheckbox>();
        public List<string> StaleRoles { get; set; } = new List<string>();
    }

    public class RoleCheckbox
    {
        public string RoleName { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}

