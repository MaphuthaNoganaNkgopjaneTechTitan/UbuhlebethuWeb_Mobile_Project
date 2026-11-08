using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.WebApp.Filters
{
    public class RequireMfaSetupFilter : IAsyncActionFilter
    {
        private readonly UserManager<ApplicationUser> _userManager;
        
        public RequireMfaSetupFilter(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            var path = context.HttpContext.Request.Path.Value?.ToLower() ?? "";
            
            if (user.Identity?.IsAuthenticated == true && 
                !path.Contains("/account/setupmfa") && 
                !path.Contains("/account/enablemfa") && 
                !path.Contains("/account/logout") &&
                !path.StartsWith("/api/") &&
                !path.Contains("/lib/") && !path.Contains("/css/") && !path.Contains("/js/"))
            {
                var appUser = await _userManager.GetUserAsync(user);
                if (appUser != null && !appUser.TwoFactorEnabled)
                {
                    context.Result = new RedirectToActionResult("SetupMfa", "Account", null);
                    return;
                }
            }
            
            await next();
        }
    }
}
