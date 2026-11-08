using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Models.DTOs;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize(Roles = "Manager,Management,Executive,Admin")]
    public class ManagerController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        public ManagerController(IConnectProApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = await _apiClient.GetDashboardStats();
            return View(viewModel);
        }

        // Dedicated action for quotes sent to the Manager for review
        [HttpGet]
        public async Task<IActionResult> ReviewQuotes()
        {
            var allQuotes = await _apiClient.GetQuotesAsync();
            var pendingReview = allQuotes
                .Where(q => string.Equals(q.Status, "Under Review", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(q => q.RequestDate)
                .ToList();

            return View(pendingReview);
        }

        [HttpGet]
        public async Task<IActionResult> Proposals(string? status = null)
        {
            var quotes = await _apiClient.GetQuotesAsync();
            if (!string.IsNullOrEmpty(status))
            {
                quotes = quotes.Where(q => q.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            ViewData["Status"] = status ?? "";
            return View(quotes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptQuote(Guid id)
        {
            await _apiClient.ManagerAcceptQuoteAsync(id);
            TempData["SuccessMessage"] = "Quotation accepted and returned to Office/QS.";
            return RedirectToAction("Details", "Quotation", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectQuote(Guid id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "A rejection reason is required.";
                return RedirectToAction("Details", "Quotation", new { id });
            }

            await _apiClient.ManagerRejectQuoteAsync(id, reason);
            TempData["SuccessMessage"] = "Quotation rejected. Client and Office have been notified.";
            return RedirectToAction("Details", "Quotation", new { id });
        }
    }
}