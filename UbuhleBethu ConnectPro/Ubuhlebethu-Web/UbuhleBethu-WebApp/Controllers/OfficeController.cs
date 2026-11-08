using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.ApiClient;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    public class AIEstimateInput
    {
        public string ProjectType { get; set; } = "RDP/Social Housing";
        public decimal SiteArea { get; set; }
        public int NumberOfUnits { get; set; }
        public string QualityGrade { get; set; } = "Standard";
        public string Province { get; set; } = "Gauteng";
        public decimal? EstimatedAmount { get; set; }
    }

    [Authorize(Roles = "Office,Admin")]
    public class OfficeController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        public OfficeController(IConnectProApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> PendingQuotes()
        {
            var quotes = await _apiClient.GetPendingQuotesAsync();
            return View(quotes);
        }

        public async Task<IActionResult> PreviewQuote(Guid id)
        {
            var quote = await _apiClient.GetQuoteAsync(id);
            if (quote == null) return NotFound();
            return View(quote);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendToManager(Guid id, decimal? estimatedBudget)
        {
            try
            {
                await _apiClient.SendToManagerAsync(id, estimatedBudget);
                TempData["SuccessMessage"] = "Quotation sent to Manager for sign-off. Client and Management notified.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to send to manager: " + ex.Message;
            }
            return RedirectToAction("Details", "Quotation", new { id });
        }

        [HttpGet]
        public IActionResult AIDraftEstimate() => View(new AIEstimateInput());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CalculateEstimate(AIEstimateInput input)
        {
            if (input.SiteArea <= 0)
                ModelState.AddModelError(nameof(input.SiteArea), "Enter a site area greater than zero.");
            if (input.NumberOfUnits < 0)
                ModelState.AddModelError(nameof(input.NumberOfUnits), "Units cannot be negative.");

            if (!ModelState.IsValid) return View("AIDraftEstimate", input);

            var rate = input.ProjectType switch
            {
                "Civil Infrastructure" => 8200m,
                "School/Clinic" => 10500m,
                "Commercial" => 14500m,
                _ => 6800m
            };
            var qualityMultiplier = input.QualityGrade switch
            {
                "Medium" => 1.15m,
                "Premium" => 1.35m,
                _ => 1m
            };
            var provinceMultiplier = input.Province is "Western Cape" or "KwaZulu-Natal" ? 1.08m : 1m;
            var unitAllowance = input.ProjectType == "RDP/Social Housing" ? input.NumberOfUnits * 42000m : 0m;
            input.EstimatedAmount = Math.Round((input.SiteArea * rate + unitAllowance) * qualityMultiplier * provinceMultiplier, 2);
            return View("AIDraftEstimate", input);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendFeedback(Guid id, string feedback)
        {
            try
            {
                await _apiClient.SendFeedbackAsync(id, feedback);
                TempData["SuccessMessage"] = "Invoice generated according to estimate and delivered to client.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to finalize invoice: " + ex.Message;
            }
            return RedirectToAction("Details", "Quotation", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDraftEstimate(Guid id)
        {
            var quote = await _apiClient.GenerateDraftEstimateAsync(id);
            if (quote == null) return NotFound();

            TempData["SuccessMessage"] = "Draft estimate generated successfully.";
            return RedirectToAction("Details", "Quotation", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendInvoice(Guid id)
        {
            var result = await _apiClient.SendInvoiceAsync(id);
            TempData["SuccessMessage"] = result ?? "Invoice generated and sent to client.";
            return RedirectToAction("Details", "Quotation", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveQuote(Guid id)
        {
            var quote = await _apiClient.ApproveQuoteAsync(id);
            if (quote == null) return NotFound();

            TempData["SuccessMessage"] = "Quote approved and project created.";
            return RedirectToAction("Details", "Quotation", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> AllProposals(string? status = null)
        {
            var quotes = await _apiClient.GetQuotesAsync();
            if (!string.IsNullOrEmpty(status))
            {
                quotes = quotes.Where(q => q.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            ViewData["Status"] = status ?? "";
            return View(quotes);
        }
    }
}