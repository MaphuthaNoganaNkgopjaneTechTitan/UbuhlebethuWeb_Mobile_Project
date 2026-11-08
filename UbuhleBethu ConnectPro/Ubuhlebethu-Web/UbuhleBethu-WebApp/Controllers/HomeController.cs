using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Models.Portfolio;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    /// <summary>The public website: Ubuhlebethu's completed buildings and projects.</summary>
    [AllowAnonymous]
    public class HomeController : Controller
    {
        public IActionResult Index() => View(PortfolioCatalog.All);

        public IActionResult Privacy() => View();

        public IActionResult Unavailable()
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
