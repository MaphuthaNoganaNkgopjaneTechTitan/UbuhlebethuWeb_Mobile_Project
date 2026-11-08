using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Services;
using System.Threading.Tasks;
using System;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    public class QuotationController : Controller
    {
        private readonly IConnectProApiClient _apiClient;
        private readonly ICurrencyService _currencyService;

        public QuotationController(IConnectProApiClient apiClient, ICurrencyService currencyService)
        {
            _apiClient = apiClient;
            _currencyService = currencyService;
        }

        [Authorize(Roles = "Client,Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            ViewData["Currencies"] = _currencyService.Currencies;
            return View();
        }

        [Authorize(Roles = "Client,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuoteRequest request)
        {
            if (ModelState.IsValid)
            {
                var sanitizer = new Ganss.Xss.HtmlSanitizer();
                request.ProjectDescription = sanitizer.Sanitize(request.ProjectDescription ?? "");

                var quoteDto = new QuoteDtoForCreation
                {
                    ClientName = request.ClientName,
                    Email = request.Email ?? "",
                    Phone = request.Phone ?? "",
                    ProjectType = request.ProjectType,
                    SiteLocation = request.SiteLocation,
                    EstimatedBudget = request.EstimatedBudget,
                    ProjectDescription = request.ProjectDescription
                };

                var returnedQuote = await _apiClient.CreateQuoteAsync(quoteDto);
                TempData["SuccessMessage"] = "Quotation request submitted successfully! Ref: " + returnedQuote.Reference;
            }
            ViewData["Currencies"] = _currencyService.Currencies;
            return View(request);
        }

        [Authorize]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Details(Guid id)
        {
            var quote = await _apiClient.GetQuoteAsync(id);
            if (quote == null) return NotFound();

            if (User.IsInRole("Client") && !User.IsInRole("Admin") && !User.IsInRole("SystemAdmin") && !User.IsInRole("Office") && !User.IsInRole("OfficeStaff") && quote.CreatedBy != User.Identity?.Name)
            {
                return Forbid();
            }

            return View(quote);
        }

        [Authorize(Roles = "Client,Admin")]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Index()
        {
            var quotes = await _apiClient.GetQuotesAsync();
            return View(quotes);
        }

        [HttpPost]
        [Authorize(Roles = "Office,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDraft(Guid id)
        {
            await _apiClient.GenerateDraftEstimateAsync(id);
            TempData["SuccessMessage"] = "Draft estimate generated successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Office,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid id)
        {
            await _apiClient.ApproveQuoteAsync(id);
            TempData["SuccessMessage"] = "Quotation has been approved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Office,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoice(Guid id)
        {
            await _apiClient.SendInvoiceAsync(id);
            TempData["SuccessMessage"] = "Invoice generated and sent successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = "Client,Office,Admin")]
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Payment(Guid id)
        {
            var quote = await _apiClient.GetQuoteAsync(id);
            if (quote == null) return NotFound();
            return View(quote);
        }

        [Authorize(Roles = "Client,Office,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(Guid id)
        {
            try
            {
                await _apiClient.PayQuoteAsync(id);
                TempData["SuccessMessage"] = "Payment of outstanding balance processed successfully. Project status updated to Paid!";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Payment simulation failed: " + ex.Message;
                return RedirectToAction(nameof(Payment), new { id });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Office,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid id, string? reason = null)
        {
            await _apiClient.RejectQuoteAsync(id, reason);
            TempData["SuccessMessage"] = "Quotation has been rejected.";
            return RedirectToAction(nameof(Details), new { id });
        }
    
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> PrintInvoice(Guid id)
        {
            var quote = await _apiClient.GetQuoteAsync(id);
            if (quote == null) return NotFound();

            var currentUserName = User.Identity?.Name ?? string.Empty;
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Office") || User.IsInRole("OfficeStaff") || User.IsInRole("Manager") || User.IsInRole("Management") || User.IsInRole("Executive");
            bool isOwner = !string.IsNullOrEmpty(quote.CreatedBy) && string.Equals(quote.CreatedBy, currentUserName, StringComparison.OrdinalIgnoreCase);

            if (!isStaff && !isOwner)
            {
                return Forbid();
            }

            var jobs = await _apiClient.GetJobsAsync();
            var job = jobs.FirstOrDefault(j => j.JobNumber == quote.Reference || (!string.IsNullOrEmpty(quote.Reference) && j.JobNumber.Contains(quote.Reference)));

            var model = DashboardController.BuildPrintInvoiceViewModel(job, quote);
            return View("PrintInvoice", model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> PrintQuotation(Guid id) => await PrintInvoice(id);

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> InvoicePdf(Guid id)
        {
            var quote = await _apiClient.GetQuoteAsync(id);
            if (quote == null) return NotFound();

            if (quote.InvoiceId.HasValue)
            {
                try
                {
                    var pdfBytes = await _apiClient.GetInvoicePdfAsync(quote.InvoiceId.Value);
                    return File(pdfBytes, "application/pdf", $"TaxInvoice_{quote.Reference ?? id.ToString().Substring(0, 8)}.pdf");
                }
                catch { /* Fallback */ }
            }

            return RedirectToAction(nameof(PrintInvoice), new { id });
        }
    }
}
