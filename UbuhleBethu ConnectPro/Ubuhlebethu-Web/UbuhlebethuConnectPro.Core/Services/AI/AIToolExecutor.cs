using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UbuhlebethuConnectPro.Core.Repositories;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    /// <summary>
    /// Live data and capability tools the LLM (or the local engine) can call. This is the ONLY door between the
    /// assistant and the database, so every tool enforces authentication and role/ownership rules itself -
    /// nothing is trusted from the model or from chat text (prevents IDOR and privilege escalation).
    /// </summary>
    public class AIToolExecutor
    {
        private readonly IProjectRepository _projects;
        private readonly IExpenseRepository _expenses;
        private readonly ISiteReportRepository _siteReports;
        private readonly IUserAIMemoryService _memory;
        private readonly IQuotationRepository? _quotes;
        private readonly IInvoiceRepository? _invoices;
        private readonly ILogger<AIToolExecutor> _log;

        private string? _userName;
        private string? _roleRaw;
        private string _role = "Anonymous";
        private bool _authenticated;

        public AIToolExecutor(
            IProjectRepository projects,
            IExpenseRepository expenses,
            ISiteReportRepository siteReports,
            IUserAIMemoryService memory,
            ILogger<AIToolExecutor> log,
            IQuotationRepository? quotes = null,
            IInvoiceRepository? invoices = null)
        {
            _projects = projects;
            _expenses = expenses;
            _siteReports = siteReports;
            _memory = memory;
            _log = log;
            _quotes = quotes;
            _invoices = invoices;
        }

        /// <summary>Sets the verified caller identity. Authenticated is inferred from a username plus a known role.</summary>
        public void SetUserContext(string? userName, string? userRole) =>
            SetUserContext(userName, userRole, !string.IsNullOrWhiteSpace(userName) && SystemKnowledgeBase.CanonicalRole(userRole) != "Anonymous");

        public void SetUserContext(string? userName, string? userRole, bool isAuthenticated)
        {
            _userName = userName;
            _roleRaw = userRole;
            _role = SystemKnowledgeBase.CanonicalRole(userRole);
            _authenticated = isAuthenticated;
        }

        private bool IsRole(params string[] roles) => roles.Contains(_role, StringComparer.OrdinalIgnoreCase);
        private bool OwnsProject(Models.Project p) =>
            !string.IsNullOrWhiteSpace(p.ClientUserName) && string.Equals(p.ClientUserName, _userName, StringComparison.OrdinalIgnoreCase);

        // ───────────────────────── Tool catalogue ─────────────────────────

        private static AITool Tool(string name, string description, Dictionary<string, object>? props = null, params string[] required) => new()
        {
            Name = name,
            Description = description,
            Parameters = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = props ?? new Dictionary<string, object>(),
                ["required"] = required
            }
        };

        private static Dictionary<string, object> Str(string description) => new() { ["type"] = "string", ["description"] = description };
        private static Dictionary<string, object> Num(string description) => new() { ["type"] = "number", ["description"] = description };

        /// <summary>Returns the complete set of tool definitions passed to the LLM.</summary>
        public static List<AITool> GetAvailableTools()
        {
            var projectId = new Dictionary<string, object> { ["project_id"] = Str("The UUID of the project (use the one the user selected/pinned if they don't give one).") };
            return new List<AITool>
            {
                Tool("get_system_guide",
                    "Get exact step-by-step operating procedure, navigation path, authorised roles, a permission verdict for THIS user and pro tips for any task in UbuhleBethu ConnectPro. Always call before explaining a workflow.",
                    new() { ["action_or_topic"] = Str("Workflow key or plain-language task, e.g. 'request_quote', 'cost_quote', 'approve_quote', 'invoicing_and_milestones', 'create_project', 'review_attendance', 'expenses_and_budget', 'mobile_worker_attendance', 'user_management', 'support_ticket', 'account_security', 'how do I pay my invoice'.") },
                    "action_or_topic"),
                Tool("get_my_capabilities",
                    "List everything the current user's role CAN do in the system and what it cannot (with which roles can). Use for 'what can I do?' and 'who can do X?' questions."),
                Tool("estimate_construction_cost",
                    "Generate an itemised construction cost / quote estimate in ZAR from building type, floor area (m2), location and finish quality. Output detail adapts to the caller's role.",
                    new()
                    {
                        ["building_type"] = Str("'Residential', 'Luxury Residential', 'Commercial', 'Clinic', 'School', 'Industrial', 'Civil/Paving' or 'RDP/Social housing'"),
                        ["area_square_meters"] = Num("Floor area in square meters"),
                        ["location"] = Str("City or province, e.g. 'Gauteng', 'Durban', 'Cape Town', 'Rural'"),
                        ["quality_tier"] = Str("'Standard', 'Medium' or 'Premium'")
                    },
                    "building_type", "area_square_meters"),
                Tool("list_projects", "List the projects this user is allowed to see (title, status, progress, id)."),
                Tool("get_project_status", "Status, progress %, geofence, site manager and notes for one project.", projectId, "project_id"),
                Tool("get_project_budget", "Estimated budget, approved expenses, invoiced amount and remaining balance for a project (Admin, Manager, Executive, Office, Site Manager, or the owning Client).", projectId, "project_id"),
                Tool("get_expenses", "Logged expenses with status for a project (Admin, Manager, Office, Site Manager only).", projectId, "project_id"),
                Tool("get_site_reports", "Most recent site reports, photo status, GPS verification and progress logs for a project.", projectId, "project_id"),
                Tool("get_my_quotes", "Quotations: a Client sees their own quotes; staff see the active quote pipeline. Includes reference, status, budget."),
                Tool("get_my_invoices", "Invoices: a Client sees invoices for their own quotes; Office/Manager/Admin see the most recent invoices with status, amount and due date."),
                Tool("remember_user_fact",
                    "Store a lasting user preference, working context or note (never passwords, keys or ID numbers).",
                    new() { ["fact"] = Str("The fact or preference to remember, phrased as a short statement") }, "fact"),
                Tool("forget_user_fact",
                    "Delete remembered facts that contain a keyword when the user asks to forget something.",
                    new() { ["keyword"] = Str("Word or phrase identifying the fact(s) to delete") }, "keyword"),
                Tool("get_my_memory", "Recall what the assistant currently remembers about this user.")
            };
        }

        /// <summary>Execute a single tool call and return its result.</summary>
        public async Task<AIToolResult> ExecuteAsync(AIToolCall call, CancellationToken ct = default)
        {
            try
            {
                return call.Name switch
                {
                    "get_system_guide"           => GetSystemGuide(call),
                    "get_my_capabilities"        => new AIToolResult { ToolName = call.Name, Result = SystemKnowledgeBase.BuildCapabilitySummary(_roleRaw) },
                    "estimate_construction_cost" => EstimateConstructionCost(call),
                    "get_project_status"         => await GetProjectStatusAsync(call, ct),
                    "get_project_budget"         => await GetProjectBudgetAsync(call, ct),
                    "get_expenses"               => await GetExpensesAsync(call, ct),
                    "get_site_reports"           => await GetSiteReportsAsync(call, ct),
                    "list_projects"              => await ListProjectsAsync(ct),
                    "get_my_quotes"              => await GetMyQuotesAsync(ct),
                    "get_my_invoices"            => await GetMyInvoicesAsync(ct),
                    "remember_user_fact"         => await RememberUserFactAsync(call),
                    "forget_user_fact"           => await ForgetUserFactAsync(call),
                    "get_my_memory"              => await GetMyMemoryAsync(),
                    _ => new AIToolResult { ToolName = call.Name, Result = $"Unknown tool: {call.Name}", Success = false }
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Tool execution failed for {Tool}", call.Name);
                // Do not leak exception internals to the model/user.
                return new AIToolResult { ToolName = call.Name, Result = $"The {call.Name} lookup failed temporarily. Please try again.", Success = false };
            }
        }

        private AIToolResult Denied(string tool, string message) => new() { ToolName = tool, Result = message, Success = false };
        private AIToolResult NeedLogin(string tool) =>
            Denied(tool, "Access Restricted: this information is only available after signing in. Please log in and ask again.");

        private bool TryProjectId(AIToolCall call, out Guid id, out AIToolResult error)
        {
            id = Guid.Empty; error = null!;
            if (call.Arguments.TryGetValue("project_id", out var s) && Guid.TryParse(s, out id)) return true;
            error = Denied(call.Name, "A valid project_id (UUID) is required. Call list_projects to find the right project.");
            return false;
        }

        // ───────────────────────── Knowledge tools ─────────────────────────

        private AIToolResult GetSystemGuide(AIToolCall call)
        {
            var topic = call.Arguments.TryGetValue("action_or_topic", out var val) ? val : "general";
            var a = SystemKnowledgeBase.AssessCapability(_roleRaw, topic);

            var sb = new StringBuilder();
            sb.AppendLine($"Guide: {a.ActionTitle}");
            sb.AppendLine($"Verdict for this user ({SystemKnowledgeBase.CanonicalRole(_roleRaw)}): {(a.CanPerform ? "ALLOWED" : "NOT ALLOWED")}");
            sb.AppendLine(a.PermissionExplanation);
            sb.AppendLine($"Navigation: {a.NavigationPath}");
            sb.AppendLine($"Authorised roles: {string.Join(", ", a.AllowedRoles)}");
            sb.AppendLine("Steps:");
            foreach (var s in a.ExactSteps) sb.AppendLine(s);
            if (!string.IsNullOrWhiteSpace(a.ContactRecommendation)) sb.AppendLine($"If not allowed / next step: {a.ContactRecommendation}");
            if (a.ProTips.Count > 0)
            {
                sb.AppendLine("Tips:");
                foreach (var t in a.ProTips) sb.AppendLine($"- {t}");
            }
            return new AIToolResult { ToolName = "get_system_guide", Result = sb.ToString() };
        }

        private AIToolResult EstimateConstructionCost(AIToolCall call)
        {
            var bType = call.Arguments.TryGetValue("building_type", out var b) ? b : "Residential";
            decimal area = 150m;
            if (call.Arguments.TryGetValue("area_square_meters", out var aStr) &&
                decimal.TryParse(aStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                area = Math.Min(parsed, 500_000m);
            var loc = call.Arguments.TryGetValue("location", out var l) ? l : "Gauteng";
            var grade = call.Arguments.TryGetValue("quality_tier", out var g) ? g : "Standard";

            var e = ConstructionCostEstimator.GenerateEstimate(bType, area, loc, grade);

            string result;
            if (IsRole("Client", "Anonymous", "FieldWorker"))
            {
                // Clients/visitors get a clean indicative figure + range, not internal rates/markups.
                var low = Math.Round(e.TotalIncVat * 0.9m, 0);
                var high = Math.Round(e.TotalIncVat * 1.15m, 0);
                result = $"Indicative estimate for {e.AreaM2:N0} m2 {e.BuildingType} ({e.Location}, {e.FinishGrade} finish):\n" +
                         $"- Expected total incl. 15% VAT: R{e.TotalIncVat:N0}\n" +
                         $"- Realistic range: R{low:N0} - R{high:N0}\n" +
                         $"- Estimated build time: about {e.EstimatedWeeks} weeks\n" +
                         $"- Typical payment schedule (ex VAT): deposit R{e.Milestone1Deposit:N0}, roof height R{e.Milestone2RoofHeight:N0}, practical completion R{e.Milestone3PracticalCompletion:N0}, retention R{e.Milestone4Retention:N0}\n" +
                         "Indicative only - excludes land, professional fees and abnormal ground conditions. A binding price requires a formal quote request reviewed by our QS and approved by a manager.";
            }
            else
            {
                result = e.FormattedReport +
                         $"\nStaff note: base R{e.BaseRatePerM2:N0}/m2 adjusted to R{e.AdjustedRatePerM2:N0}/m2 for location/finish. Add company markup (typically 15-25%) and at least 10% contingency on civil/clinic work before issuing the quote.";
            }
            return new AIToolResult { ToolName = "estimate_construction_cost", Result = result };
        }

        // ───────────────────────── Live data tools ─────────────────────────

        private async Task<AIToolResult> GetProjectStatusAsync(AIToolCall call, CancellationToken ct)
        {
            if (!_authenticated) return NeedLogin(call.Name);
            if (!TryProjectId(call, out var id, out var err)) return err;

            var project = await _projects.GetByIdAsync(id, ct);
            if (project == null || project.IsDeleted) return Denied(call.Name, $"No project found with ID {id}.");

            if (IsRole("Client") && !OwnsProject(project))
                return Denied(call.Name, "Security Notice: You are not authorized to view details for this project.");

            if (!string.IsNullOrWhiteSpace(_userName))
                await _memory.SetPinnedProjectAsync(_userName, project.Id, project.Title);

            var result = $"Project: {project.Title}\n" +
                         $"Status: {project.Status}\n" +
                         $"Location: {project.Location}\n" +
                         $"Progress: {project.ProgressPercent}%\n" +
                         $"Client: {project.ClientName}\n" +
                         $"Site Manager: {project.SiteManagerName}\n" +
                         $"NHBRC Compliant: {(project.NhbrcCompliant ? "Yes" : "No")}\n" +
                         $"Geofence Radius: {project.GeofenceRadiusMeters:F0}m\n" +
                         (IsRole("Client", "FieldWorker") ? string.Empty : $"Variance: {project.VariancePercent}%\n") +
                         $"Notes: {(string.IsNullOrEmpty(project.Notes) ? "None" : project.Notes)}";
            return new AIToolResult { ToolName = call.Name, Result = result };
        }

        private async Task<AIToolResult> GetProjectBudgetAsync(AIToolCall call, CancellationToken ct)
        {
            if (!_authenticated) return NeedLogin(call.Name);
            if (IsRole("FieldWorker"))
                return Denied(call.Name, "Access Restricted: Field workers do not have clearance to view financial budget balances. Please contact the Site Manager or QS.");
            if (!TryProjectId(call, out var id, out var err)) return err;

            var project = await _projects.GetByIdAsync(id, ct);
            if (project == null || project.IsDeleted) return Denied(call.Name, $"No project found with ID {id}.");

            if (IsRole("Client") && !OwnsProject(project))
                return Denied(call.Name, "Security Notice: You are not authorized to access this project's budget.");

            var expenses = await _expenses.GetByProjectAsync(id, ct);
            var approved = expenses.Where(e => e.Status == "Approved").Sum(e => e.Amount);
            var remaining = project.EstimatedCost - approved;

            var result = $"Budget Overview for: {project.Title}\n" +
                         $"- Estimated Total Budget: R{project.EstimatedCost:N2}\n" +
                         $"- Approved Site Expenses: R{approved:N2}\n" +
                         $"- Remaining Balance: R{remaining:N2}\n" +
                         $"- Invoiced To Date: R{project.InvoicedToDate:N2}\n" +
                         $"- Budget Health: {(remaining < 0 ? "OVER BUDGET" : "Within Budget")}";
            return new AIToolResult { ToolName = call.Name, Result = result };
        }

        private async Task<AIToolResult> GetExpensesAsync(AIToolCall call, CancellationToken ct)
        {
            if (!_authenticated) return NeedLogin(call.Name);
            if (!IsRole("Admin", "Manager", "Office", "SiteManager", "Executive"))
                return Denied(call.Name, "Access Restricted: Detailed internal expense ledgers are restricted to Management, Office/QS, and Site Managers.");
            if (!TryProjectId(call, out var id, out var err)) return err;

            var expenses = await _expenses.GetByProjectAsync(id, ct);
            if (!expenses.Any()) return new AIToolResult { ToolName = call.Name, Result = "No expenses recorded for this project." };

            var lines = expenses.Take(15).Select(e =>
                $"- [{e.Status}] R{e.Amount:N2} | {e.ItemDescription} (Logged by {e.ManagerName} on {e.CreatedAt:dd MMM yyyy})");
            return new AIToolResult { ToolName = call.Name, Result = $"Expenses ({expenses.Count} total, newest 15 shown):\n{string.Join("\n", lines)}" };
        }

        private async Task<AIToolResult> GetSiteReportsAsync(AIToolCall call, CancellationToken ct)
        {
            if (!_authenticated) return NeedLogin(call.Name);
            if (!TryProjectId(call, out var id, out var err)) return err;

            var project = await _projects.GetByIdAsync(id, ct);
            if (project == null || project.IsDeleted) return Denied(call.Name, $"No project found with ID {id}.");
            if (IsRole("Client") && !OwnsProject(project))
                return Denied(call.Name, "Security Notice: Not authorized to view site reports for this project.");

            var reports = await _siteReports.GetByProjectAsync(id, 10, ct);
            var active = reports.Where(r => !r.IsDeleted).ToList();
            if (!active.Any()) return new AIToolResult { ToolName = call.Name, Result = "No site reports found for this project." };

            var lines = active.Select(r =>
                $"- {r.LoggedAt:dd MMM yyyy HH:mm} [{r.ConstructionStage}] {r.ProgressPercent}%: {r.Description} " +
                $"by {r.ReportedBy} {(r.IsOffSite ? $"[Off-Site: {r.DistanceFromSiteMeters:F0}m away]" : "[On-Site GPS verified]")} " +
                $"{(string.IsNullOrWhiteSpace(r.PhotoPath) ? "(No photo)" : "(Photo attached)")}");
            return new AIToolResult { ToolName = call.Name, Result = $"Recent Site Reports ({active.Count}):\n{string.Join("\n", lines)}" };
        }

        private async Task<AIToolResult> ListProjectsAsync(CancellationToken ct)
        {
            if (!_authenticated) return NeedLogin("list_projects");

            var projects = await _projects.GetAllAsync(ct);
            var active = projects.Where(p => !p.IsDeleted);
            if (IsRole("Client")) active = active.Where(OwnsProject);
            var list = active.OrderByDescending(p => p.StartDate).Take(25).ToList();

            if (list.Count == 0) return new AIToolResult { ToolName = "list_projects", Result = "No active projects found." };
            var lines = list.Select(p => $"- [{p.Status}] {p.Title} (ID: {p.Id}, Progress: {p.ProgressPercent}%, Site Manager: {p.SiteManagerName})");
            return new AIToolResult { ToolName = "list_projects", Result = $"Projects ({list.Count} shown):\n{string.Join("\n", lines)}" };
        }

        private async Task<AIToolResult> GetMyQuotesAsync(CancellationToken ct)
        {
            if (!_authenticated) return NeedLogin("get_my_quotes");
            if (_quotes == null) return Denied("get_my_quotes", "Quote lookup is not available right now.");
            if (IsRole("FieldWorker", "SiteManager"))
                return Denied("get_my_quotes", "Access Restricted: quotations are handled by Clients, Office/QS and Managers.");

            List<Models.QuoteRequest> quotes;
            if (IsRole("Client"))
                quotes = await _quotes.GetByClientAsync(_userName ?? string.Empty, null, null, null, ct);
            else
                quotes = await _quotes.GetByStatusesAsync(new[] { "Submitted", "Reviewed", "Approved", "Invoiced", "Paid" }, ct);

            var list = quotes.Where(q => !q.IsDeleted).OrderByDescending(q => q.RequestDate).Take(15).ToList();
            if (list.Count == 0) return new AIToolResult { ToolName = "get_my_quotes", Result = "No quotations found." };

            var lines = list.Select(q => $"- {q.Reference} | {q.Status} | {q.ProjectType} @ {q.SiteLocation} | budget R{q.EstimatedBudget:N0} | requested {q.RequestDate:dd MMM yyyy}{(IsRole("Client") ? "" : $" | client {q.ClientName}")}{(q.IsPaid ? " | PAID" : "")}");
            return new AIToolResult { ToolName = "get_my_quotes", Result = $"Quotations ({list.Count} shown):\n{string.Join("\n", lines)}" };
        }

        private async Task<AIToolResult> GetMyInvoicesAsync(CancellationToken ct)
        {
            if (!_authenticated) return NeedLogin("get_my_invoices");
            if (_invoices == null) return Denied("get_my_invoices", "Invoice lookup is not available right now.");
            if (!IsRole("Client", "Office", "Manager", "Admin", "Executive"))
                return Denied("get_my_invoices", "Access Restricted: invoices are visible to Clients (their own), Office/QS, Managers and Admins.");

            var invoices = new List<Models.Invoice>();
            if (IsRole("Client"))
            {
                if (_quotes == null) return Denied("get_my_invoices", "Invoice lookup is not available right now.");
                var mine = await _quotes.GetByClientAsync(_userName ?? string.Empty, null, null, null, ct);
                foreach (var q in mine.Where(q => !q.IsDeleted).Take(20))
                {
                    var inv = await _invoices.GetByQuoteIdAsync(q.Id, ct);
                    if (inv != null && !inv.IsDeleted) invoices.Add(inv);
                }
            }
            else
            {
                invoices = (await _invoices.GetRecentAsync(15, ct)).Where(i => !i.IsDeleted).ToList();
            }

            if (invoices.Count == 0) return new AIToolResult { ToolName = "get_my_invoices", Result = "No invoices found." };
            var lines = invoices.OrderByDescending(i => i.IssueDate).Take(15).Select(i =>
                $"- Invoice {i.Id.ToString()[..8]} | {i.Status} | R{i.Amount:N2} (paid R{i.PaidAmount:N2}) | issued {i.IssueDate:dd MMM yyyy} | due {i.DueDate:dd MMM yyyy}");
            return new AIToolResult { ToolName = "get_my_invoices", Result = $"Invoices ({invoices.Count}):\n{string.Join("\n", lines)}" };
        }

        // ───────────────────────── Memory tools ─────────────────────────

        private async Task<AIToolResult> RememberUserFactAsync(AIToolCall call)
        {
            if (!_authenticated) return Denied(call.Name, "I can only keep personal memory for signed-in users.");
            if (!call.Arguments.TryGetValue("fact", out var fact) || string.IsNullOrWhiteSpace(fact))
                return Denied(call.Name, "No fact provided to remember.");
            if (!UserAIMemoryService.IsSafeToStore(fact))
                return Denied(call.Name, "I can't store anything that looks like a password, key, card or ID number. Please rephrase without sensitive details.");

            await _memory.SaveFactAsync(_userName, fact);
            return new AIToolResult { ToolName = call.Name, Result = $"Saved to the user's profile: \"{fact}\"." };
        }

        private async Task<AIToolResult> ForgetUserFactAsync(AIToolCall call)
        {
            if (!_authenticated) return NeedLogin(call.Name);
            if (!call.Arguments.TryGetValue("keyword", out var kw) || string.IsNullOrWhiteSpace(kw))
                return Denied(call.Name, "Tell me what to forget.");
            var n = await _memory.ForgetAsync(_userName, kw);
            return new AIToolResult { ToolName = call.Name, Result = n == 0 ? $"Nothing remembered matched \"{kw}\"." : $"Forgot {n} remembered item(s) matching \"{kw}\"." };
        }

        private async Task<AIToolResult> GetMyMemoryAsync()
        {
            if (!_authenticated) return Denied("get_my_memory", "Memory is only kept for signed-in users.");
            var formatted = await _memory.FormatMemoryForPromptAsync(_userName);
            return new AIToolResult
            {
                ToolName = "get_my_memory",
                Result = string.IsNullOrWhiteSpace(formatted)
                    ? "Nothing is stored for this user yet. They can say 'Remember that...' at any time."
                    : formatted
            };
        }
    }
}
