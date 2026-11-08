using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace UbuhlebethuConnectPro.WebApp.Filters
{
    public class DynamicRoleRefreshFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(user.Identity.Name))
            {
                var lastCheckStr = user.FindFirst("LastRoleCheck")?.Value;
                // Check if roles were synced in the last 1 minute
                if (!DateTimeOffset.TryParse(lastCheckStr, out var lastCheck) || DateTimeOffset.UtcNow.Subtract(lastCheck).TotalMinutes > 1)
                {
                    var apiClient = context.HttpContext.RequestServices.GetRequiredService<IConnectProApiClient>();
                    
                    try 
                    {
                        var users = await apiClient.GetUsersAsync(search: user.Identity.Name);
                        var currentUserDto = users?.FirstOrDefault(u => string.Equals(u.UserName, user.Identity.Name, StringComparison.OrdinalIgnoreCase));
                        
                        if (currentUserDto != null)
                        {
                            var currentRolesInCookie = user.FindAll(ClaimTypes.Role).Select(c => c.Value).OrderBy(r => r).ToList();
                            var newRoles = currentUserDto.Roles.OrderBy(r => r).ToList();

                            if (!currentRolesInCookie.SequenceEqual(newRoles) || DateTimeOffset.UtcNow.Subtract(lastCheck).TotalMinutes > 1)
                            {
                                var claims = new List<Claim>
                                {
                                    new Claim(ClaimTypes.Name, currentUserDto.UserName),
                                    new Claim("LastRoleCheck", DateTimeOffset.UtcNow.ToString("O"))
                                };
                                foreach (var role in currentUserDto.Roles)
                                {
                                    claims.Add(new Claim(ClaimTypes.Role, role));
                                }

                                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                                await context.HttpContext.SignInAsync(
                                    CookieAuthenticationDefaults.AuthenticationScheme, 
                                    new ClaimsPrincipal(identity),
                                    new AuthenticationProperties { IsPersistent = true });
                            }
                        }
                    }
                    catch 
                    {
                        // Ignore API errors, we'll try again on the next request.
                    }
                }
            }

            await next();
        }
    }
}
