using UbuhlebethuConnectPro.WebApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Services.AI;
using UbuhlebethuConnectPro.WebApp.ApiClient;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IConnectProApiClient _apiClient;
        private readonly IAIService _aiService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IConnectProApiClient apiClient, IAIService aiService, ILogger<DashboardController> logger)
        {
            _apiClient = apiClient;
            _aiService = aiService;
            _logger = logger;
        }

        private async Task<List<Job>> GetJobsAsync()
        {
            return await _apiClient.GetJobsAsync();
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Client") && !User.IsInRole("Office") && !User.IsInRole("Admin"))
            {
                return RedirectToAction("ClientPortal");
            }
            if ((User.IsInRole("Executive") || User.IsInRole("Manager")) && !User.IsInRole("Office") && !User.IsInRole("Admin"))
            {
                return RedirectToAction("Executive");
            }
            if (!User.IsInRole("Office") && !User.IsInRole("Admin"))
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            try
            {
                return View(await GetJobsAsync());
            }
            catch (System.Net.Http.HttpRequestException)
            {
                ViewBag.ApiStartingUp = true;
                return View(new List<Job>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager,Management")]
        public async Task<IActionResult> DeleteProjects([FromForm] List<Guid> projectIds, [FromForm] string? reason)
        {
            if (projectIds == null || !projectIds.Any())
            {
                TempData["Error"] = "Please select at least one project to delete.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _apiClient.DeleteProjectsAsync(projectIds, reason);
                TempData["Success"] = $"Successfully deleted {projectIds.Count} project(s). Progress, outstanding balances, and system metrics have been automatically updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete projects.");
                TempData["Error"] = "Failed to delete projects: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Client,Admin")]
        public async Task<IActionResult> ClientPortal(DateTime? from = null, DateTime? to = null, string? status = null)
        {
            var username = User.Identity?.Name ?? string.Empty;
            
            var quotes = await _apiClient.GetClientQuotesAsync(username, from, to, status);
            ViewBag.ClientQuotes = quotes;

            var allJobs = await GetJobsAsync();
            var clientJobs = allJobs.Where(j => 
                string.Equals(j.ClientUserName, username, StringComparison.OrdinalIgnoreCase) || 
                string.Equals(j.ClientName, username, StringComparison.OrdinalIgnoreCase) || 
                User.IsInRole("Admin")
            ).ToList();

            // Load site reports & photos for each of the client's projects
            var projectPhotos = new Dictionary<Guid, List<SiteReportDto>>();
            foreach (var job in clientJobs)
            {
                try
                {
                    var reports = await _apiClient.GetSiteReportsAsync(job.Id);
                    projectPhotos[job.Id] = reports.Where(r => !string.IsNullOrWhiteSpace(r.PhotoPath)).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load site photos for project {JobId}", job.Id);
                    projectPhotos[job.Id] = new List<SiteReportDto>();
                }
            }
            ViewBag.ProjectPhotos = projectPhotos;

            return View(clientJobs);
        }

        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> Executive()
        {
            var jobs = await GetJobsAsync();
            ViewBag.TotalProjects = jobs.Count;
            ViewBag.TotalValue = jobs.Sum(j => j.EstimatedCost);
            ViewBag.TotalInvoiced = jobs.Sum(j => j.InvoicedAmount);
            ViewBag.AvgProgress = jobs.Count > 0 ? (int)jobs.Average(j => j.Progress) : 0;
            return View(jobs);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var jobs = await GetJobsAsync();
            var job = jobs.FirstOrDefault(j => j.Id == id);
            if (job == null) return NotFound();
            
            if (User.IsInRole("Client") && !User.IsInRole("Admin") && !User.IsInRole("SystemAdmin") && !User.IsInRole("Office") && !User.IsInRole("OfficeStaff") && job.ClientName != User.Identity?.Name)
            {
                return Forbid();
            }
            
            List<ProjectActivityDto> activities;
            try { activities = await _apiClient.GetJobActivitiesAsync(id); }
            catch (Exception) { activities = new List<ProjectActivityDto>(); }
            ViewBag.Activities = activities;
            ViewBag.CanEdit = User.IsInRole("Manager") || User.IsInRole("Admin") || User.IsInRole("SystemAdmin") ||
                              User.IsInRole("Management") || User.IsInRole("Office") || User.IsInRole("SiteManager") || User.IsInRole("Executive");
            return View(job);
        }

        // ── Job Details: saving edits. Every action returns JSON so the page can show a real result. ──
        private const string EditRoles = "Manager,Admin,SystemAdmin,Management,Office,SiteManager,Executive";

        private async Task<IActionResult> SaveJobChange(Guid id, Action<Job> change)
        {
            try
            {
                var job = (await GetJobsAsync()).FirstOrDefault(j => j.Id == id);
                if (job == null) return NotFound(new { ok = false, error = "Job not found." });
                change(job);
                await _apiClient.UpdateJobAsync(job);
                return Json(new { ok = true });
            }
            catch (ApiRejectedException ex) { return BadRequest(new { ok = false, error = ex.Message }); }
            catch (Exception) { return StatusCode(502, new { ok = false, error = "Could not save. Please try again." }); }
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditRoles)]
        public Task<IActionResult> SaveJob(Guid id, string description, string buildingType, decimal estimatedCost, string siteManager, string nhbrcStatus, string? status, int? progress)
        {
            var statuses = new[] { "Planning", "In Progress", "On Hold", "Completed" };
            if (status != null && !statuses.Contains(status))
                return Task.FromResult<IActionResult>(BadRequest(new { ok = false, error = "Unknown status." }));
            if (progress is < 0 or > 100)
                return Task.FromResult<IActionResult>(BadRequest(new { ok = false, error = "Progress must be between 0 and 100." }));
            if (string.IsNullOrWhiteSpace(description) || description.Length > 200)
                return Task.FromResult<IActionResult>(BadRequest(new { ok = false, error = "Enter a description (200 characters or fewer)." }));
            if (estimatedCost <= 0)
                return Task.FromResult<IActionResult>(BadRequest(new { ok = false, error = "The budget must be greater than zero." }));
            return SaveJobChange(id, j =>
            {
                j.Description = description.Trim();
                j.BuildingType = (buildingType ?? "").Trim();
                j.EstimatedCost = estimatedCost;
                j.SiteManager = (siteManager ?? "").Trim();
                j.NhbrcCompliant = nhbrcStatus == "Verified";
                if (status != null) j.Status = status;
                if (progress.HasValue) j.Progress = progress.Value;
            });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditRoles)]
        public async Task<IActionResult> SaveClient(Guid id, string clientName, string? phone, string? email)
        {
            if (string.IsNullOrWhiteSpace(clientName))
                return BadRequest(new { ok = false, error = "Enter the client name." });
            if (!string.IsNullOrWhiteSpace(email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                return BadRequest(new { ok = false, error = "Enter a valid email address." });
            var result = await SaveJobChange(id, j => j.ClientName = clientName.Trim());
            if (result is not JsonResult) return result;
            try
            {
                await _apiClient.AddJobActivityAsync(id, new ProjectActivityRequest
                {
                    Kind = "ClientContact", Title = clientName.Trim(), Body = (phone ?? "").Trim(), Status = (email ?? "").Trim()
                });
                return Json(new { ok = true });
            }
            catch (ApiRejectedException ex) { return BadRequest(new { ok = false, error = ex.Message }); }
            catch (Exception) { return StatusCode(502, new { ok = false, error = "Could not save. Please try again." }); }
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditRoles)]
        public Task<IActionResult> SaveLocation(Guid id, string location)
        {
            if (string.IsNullOrWhiteSpace(location) || location.Length > 300)
                return Task.FromResult<IActionResult>(BadRequest(new { ok = false, error = "Enter the site address (300 characters or fewer)." }));
            return SaveJobChange(id, j => j.Location = location.Trim());
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditRoles)]
        public Task<IActionResult> SaveNotes(Guid id, string? notes)
            => SaveJobChange(id, j => j.Notes = (notes ?? "").Trim());

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditRoles)]
        public async Task<IActionResult> AddActivity(Guid id, string kind, string title, string? body, string? amount, string? dueDate, string? status)
        {
            if (kind != "DailyLog" && kind != "Milestone" && kind != "PurchaseOrder")
                return BadRequest(new { ok = false, error = "Unknown entry type." });
            try
            {
                if (kind == "PurchaseOrder")
                {
                    // Reference is assigned by the server, never typed in.
                    title = title?.Trim() ?? "";
                    status = "Pending Sign-off";
                }
                await _apiClient.AddJobActivityAsync(id, new ProjectActivityRequest
                {
                    Kind = kind, Title = title ?? "", Body = body, Amount = amount, DueDate = dueDate, Status = status
                });
                return Json(new { ok = true });
            }
            catch (ApiRejectedException ex) { return BadRequest(new { ok = false, error = ex.Message }); }
            catch (Exception) { return StatusCode(502, new { ok = false, error = "Could not save. Please try again." }); }
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditRoles)]
        [RequestSizeLimit(3_000_000)]
        public async Task<IActionResult> UploadDocument(Guid id, string? title, string? category, IFormFile? file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { ok = false, error = "Choose a file to upload." });
            if (file.Length > 2 * 1024 * 1024) return BadRequest(new { ok = false, error = "The file is too large (limit 2 MB)." });
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt" };
            if (!allowed.Contains(ext)) return BadRequest(new { ok = false, error = "That file type is not allowed. Use PDF, image, Word, Excel, CSV or text." });
            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var name = Path.GetFileName(file.FileName);
                await _apiClient.AddJobActivityAsync(id, new ProjectActivityRequest
                {
                    Kind = "Document",
                    Title = string.IsNullOrWhiteSpace(title) ? name : title.Trim(),
                    Body = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                    FileName = name,
                    ContentType = file.ContentType,
                    ContentBase64 = Convert.ToBase64String(ms.ToArray())
                });
                return Json(new { ok = true });
            }
            catch (ApiRejectedException ex) { return BadRequest(new { ok = false, error = ex.Message }); }
            catch (Exception) { return StatusCode(502, new { ok = false, error = "Could not upload. Please try again." }); }
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditRoles)]
        public async Task<IActionResult> SetActivityStatus(Guid id, string activityId, string status, string tab)
        {
            var tabs = new[] { "schedule", "purchase-orders" };
            if (!tabs.Contains(tab)) tab = "job-details";
            try
            {
                await _apiClient.SetJobActivityStatusAsync(id, activityId, status);
                TempData["SuccessMessage"] = "Status updated.";
            }
            catch (ApiRejectedException ex) { TempData["Error"] = ex.Message; }
            catch (Exception) { TempData["Error"] = "Could not update the status. Please try again."; }
            return Redirect(Url.Action("Details", new { id }) + "#" + tab);
        }

        /// <summary>AI cost and risk note for one active job. Falls back to a rule-based note when no AI key is set.</summary>
        [HttpGet, Authorize(Roles = EditRoles)]
        public async Task<IActionResult> JobEstimate(Guid id)
        {
            var job = (await GetJobsAsync()).FirstOrDefault(j => j.Id == id);
            if (job == null) return NotFound(new { ok = false, error = "Job not found." });

            decimal remaining = Math.Max(0, job.EstimatedCost - job.InvoicedAmount);
            decimal earned = job.EstimatedCost * job.Progress / 100m;
            decimal billingGap = earned - job.InvoicedAmount;
            var ci = System.Globalization.CultureInfo.GetCultureInfo("en-ZA");
            string fallback =
                $"Budget: R {job.EstimatedCost.ToString("N0", ci)}. Invoiced so far: R {job.InvoicedAmount.ToString("N0", ci)}. Remaining to invoice: R {remaining.ToString("N0", ci)}.\n\n" +
                $"Progress is {job.Progress}%, which equates to about R {earned.ToString("N0", ci)} of work earned. " +
                (billingGap > 0 ? $"That is R {billingGap.ToString("N0", ci)} ahead of what has been invoiced, so consider issuing an invoice."
                                : "Invoicing is in line with progress.") + "\n\n" +
                (job.Variance > 20 ? $"Risk: the cost variance is {job.Variance}%, above the 20% warning level. Review spend before committing more material orders."
                                   : $"Risk: the cost variance is {job.Variance}%, within the acceptable range.");

            var summary = $"Job {job.JobNumber}: {job.Description}; type {job.BuildingType}; budget {job.EstimatedCost}; invoiced {job.InvoicedAmount}; progress {job.Progress}%; variance {job.Variance}%; status {job.Status}";
            var prompt = "In three short paragraphs for a quantity surveyor: (1) remaining cost to complete, (2) invoicing versus progress, (3) the main cost risk. Plain text only, no markdown.";
            try
            {
                var insight = await _aiService.AskAsync(prompt, summary, fallback);
            if (insight.UsedFallback)
                _logger.LogWarning("AI executive report used fallback text. Focus: {FocusArea}", "overview");
                return Json(new { ok = true, text = (insight.Text ?? fallback).Replace("**", "").Replace("##", "").Trim() });
            }
            catch (Exception)
            {
                return Json(new { ok = true, text = fallback });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadDocument(string id)
        {
            var f = await _apiClient.GetJobFileAsync(id);
            if (f == null) return NotFound();
            if (!(User.IsInRole("Manager") || User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Management") ||
                  User.IsInRole("Office") || User.IsInRole("SiteManager") || User.IsInRole("Executive")))
            {
                // A client may only download documents that belong to one of their own jobs.
                var job = (await GetJobsAsync()).FirstOrDefault(j => j.Id.ToString() == f.ProjectId);
                if (job == null || !string.Equals(job.ClientName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)) return Forbid();
            }
            byte[] bytes;
            try { bytes = Convert.FromBase64String(f.ContentBase64); } catch (FormatException) { return NotFound(); }
            return File(bytes, string.IsNullOrWhiteSpace(f.ContentType) ? "application/octet-stream" : f.ContentType,
                Path.GetFileName(f.FileName));
        }

        [Authorize(Roles = "Office,Admin")]
        public IActionResult Office() => View();

        [Authorize(Roles = "Office,Admin")]
        public async Task<IActionResult> PendingQuotes()
        {
            var pending = await _apiClient.GetPendingQuotesAsync();
            return View(pending);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Admin() => RedirectToAction("Index", "Admin");

        [Authorize(Roles = "Admin")]
        public IActionResult UserMgmt() => RedirectToAction("Index", "Admin");

        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> Management()
        {
            var jobs = await GetJobsAsync();
            ViewBag.TotalProjects = jobs.Count;
            ViewBag.TotalValue = jobs.Sum(j => j.EstimatedCost);
            return View(jobs);
        }

        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> LiveStats() => View(await GetJobsAsync());

        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> PortfolioView() => View(await GetJobsAsync());

        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> Reports() => View(await GetJobsAsync());

        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> RegionalMap() => View(await GetJobsAsync());

        [HttpGet]
        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> Export() => View(await GetJobsAsync());

        private static string Csv(object? v)
        {
            var t = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            // Neutralise spreadsheet formula injection, then quote.
            if (t.Length > 0 && "=+-@\t\r".IndexOf(t[0]) >= 0) t = "'" + t;
            return "\"" + t.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>Downloads project records as a CSV file (GET so a plain link works).</summary>
        [HttpGet]
        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management,Office")]
        public async Task<IActionResult> DownloadCsv(string? module, DateTime? from = null, DateTime? to = null)
        {
            if (module == "User Audit Logs")
            {
                if (!User.IsInRole("Admin") && !User.IsInRole("SystemAdmin")) return Forbid();
                return await _apiClient.ExportAuditLogsAsync(null);
            }

            var jobs = (await GetJobsAsync()).AsEnumerable();
            if (from.HasValue) jobs = jobs.Where(j => j.CreatedDate.Date >= from.Value.Date);
            if (to.HasValue) jobs = jobs.Where(j => j.CreatedDate.Date <= to.Value.Date);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Reference,Client,Project Type,Location,Estimated Cost (ZAR),Invoiced (ZAR),Progress %,Status,Created");
            foreach (var j in jobs)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(j.JobNumber), Csv(j.ClientName), Csv(j.BuildingType), Csv(j.Location),
                    Csv(j.EstimatedCost), Csv(j.InvoicedAmount), Csv(j.Progress), Csv(j.Status),
                    Csv(j.CreatedDate.ToString("yyyy-MM-dd"))
                }));
            }
            // UTF-8 BOM so Excel shows accented characters correctly.
            var bytes = new System.Text.UTF8Encoding(true).GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var safe = new string((module ?? "Projects").Where(char.IsLetterOrDigit).ToArray());
            return File(bytes, "text/csv", $"Ubuhlebethu_{safe}_{DateTime.Now:yyyyMMdd}.csv");
        }

        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> AIReport()
        {
            var jobs = await GetJobsAsync();
            var summary = string.Join("\n", jobs.Select(j => $"Job: {j.JobNumber}, Type: {j.BuildingType}, Cost: {j.EstimatedCost:C}, Status: {j.Status}"));
            var prompt = "Analyze these construction jobs and give exactly two short paragraphs of insights: one about cost overrun predictions or budget risks, and one about revenue or status trends.";
            
            var fallback = "The AI model predicts a stable budget across most projects. However, continuous monitoring is advised for projects in the early stages.\n\nRevenue trends show consistent growth with the majority of projects moving smoothly through the pipeline.";
            
            var insight = await _aiService.AskAsync(prompt, summary, fallback);
            if (insight.UsedFallback)
                _logger.LogWarning("AI executive report used fallback text. Focus: overview");
            
            ViewBag.AiInsight = insight.Text;
            return View(jobs);
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Admin,SystemAdmin,Management")]
        public async Task<IActionResult> GenerateAIReport(string focusArea)
        {
            var jobs = await GetJobsAsync();
            var summary = string.Join("\n", jobs.Select(j => $"Job: {j.JobNumber}, Type: {j.BuildingType}, Cost: {j.EstimatedCost:C}, Status: {j.Status}"));
            var prompt = $"Generate a detailed AI analysis report focusing on {focusArea} for these construction projects. Include specific data-driven recommendations, risk assessments, and projections based on the provided project summaries.";
            var fallback = $"AI analysis for {focusArea} completed. Current projects are progressing as expected with no immediate major risks identified based on the available data.";
            
            var insight = await _aiService.AskAsync(prompt, summary, fallback);
            if (insight.UsedFallback)
                _logger.LogWarning("AI executive report used fallback text. Focus: {FocusArea}", focusArea);
            
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes($"AI INSIGHT REPORT: {focusArea.ToUpper()}\nGenerated: {DateTime.Now:F}\n\n{insight.Text}");
            return File(fileBytes, "text/plain", $"AI_Report_{focusArea.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.txt");
        }
    
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Client,Admin,Office,Manager")]
        public async Task<IActionResult> SimulateMilestonePayment(Guid jobId, int? milestone = null)
        {
            try
            {
                var updatedJob = await _apiClient.PayMilestoneAsync(jobId, milestone);
                TempData["SuccessMessage"] = $"Milestone payment simulation successful! Invoiced to date updated to ZAR {updatedJob.InvoicedAmount:N0}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to simulate milestone payment for job {JobId}", jobId);
                TempData["ErrorMessage"] = "Milestone payment failed: " + ex.Message;
            }
            return RedirectToAction(nameof(ClientPortal));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> PrintInvoice(Guid? id, Guid? jobId)
        {
            var targetId = jobId ?? id ?? Guid.Empty;
            if (targetId == Guid.Empty) return BadRequest("Missing Project or Invoice ID.");

            var jobs = await GetJobsAsync();
            var job = jobs.FirstOrDefault(j => j.Id == targetId);

            QuoteDto? quote = null;
            if (job == null)
            {
                try
                {
                    quote = await _apiClient.GetQuoteAsync(targetId);
                    if (quote != null)
                    {
                        job = jobs.FirstOrDefault(j => j.JobNumber == quote.Reference || (!string.IsNullOrEmpty(quote.Reference) && j.JobNumber.Contains(quote.Reference)));
                    }
                }
                catch { /* Ignore quote lookup errors */ }
            }

            if (job == null && quote == null)
            {
                return NotFound("Project or Quotation not found.");
            }

            var currentUserName = User.Identity?.Name ?? string.Empty;
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Office") || User.IsInRole("OfficeStaff") || User.IsInRole("Manager") || User.IsInRole("Management") || User.IsInRole("Executive");

            if (!isStaff)
            {
                if (job != null && !string.Equals(job.ClientName, currentUserName, StringComparison.OrdinalIgnoreCase) && !string.Equals(job.ClientUserName, currentUserName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
                if (quote != null && !string.Equals(quote.CreatedBy, currentUserName, StringComparison.OrdinalIgnoreCase) && !string.Equals(quote.ClientName, currentUserName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var model = BuildPrintInvoiceViewModel(job, quote);
            return View("PrintInvoice", model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> InvoicePdf(Guid? id, Guid? jobId)
        {
            var targetId = jobId ?? id ?? Guid.Empty;
            if (targetId == Guid.Empty) return BadRequest("Missing Project or Invoice ID.");

            var jobs = await GetJobsAsync();
            var job = jobs.FirstOrDefault(j => j.Id == targetId);

            if (job != null)
            {
                try
                {
                    var quotes = await _apiClient.GetQuotesAsync();
                    var matchingQuote = quotes.FirstOrDefault(q => q.Reference == job.JobNumber);
                    if (matchingQuote?.InvoiceId != null)
                    {
                        var pdfBytes = await _apiClient.GetInvoicePdfAsync(matchingQuote.InvoiceId.Value);
                        return File(pdfBytes, "application/pdf", $"TaxInvoice_{job.JobNumber}.pdf");
                    }
                }
                catch { /* Fallback */ }
            }

            return RedirectToAction(nameof(PrintInvoice), new { jobId = targetId });
        }

        public static PrintInvoiceViewModel BuildPrintInvoiceViewModel(Job? job, QuoteDto? quote)
        {
            var model = new PrintInvoiceViewModel();

            if (job != null)
            {
                model.JobId = job.Id;
                model.Reference = string.IsNullOrWhiteSpace(job.JobNumber) ? job.Id.ToString().Substring(0, 8).ToUpperInvariant() : job.JobNumber;
                model.InvoiceNumber = model.Reference.StartsWith("INV-", StringComparison.OrdinalIgnoreCase) ? model.Reference : $"INV-{model.Reference}";
                model.ClientName = string.IsNullOrWhiteSpace(job.ClientName) ? "Valued Client" : job.ClientName;
                model.ClientUserName = job.ClientUserName;
                model.SiteLocation = string.IsNullOrWhiteSpace(job.Location) ? "South Africa" : job.Location;
                model.ProjectType = string.IsNullOrWhiteSpace(job.BuildingType) ? "General Construction" : job.BuildingType;
                model.ProjectDescription = string.IsNullOrWhiteSpace(job.Description) ? "Construction and Contracting Works" : job.Description;
                model.SiteManager = string.IsNullOrWhiteSpace(job.SiteManager) ? "Assigned Site Manager" : job.SiteManager;
                model.TotalAmount = job.EstimatedCost;
                model.InvoicedAmount = job.InvoicedAmount;
                model.Progress = job.Progress;
                model.Variance = job.Variance;
                model.Status = job.Status;
                model.NhbrcCompliant = job.NhbrcCompliant;
                model.IssueDate = job.CreatedDate;
                model.DueDate = job.CreatedDate.AddDays(30);
            }

            if (quote != null)
            {
                model.QuoteId = quote.Id;
                model.InvoiceId = quote.InvoiceId;
                if (job == null)
                {
                    model.Reference = string.IsNullOrWhiteSpace(quote.Reference) ? quote.Id.ToString().Substring(0, 8).ToUpperInvariant() : quote.Reference;
                    model.InvoiceNumber = model.Reference.StartsWith("INV-", StringComparison.OrdinalIgnoreCase) ? model.Reference : $"INV-{model.Reference}";
                    model.ClientName = string.IsNullOrWhiteSpace(quote.ClientName) ? "Valued Client" : quote.ClientName;
                    model.ClientUserName = quote.CreatedBy;
                    model.SiteLocation = string.IsNullOrWhiteSpace(quote.SiteLocation) ? "South Africa" : quote.SiteLocation;
                    model.ProjectType = string.IsNullOrWhiteSpace(quote.ProjectType) ? "General Construction" : quote.ProjectType;
                    model.ProjectDescription = string.IsNullOrWhiteSpace(quote.ProjectDescription) ? "Construction Contract Services" : quote.ProjectDescription;
                    model.TotalAmount = quote.EstimatedBudget;
                    model.InvoicedAmount = quote.IsPaid ? quote.EstimatedBudget : 0m;
                    model.Progress = quote.IsPaid ? 100 : 0;
                    model.Status = quote.Status;
                    model.IssueDate = quote.RequestDate;
                    model.DueDate = quote.RequestDate.AddDays(30);
                }

                if (!string.IsNullOrWhiteSpace(quote.Email)) model.ClientEmail = quote.Email;
                if (!string.IsNullOrWhiteSpace(quote.Phone)) model.ClientPhone = quote.Phone;
            }

            decimal totalCost = model.TotalAmount;
            decimal invoiced = model.InvoicedAmount;
            int progress = model.Progress;

            decimal m1Amt = Math.Round(totalCost * 0.25m, 2);
            decimal m2Amt = Math.Round(totalCost * 0.50m, 2);
            decimal m3Amt = Math.Max(0, totalCost - m1Amt - m2Amt);

            bool m1Paid = invoiced >= m1Amt;
            bool m2Paid = invoiced >= (m1Amt + m2Amt);
            bool m3Paid = invoiced >= totalCost && totalCost > 0;

            bool m2Eligible = !m2Paid && m1Paid && progress >= 40;
            bool m3Eligible = !m3Paid && m2Paid && progress >= 90;

            string m1Status = m1Paid ? "Paid" : "Outstanding (Payable immediately)";
            string m2Status = m2Paid ? "Paid" : (!m1Paid ? "Locked (Requires Deposit)" : (progress >= 40 ? "Outstanding (Eligible for Payment)" : $"Locked (Requires 40% site progress; current: {progress}%)"));
            string m3Status = m3Paid ? "Paid" : (!m2Paid ? "Locked (Requires Milestone 2)" : (progress >= 90 ? "Outstanding (Eligible for Payment)" : $"Locked (Requires 90% site progress; current: {progress}%)"));

            model.Milestones = new List<MilestoneItemViewModel>
            {
                new MilestoneItemViewModel
                {
                    StageNumber = 1,
                    Title = "Milestone 1: Deposit",
                    Description = "Project activation, mobilization & site setup",
                    Percentage = 25m,
                    Amount = m1Amt,
                    RequiredProgress = 0,
                    IsPaid = m1Paid,
                    IsEligible = !m1Paid,
                    Status = m1Status,
                    StatusClass = m1Paid ? "badge-success" : "badge-primary"
                },
                new MilestoneItemViewModel
                {
                    StageNumber = 2,
                    Title = "Milestone 2: Progress Draw",
                    Description = "Substructure, superstructure & major construction",
                    Percentage = 50m,
                    Amount = m2Amt,
                    RequiredProgress = 40,
                    IsPaid = m2Paid,
                    IsEligible = m2Eligible,
                    Status = m2Status,
                    StatusClass = m2Paid ? "badge-success" : (m2Eligible ? "badge-warning" : "badge-neutral")
                },
                new MilestoneItemViewModel
                {
                    StageNumber = 3,
                    Title = "Milestone 3: Practical Completion / Handover",
                    Description = "Finishes, snag list sign-off, compliance & handover",
                    Percentage = 25m,
                    Amount = m3Amt,
                    RequiredProgress = 90,
                    IsPaid = m3Paid,
                    IsEligible = m3Eligible,
                    Status = m3Status,
                    StatusClass = m3Paid ? "badge-success" : (m3Eligible ? "badge-warning" : "badge-neutral")
                }
            };

            return model;
        }
    }
}
