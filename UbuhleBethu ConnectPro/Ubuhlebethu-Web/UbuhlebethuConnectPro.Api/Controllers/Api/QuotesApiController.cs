using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Services;
using System.Threading.Tasks;
using System;
using System.Linq;
using Ganss.Xss;
using UbuhlebethuConnectPro.Core.Repositories;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/quotes")]
    [Route("api/v1/quotes")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class QuotesApiController : ControllerBase
    {
        private readonly IQuotationRepository _quoteRepo;
        private readonly IQuotationService _quoteService;

        public QuotesApiController(IQuotationRepository quoteRepo, IQuotationService quoteService)
        {
            _quoteRepo = quoteRepo;
            _quoteService = quoteService;
        }

        public class QuoteDto
        {
            public Guid Id { get; set; }
            public string Reference { get; set; } = string.Empty;
            public DateTime RequestDate { get; set; }
            public string ProjectType { get; set; } = string.Empty;
            public string SiteLocation { get; set; } = string.Empty;
            public decimal EstimatedBudget { get; set; }
            public string Status { get; set; } = string.Empty;
            public bool IsPaid { get; set; }
            public string ClientName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string ProjectDescription { get; set; } = string.Empty;
            public string CreatedBy { get; set; } = string.Empty;
            public Guid? InvoiceId { get; set; }
        }

        public class QuoteDtoForCreation
        {
            public string ClientName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string ProjectType { get; set; } = string.Empty;
            public string SiteLocation { get; set; } = string.Empty;
            public decimal EstimatedBudget { get; set; }
            public string ProjectDescription { get; set; } = string.Empty;
        }

        [HttpGet("")]
        public async Task<IActionResult> GetMyQuotes()
        {
            var username = User.Identity?.Name ?? string.Empty;

            List<QuoteRequest> quotes;

            // If user has operational, management, or administrative roles, return all quotes
            if (User.IsInRole("Admin") ||
                User.IsInRole("SystemAdmin") ||
                User.IsInRole("Office") ||
                User.IsInRole("OfficeStaff") ||
                User.IsInRole("Manager") ||
                User.IsInRole("Management") ||
                User.IsInRole("Executive"))
            {
                // Empty string and null owner returns all quotes across the company
                quotes = await _quoteRepo.SearchAsync("", null);
            }
            else
            {
                // Standard Clients only see their own quotation submissions
                quotes = await _quoteRepo.GetByClientAsync(username);
            }

            var quoteDtos = quotes.Select(q => new QuoteDto
            {
                Id = q.Id,
                Reference = q.Reference,
                RequestDate = q.RequestDate,
                ProjectType = q.ProjectType,
                SiteLocation = q.SiteLocation,
                EstimatedBudget = q.EstimatedBudget,
                Status = q.Status,
                IsPaid = q.IsPaid,
                ClientName = q.ClientName,
                Email = q.Email,
                Phone = q.Phone,
                ProjectDescription = q.ProjectDescription,
                CreatedBy = q.CreatedBy,
                InvoiceId = q.InvoiceId
            });

            return Ok(quoteDtos);
        }

        [HttpGet("client/{username}")]
        public async Task<IActionResult> GetQuotesByClient(string username, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] string? status = null)
        {
            if (!User.IsInRole("Admin") &&
                !User.IsInRole("SystemAdmin") &&
                !User.IsInRole("Office") &&
                !User.IsInRole("OfficeStaff") &&
                !User.IsInRole("Manager") &&
                !User.IsInRole("Management") &&
                !string.Equals(username, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var quotes = await _quoteRepo.GetByClientAsync(username, from, to, status);
            var quoteDtos = quotes.Select(q => new QuoteDto
            {
                Id = q.Id,
                Reference = q.Reference,
                RequestDate = q.RequestDate,
                ProjectType = q.ProjectType,
                SiteLocation = q.SiteLocation,
                EstimatedBudget = q.EstimatedBudget,
                Status = q.Status,
                IsPaid = q.IsPaid,
                ClientName = q.ClientName,
                Email = q.Email,
                Phone = q.Phone,
                ProjectDescription = q.ProjectDescription,
                CreatedBy = q.CreatedBy,
                InvoiceId = q.InvoiceId
            }).ToList();

            return Ok(quoteDtos);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetQuote(Guid id)
        {
            var quote = await _quoteRepo.GetByIdAsync(id);
            if (quote == null) return NotFound();

            if (!User.IsInRole("Admin") &&
                !User.IsInRole("Office") &&
                !User.IsInRole("Manager") &&
                !User.IsInRole("Management") &&
                !User.IsInRole("Executive") &&
                !string.Equals(quote.CreatedBy, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var quoteDto = new QuoteDto
            {
                Id = quote.Id,
                Reference = quote.Reference,
                RequestDate = quote.RequestDate,
                ProjectType = quote.ProjectType,
                SiteLocation = quote.SiteLocation,
                EstimatedBudget = quote.EstimatedBudget,
                Status = quote.Status,
                IsPaid = quote.IsPaid,
                ClientName = quote.ClientName,
                Email = quote.Email,
                Phone = quote.Phone,
                ProjectDescription = quote.ProjectDescription,
                CreatedBy = quote.CreatedBy,
                InvoiceId = quote.InvoiceId
            };

            return Ok(quoteDto);
        }

        [AllowAnonymous]
        [HttpPost("")]
        public async Task<IActionResult> SubmitQuote([FromBody] QuoteDtoForCreation form)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var sanitizer = new HtmlSanitizer();
            var quote = new QuoteRequest
            {
                ClientName = sanitizer.Sanitize(form.ClientName),
                Email = sanitizer.Sanitize(form.Email),
                Phone = sanitizer.Sanitize(form.Phone),
                ProjectType = form.ProjectType,
                SiteLocation = sanitizer.Sanitize(form.SiteLocation),
                EstimatedBudget = form.EstimatedBudget,
                ProjectDescription = sanitizer.Sanitize(form.ProjectDescription)
            };

            var username = User.Identity?.Name ?? quote.ClientName;
            var created = await _quoteService.CreateQuoteAsync(quote, username);

            var quoteDto = new QuoteDto
            {
                Id = created.Id,
                Reference = created.Reference,
                RequestDate = created.RequestDate,
                ProjectType = created.ProjectType,
                SiteLocation = created.SiteLocation,
                EstimatedBudget = created.EstimatedBudget,
                Status = created.Status,
                IsPaid = created.IsPaid,
                ClientName = created.ClientName,
                Email = created.Email,
                Phone = created.Phone,
                ProjectDescription = created.ProjectDescription,
                CreatedBy = created.CreatedBy,
                InvoiceId = created.InvoiceId
            };

            return CreatedAtAction(nameof(GetQuote), new { id = created.Id }, quoteDto);
        }

        [AllowAnonymous]
        [HttpGet("track")]
        public async Task<IActionResult> TrackQuote([FromQuery] string reference, [FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(email))
                return BadRequest("Reference and email are required.");

            var quotes = await _quoteRepo.SearchAsync(reference.Trim(), null);
            var quote = quotes.FirstOrDefault(q =>
                string.Equals(q.Reference?.Trim(), reference.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(q.Email?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));

            if (quote == null) return NotFound();

            return Ok(new QuoteDto
            {
                Id = quote.Id,
                Reference = quote.Reference,
                RequestDate = quote.RequestDate,
                ProjectType = quote.ProjectType,
                SiteLocation = quote.SiteLocation,
                EstimatedBudget = quote.EstimatedBudget,
                Status = quote.Status,
                IsPaid = quote.IsPaid,
                ClientName = quote.ClientName,
                Email = quote.Email,
                Phone = quote.Phone,
                ProjectDescription = quote.ProjectDescription,
                CreatedBy = quote.CreatedBy,
                InvoiceId = quote.InvoiceId
            });
        }

        [HttpPost("{id:guid}/pay")]
        public async Task<IActionResult> PayQuote(Guid id)
        {
            var quote = await _quoteRepo.GetByIdAsync(id);
            if (quote == null) return NotFound();

            var username = User.Identity?.Name ?? "Client";
            bool isSystemAdmin = User.IsInRole("Admin") || User.IsInRole("SystemAdmin");

            // If quote status is Approved, ensure it transitions smoothly for payment simulation
            if (quote.Status == "Approved" || quote.Status == "Submitted")
            {
                quote.Status = "Invoiced";
                await _quoteRepo.SaveChangesAsync();
            }

            var invoice = await _quoteService.MarkAsPaidAsync(id, quote.CreatedBy ?? username, true);

            return Ok(new
            {
                message = "Payment processed successfully.",
                quoteId = quote.Id,
                invoiceId = invoice.Id,
                status = quote.Status,
                isPaid = quote.IsPaid
            });
        }

        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> RejectQuote(Guid id, [FromBody] string? reason = null)
        {
            var quote = await _quoteRepo.GetByIdAsync(id);
            if (quote == null) return NotFound();

            if (!User.IsInRole("Office") && !User.IsInRole("Admin") && !User.IsInRole("Manager"))
            {
                return Forbid();
            }

            quote.Status = "Rejected";
            await _quoteRepo.SaveChangesAsync();

            return Ok(new
            {
                message = "Quotation has been rejected.",
                quoteId = quote.Id,
                status = quote.Status
            });
        }
    }
}