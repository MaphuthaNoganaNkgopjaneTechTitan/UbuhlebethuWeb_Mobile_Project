using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using System.Threading.Tasks;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize]
    public class InvoiceController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        public InvoiceController(IConnectProApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Preview(Guid id)
        {
            var invoiceDto = await _apiClient.GetInvoice(id);
            if (invoiceDto == null) return NotFound();
            
            var vm = new UbuhlebethuConnectPro.WebApp.Models.DTOs.InvoiceViewModel
            {
                Id = invoiceDto.Id,
                Amount = invoiceDto.Amount,
                IsPaid = invoiceDto.IsPaid,
                QuoteRequestId = invoiceDto.QuoteRequestId,
                CreatedAt = invoiceDto.CreatedAt,
                QuoteProjectDescription = invoiceDto.QuoteProjectDescription,
                QuoteClientName = invoiceDto.QuoteClientName,
                QuoteReference = invoiceDto.QuoteReference,
                QuoteSiteLocation = invoiceDto.QuoteSiteLocation,
                QuoteProjectType = invoiceDto.QuoteProjectType
            };

            return View(vm);
        }

        public async Task<IActionResult> Download(Guid id)
        {
            var result = await _apiClient.GetInvoiceHtml(id);
            return result;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Office,Admin,Manager,Management,Executive")]
        public async Task<IActionResult> Send(Guid id)
        {
            TempData["SuccessMessage"] = await _apiClient.SendInvoiceAsync(id);
            return RedirectToAction(nameof(Preview), new { id });
        }
    }
}
