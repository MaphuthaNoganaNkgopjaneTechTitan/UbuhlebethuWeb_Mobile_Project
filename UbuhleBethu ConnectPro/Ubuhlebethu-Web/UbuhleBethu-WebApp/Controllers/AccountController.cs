using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Models.DTOs;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Models.DTOs;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        public AccountController(IConnectProApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login), new { returnUrl });
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("ClientPortal", "Dashboard");
            }
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var registerReq = new UbuhlebethuConnectPro.Core.Models.DTOs.RegisterRequest
                {
                    Email = model.Email.Trim(),
                    Password = model.Password
                };

                var apiResult = await _apiClient.RegisterAsync(registerReq);
                if (apiResult == null || string.IsNullOrEmpty(apiResult.Token))
                {
                    ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
                    return View(model);
                }

                // Store tokens in session
                HttpContext.Session.SetString("JwtToken", apiResult.Token);
                HttpContext.Session.SetString("RefreshToken", apiResult.RefreshToken);

                // Create identity and sign in
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, apiResult.UserName ?? model.Email),
                    new Claim("LastRoleCheck", DateTimeOffset.UtcNow.ToString("O")),
                    new Claim(ClaimTypes.Role, "Client")
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = true };
                authProperties.StoreTokens(new[]
                {
                    new AuthenticationToken { Name = "jwt_token", Value = apiResult.Token }
                });

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);
                return RedirectToAction("ClientPortal", "Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            UbuhlebethuConnectPro.WebApp.Models.DTOs.LoginViewModel model,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var loginRequest = new LoginRequest { Username = model.Username, Password = model.Password };
                var apiLoginResult = await _apiClient.LoginAsync(loginRequest);

                if (apiLoginResult == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }

                // Store tokens in session
                HttpContext.Session.SetString("JwtToken", apiLoginResult.Token);
                HttpContext.Session.SetString("RefreshToken", apiLoginResult.RefreshToken);

                // Create identity and sign in
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, apiLoginResult.UserName ?? model.Username),
                    new Claim("LastRoleCheck", DateTimeOffset.UtcNow.ToString("O"))
                };

                if (apiLoginResult.Roles != null)
                {
                    foreach (var role in apiLoginResult.Roles)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true
                };
                authProperties.StoreTokens(new[]
                {
                    new AuthenticationToken { Name = "jwt_token", Value = apiLoginResult.Token }
                });

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

                if (apiLoginResult.RequiresPasswordReset)
                {
                    return RedirectToAction("ChangePassword", "Account", new { returnUrl });
                }

                return RedirectAfterLogin(apiLoginResult.Roles, returnUrl);
            }
            catch (InvalidOperationException ex) when (ex.Message == "LockedOut")
            {
                ModelState.AddModelError(string.Empty, "Account has been blocked . Contact Admin/Support.");
                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt or unable to obtain API token.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult ChangePassword(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
            {
                ModelState.AddModelError(string.Empty, "All fields are required.");
                return View();
            }

            try
            {
                var users = await _apiClient.GetUsersAsync(search: User.Identity?.Name);
                var user = users.FirstOrDefault(u => u.UserName == User.Identity?.Name);
                if (user != null)
                {
                    await _apiClient.ChangePasswordAsync(user.Id, new UbuhlebethuConnectPro.WebApp.ApiClient.ChangePasswordRequest { CurrentPassword = currentPassword, NewPassword = newPassword });
                    TempData["Success"] = "Password changed successfully.";
                    return RedirectAfterLogin(user.Roles, returnUrl);
                }
                ModelState.AddModelError(string.Empty, "User not found.");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Failed to change password. Please ensure current password is correct.");
            }
            return View();
        }

        [HttpGet]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            return RedirectToAction(nameof(Login), "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        private IActionResult RedirectAfterLogin(IEnumerable<string>? roles, string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (roles != null)
            {
                if (roles.Contains("Client"))
                {
                    return RedirectToAction("ClientPortal", "Dashboard");
                }

                if (roles.Contains("Office"))
                {
                    return RedirectToAction("Index", "Dashboard");
                }

                if (roles.Contains("Manager") || roles.Contains("Executive"))
                {
                    return RedirectToAction("Executive", "Dashboard");
                }

                if (roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
            }

            return RedirectToAction(nameof(Login), "Account");
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            string? message = null;
            if (statusCode == 400)
            {
                message = "The request could not be completed. If you were editing a user, please verify that at least one role is assigned.";
            }
            return View(new ErrorViewModel 
            { 
                RequestId = requestId,
                StatusCode = statusCode,
                Message = message
            });
        }
    }
}

