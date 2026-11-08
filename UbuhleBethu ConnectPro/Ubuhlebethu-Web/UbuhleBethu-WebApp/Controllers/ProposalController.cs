using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UbuhlebethuConnectPro.Core.Validation;
using UbuhlebethuConnectPro.WebApp.ApiClient;
using UbuhlebethuConnectPro.WebApp.Models.Forms;
using UbuhlebethuConnectPro.WebApp.Models.Portfolio;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    /// <summary>
    /// "Submit a proposal" for a completed building: the visitor enters personal information and uploads a
    /// business proposal document. No account needed; Office staff pick it up from their queue.
    /// </summary>
    [AllowAnonymous]
    public class ProposalController : Controller
    {
        private readonly IConnectProApiClient _api;
        private readonly ILogger<ProposalController> _log;

        public ProposalController(IConnectProApiClient api, ILogger<ProposalController> log)
        {
            _api = api;
            _log = log;
        }

        [HttpGet]
        public IActionResult Submit(string? project)
        {
            var p = PortfolioCatalog.Find(project);
            var form = new ProposalForm { ProjectSlug = p?.Slug, ProjectType = p?.ProjectType ?? string.Empty };
            return ShowForm(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("proposals")]
        [RequestSizeLimit(11 * 1024 * 1024)]
        public async Task<IActionResult> Submit(ProposalForm form, CancellationToken ct)
        {
            // honeypot: pretend it worked so bots learn nothing
            if (!string.IsNullOrEmpty(form.Website))
                return RedirectToAction(nameof(Confirmation), new { reference = "UBU-RECEIVED" });

            byte[] bytes = Array.Empty<byte>();
            string safeName = "proposal";
            string contentType = "application/octet-stream";

            if (form.Document != null && form.Document.Length > 0)
            {
                if (form.Document.Length > ProposalFileValidator.MaxBytes)
                {
                    ModelState.AddModelError(nameof(form.Document), "The file is larger than 10 MB.");
                }
                else
                {
                    await using var ms = new MemoryStream();
                    await form.Document.CopyToAsync(ms, ct);
                    bytes = ms.ToArray();
                    var check = ProposalFileValidator.Validate(form.Document.FileName, bytes);
                    if (!check.IsValid) ModelState.AddModelError(nameof(form.Document), check.Error!);
                    safeName = check.SafeFileName;
                    contentType = check.ContentType;
                }
            }

            if (!ModelState.IsValid) return ShowForm(form);

            var project = PortfolioCatalog.Find(form.ProjectSlug);
            QuoteDto result;
            try
            {
                var desc = form.Description.Trim();
                if (project != null) desc += $" [Reference Project: {project.Name}]";
                if (!string.IsNullOrWhiteSpace(form.Company)) desc += $" [Company: {form.Company.Trim()}]";
                if (bytes.Length > 0) desc += $" [Attachment: {safeName}]";

                result = await _api.CreateQuoteAsync(new QuoteDtoForCreation
                {
                    ClientName = form.FullName.Trim() + (string.IsNullOrWhiteSpace(form.Company) ? "" : $" ({form.Company.Trim()})"),
                    Email = form.Email.Trim(),
                    Phone = form.Phone.Trim(),
                    ProjectType = string.IsNullOrWhiteSpace(form.ProjectType) ? (project?.ProjectType ?? "Building Construction") : form.ProjectType,
                    SiteLocation = form.SiteLocation.Trim(),
                    EstimatedBudget = form.Budget ?? 0,
                    Budget = form.Budget ?? 0,
                    ProjectDescription = desc
                });

                if (bytes.Length > 0)
                {
                    try
                    {
                        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proposals");
                        Directory.CreateDirectory(uploadsDir);
                        var filePath = Path.Combine(uploadsDir, $"{result.Reference}_{safeName}");
                        await System.IO.File.WriteAllBytesAsync(filePath, bytes, ct);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Failed to save uploaded proposal document to disk");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning(ex, "API unreachable while submitting a proposal");
                ModelState.AddModelError(string.Empty, "Our system is temporarily unavailable. Nothing was lost – please try again in a few minutes, or call 011 887 7240.");
                return ShowForm(form);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error submitting proposal");
                ModelState.AddModelError(string.Empty, "We could not submit your proposal. Please verify the information and try again.");
                return ShowForm(form);
            }

            TempData["ProposalEmail"] = form.Email.Trim();
            return RedirectToAction(nameof(Confirmation), new { reference = result.Reference });
        }

        [HttpGet]
        public IActionResult Confirmation(string reference)
        {
            ViewData["Reference"] = reference;
            ViewData["Email"] = TempData["ProposalEmail"];
            return View();
        }

        [HttpGet]
        public IActionResult Track(string? reference) => View(new TrackForm { Reference = reference ?? string.Empty });

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("track")]
        public async Task<IActionResult> Track(TrackForm form)
        {
            if (!ModelState.IsValid) return View(form);
            try
            {
                var quote = await _api.TrackProposalAsync(form.Reference, form.Email);

                if (quote == null)
                {
                    ModelState.AddModelError(string.Empty, "We could not find a proposal with those details. Check the reference and the email you used.");
                    return View(form);
                }

                var status = new ProposalStatusDto
                {
                    Reference = quote.Reference,
                    ProjectOfInterest = quote.ProjectType,
                    Status = quote.Status,
                    SubmittedAt = quote.RequestDate,
                    Note = null
                };

                ViewData["Status"] = status;
                return View(form);
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "Tracking is temporarily unavailable. Please try again shortly.");
                return View(form);
            }
        }

        private IActionResult ShowForm(ProposalForm form)
        {
            ViewData["Project"] = PortfolioCatalog.Find(form.ProjectSlug);
            return View("Submit", form);
        }
    }
}
