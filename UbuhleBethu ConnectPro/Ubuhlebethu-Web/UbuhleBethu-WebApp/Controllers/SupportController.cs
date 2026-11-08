using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.ApiClient;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize]
    public class SupportController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        public SupportController(IConnectProApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Index()
        {
            var tickets = await _apiClient.GetSupportTicketsAsync();
            return View(tickets ?? new List<SupportTicketDto>());
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string subject, string description, string? category = null)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description))
            {
                ModelState.AddModelError(string.Empty, "Subject and description are required.");
                return View();
            }

            // Determine target stakeholder based on category and keywords
            string routedStakeholder = ResolveTargetStakeholder(category, subject, description);

            // Tag subject with the stakeholder so both Web and Mobile route to the right department
            string taggedSubject = $"[{routedStakeholder}] {subject.Trim()}";

            await _apiClient.CreateSupportTicketAsync(taggedSubject, description.Trim());
            TempData["SuccessMessage"] = $"Support ticket submitted and routed to {routedStakeholder}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,SystemAdmin,Manager,Management,Executive,Office,OfficeStaff")]
        public async Task<IActionResult> Respond(string id, string response)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(response))
            {
                TempData["ErrorMessage"] = "Response cannot be blank.";
                return RedirectToAction(nameof(Index));
            }

            if (Guid.TryParse(id, out var ticketGuid))
            {
                var success = await _apiClient.RespondToSupportTicketAsync(ticketGuid, response.Trim());
                if (success)
                {
                    TempData["SuccessMessage"] = "Response submitted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to submit response.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private static string ResolveTargetStakeholder(string? category, string subject, string description)
        {
            string combined = $"{category} {subject} {description}".ToLowerInvariant();

            // 1. Office / Quantity Surveying (Billing, Invoicing, Payments, BOQ, Pricing)
            if (combined.Contains("invoice") || combined.Contains("billing") || combined.Contains("payment") ||
                combined.Contains("boq") || combined.Contains("estimate") || combined.Contains("quote") ||
                combined.Contains("pricing") || combined.Contains("cost"))
            {
                return "Office / QS";
            }

            // 2. Project Manager (Demolition, Site Safety, Rubble, Progress, Structural)
            if (combined.Contains("demolition") || combined.Contains("site") || combined.Contains("progress") ||
                combined.Contains("safety") || combined.Contains("contractor") || combined.Contains("structural") ||
                combined.Contains("rubble") || combined.Contains("building") || combined.Contains("inspection"))
            {
                return "Project Manager";
            }

            // 3. System Administrator (Access, Login, Account, Bug, Error)
            if (combined.Contains("password") || combined.Contains("login") || combined.Contains("access") ||
                combined.Contains("account") || combined.Contains("bug") || combined.Contains("error") ||
                combined.Contains("technical"))
            {
                return "System Admin";
            }

            return "Project Manager"; // Default to Project Manager
        }
    }
}