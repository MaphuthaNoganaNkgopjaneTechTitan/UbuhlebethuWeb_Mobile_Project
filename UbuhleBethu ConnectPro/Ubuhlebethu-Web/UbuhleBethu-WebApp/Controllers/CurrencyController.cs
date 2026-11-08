using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.Services;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize]
    public class CurrencyController : Controller
    {
        private readonly ICurrencyService _currency;
        public CurrencyController(ICurrencyService currency) => _currency = currency;

        [HttpGet]
        public async Task<IActionResult> Convert(decimal amount, string from = "ZAR", string to = "USD", CancellationToken ct = default)
        {
            try
            {
                return Json(await _currency.ConvertAsync(amount, from, to, ct));
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // All rates for one base currency in a single call (the pages convert amounts on screen with this).
        [HttpGet]
        public async Task<IActionResult> Rates(string baseCode = "ZAR", CancellationToken ct = default)
        {
            try
            {
                return Json(await _currency.GetRatesAsync(baseCode, ct));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // The list of supported currencies for dropdowns.
        [HttpGet]
        public IActionResult List() =>
            Json(_currency.Currencies.Select(c => new { code = c.Key, name = c.Value }));
    }
}
