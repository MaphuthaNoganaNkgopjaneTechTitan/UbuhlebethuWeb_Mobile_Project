using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.Core.Services;
using UbuhlebethuConnectPro.Core.Services.AI;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/office")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class OfficeApiController : ControllerBase
    {
        private readonly IQuotationRepository _quoteRepo;
        private readonly IQuotationService _quoteService;
        private readonly IProjectService _projectService;
        private readonly IAIService _aiService;

        public OfficeApiController(IQuotationRepository quoteRepo, IQuotationService quoteService, IProjectService projectService, IAIService aiService)
        {
            _quoteRepo = quoteRepo;
            _quoteService = quoteService;
            _projectService = projectService;
            _aiService = aiService;
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
            public bool IsPaid { get; set; } = false;
            public string ClientName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string ProjectDescription { get; set; } = string.Empty;
            public string CreatedBy { get; set; } = string.Empty;
            public Guid? InvoiceId { get; set; }
        }

        public class SendToManagerRequest
        {
            public decimal? EstimatedBudget { get; set; }
        }

        public class SendFeedbackRequest
        {
            public string Feedback { get; set; } = string.Empty;
        }

        // POST: api/v1/office/send-to-manager/{id}
        [HttpPost("send-to-manager/{id:guid}")]
        public async Task<IActionResult> SendToManager(Guid id, [FromBody] SendToManagerRequest? request)
        {
            var username = User.Identity?.Name ?? "office";
            await _quoteService.SendToManagerForReviewAsync(id, request?.EstimatedBudget, username);
            return Ok(new { message = "Quotation sent to manager for review." });
        }

        // POST: api/v1/office/send-feedback/{id}
        [HttpPost("send-feedback/{id:guid}")]
        public async Task<IActionResult> SendFeedback(Guid id, [FromBody] SendFeedbackRequest? request)
        {
            var username = User.Identity?.Name ?? "office";
            var invoice = await _quoteService.QsFinalizeAndSendFeedbackAsync(id, request?.Feedback ?? string.Empty, username);
            return Ok(invoice);
        }

        // GET: api/v1/office/pending-quotes
        [HttpGet("pending-quotes")]
        public async Task<IActionResult> GetPendingQuotes()
        {
            // Includes ManagerApproved and Rejected so QS & Office can action them
            var pending = await _quoteRepo.GetByStatusesAsync(new[] { "Submitted", "Draft", "ManagerApproved", "Rejected" });
            var quoteDtos = pending.Select(q => new QuoteDto
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

        // GET: api/v1/office/preview/{id}
        [HttpGet("preview/{id:guid}")]
        public async Task<IActionResult> PreviewQuote(Guid id)
        {
            var quote = await _quoteRepo.GetByIdAsync(id);
            if (quote == null) return NotFound();

            if (!User.IsInRole("Admin") && !User.IsInRole("Office") && !string.Equals(quote.CreatedBy, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
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

        // POST: api/v1/office/generate-draft/{id}
        [HttpPost("generate-draft/{id:guid}")]
        public async Task<IActionResult> GenerateDraftEstimate(Guid id)
        {
            var quote = await _quoteRepo.GetByIdAsync(id);
            if (quote == null) return NotFound();

            if (!User.IsInRole("Office") && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var estimateResult = await _aiService.EstimateAsync(quote);

            quote.EstimatedBudget = estimateResult.Amount;
            quote.Status = "Reviewed";
            await _quoteRepo.SaveChangesAsync();

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

        // POST: api/v1/office/approve/{id}
        [HttpPost("approve/{id:guid}")]
        public async Task<IActionResult> ApproveQuote(Guid id)
        {
            var quote = await _quoteRepo.GetByIdAsync(id);
            if (quote == null) return NotFound();

            if (!User.IsInRole("Office") && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var username = User.Identity?.Name ?? "";
            var invoice = await _quoteService.ApproveQuoteAtomicAsync(id, username);

            var updatedQuote = await _quoteRepo.GetByIdAsync(id);
            var quoteDto = new QuoteDto
            {
                Id = updatedQuote.Id,
                Reference = updatedQuote.Reference,
                RequestDate = updatedQuote.RequestDate,
                ProjectType = updatedQuote.ProjectType,
                SiteLocation = updatedQuote.SiteLocation,
                EstimatedBudget = updatedQuote.EstimatedBudget,
                Status = updatedQuote.Status,
                IsPaid = updatedQuote.IsPaid,
                ClientName = updatedQuote.ClientName,
                Email = updatedQuote.Email,
                Phone = updatedQuote.Phone,
                ProjectDescription = updatedQuote.ProjectDescription,
                CreatedBy = updatedQuote.CreatedBy,
                InvoiceId = updatedQuote.InvoiceId
            };

            return Ok(quoteDto);
        }

        // POST: api/v1/office/send-invoice/{id}
        [HttpPost("send-invoice/{id:guid}")]
        public async Task<IActionResult> SendInvoice(Guid id)
        {
            var quote = await _quoteRepo.GetByIdAsync(id);
            if (quote == null) return NotFound();

            if (!User.IsInRole("Office") && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Invoices can only be generated once the quotation has been signed off by management
            if (quote.Status != "ManagerApproved" && quote.Status != "Approved" && quote.Status != "Invoiced")
            {
                return BadRequest("Invoices can only be generated once the quotation has been signed off by management.");
            }

            var username = User.Identity?.Name ?? "office";
            await _quoteService.QsFinalizeAndSendFeedbackAsync(id, "Invoice generated and sent to client.", username);

            return Ok("Invoice generated and sent to client.");
        }
    }
}