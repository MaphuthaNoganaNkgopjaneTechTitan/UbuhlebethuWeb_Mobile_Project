using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    /// <summary>
    /// Second half of the knowledge base: extra workflow guides, keyword routing, role canonicalisation and
    /// the compact index/capability summaries fed to the LLM so it knows the whole system.
    /// </summary>
    public static partial class SystemKnowledgeBase
    {
        /// <summary>Collapses the aliases used across web, API and seed data into one canonical role name.</summary>
        public static string CanonicalRole(string? role)
        {
            var r = (role ?? string.Empty).Trim();
            if (r.Length == 0) return "Anonymous";
            return r.ToLowerInvariant() switch
            {
                "admin" or "systemadmin" or "system administrator" => "Admin",
                "manager" or "management" => "Manager",
                "office" or "qs" or "office / qs" => "Office",
                "executive" => "Executive",
                "sitemanager" or "site manager" => "SiteManager",
                "fieldworker" or "field worker" => "FieldWorker",
                "client" => "Client",
                "anonymous" or "user" => "Anonymous",
                _ => r
            };
        }

        public static bool RoleMatches(string? userRole, string allowedRole)
        {
            if (string.Equals(allowedRole, "All Roles", StringComparison.OrdinalIgnoreCase)) return true;
            var user = CanonicalRole(userRole);
            // "Anonymous" (public landing page) only means unauthenticated visitors; "User" is an unknown signed-in role.
            return string.Equals(user, CanonicalRole(allowedRole), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Role match plus the Admin override (Admins can act everywhere except in purely end-user/field-only flows).</summary>
        public static bool CanPerform(string? role, SystemActionGuide guide)
        {
            if (guide.AllowedRoles.Any(r => RoleMatches(role, r))) return true;
            if (CanonicalRole(role) != "Admin") return false;
            var endUserOnly = guide.AllowedRoles.All(r => CanonicalRole(r) is "Client" or "FieldWorker" or "SiteManager");
            return !endUserOnly;
        }

        public static IReadOnlyList<SystemActionGuide> AllGuides => Guides.Concat(ExtraGuides).ToList();

        /// <summary>Keywords/phrases per guide. Multi-word phrases outrank single words when scoring.</summary>
        private static readonly Dictionary<string, string[]> Keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["request_quote"] = new[] { "request a quote", "new quote", "get a quote", "submit quote", "quotation request", "request quote", "ask for a quote", "quote request" },
            ["cost_quote"] = new[] { "cost a quote", "cost quote", "price a quote", "prepare quote", "review quote", "pending quotes", "markup", "bill of quantities", "boq", "qs costing", "prepare a quote", "send to manager" },
            ["approve_quote"] = new[] { "approve quote", "approve a quote", "reject quote", "accept quote", "review quotes", "convert to project", "contract conversion", "quote approval" },
            ["invoicing_and_milestones"] = new[] { "invoice", "invoicing", "milestone", "payment proof", "proof of payment", "billing", "deposit" },
            ["mobile_worker_attendance"] = new[] { "attendance", "clock in", "clock out", "check in", "check-in", "start shift", "start my shift", "start job", "shift", "geofence", "gps verification" },
            ["mobile_work_log_and_photo"] = new[] { "work log", "daily log", "progress photo", "upload photo", "site photo", "photo" },
            ["scan_materials"] = new[] { "barcode", "scan", "material", "inventory", "delivery" },
            ["user_management"] = new[] { "create user", "add user", "new user", "manage users", "deactivate", "reactivate", "delete user", "edit roles", "assign role", "user management", "permissions" },
            ["system_health"] = new[] { "system health", "health check", "monitor", "telemetry", "observability", "uptime", "database health" },
            ["track_quote_status"] = new[] { "track quote", "track my quote", "quote status", "where is my quote", "tracking reference", "proposal status", "track proposal", "track my request" },
            ["accept_quote_and_pay"] = new[] { "accept my quote", "sign quote", "digital signature", "pay invoice", "make payment", "pay my invoice", "pay online", "payment receipt", "accept proposal" },
            ["create_project"] = new[] { "create project", "new project", "start project", "set up project", "open a job", "create a job" },
            ["manage_projects"] = new[] { "project management", "manage projects", "delete project", "remove project", "master project", "project list", "assign site manager" },
            ["record_site_progress_web"] = new[] { "site report", "site progress", "progress percent", "record progress", "update progress", "progress update" },
            ["review_attendance"] = new[] { "approve attendance", "review attendance", "attendance records", "team attendance", "timesheet", "off-site attendance" },
            ["expenses_and_budget"] = new[] { "expense", "budget", "variance", "spend", "over budget", "cost to date" },
            ["support_ticket"] = new[] { "support", "ticket", "help desk", "report a problem", "raise a ticket", "complaint" },
            ["notifications"] = new[] { "notification", "notifications", "alerts", "bell" },
            ["account_security"] = new[] { "password", "change password", "forgot password", "mfa", "two factor", "2fa", "authenticator", "security", "login problem", "log in", "sign in" },
            ["reports_and_exports"] = new[] { "report", "reports", "export", "dashboard", "kpi", "portfolio", "regional map", "live stats", "analytics", "csv", "pdf" },
            ["audit_logs"] = new[] { "audit", "audit log", "audit trail", "who changed", "activity log" },
            ["system_settings"] = new[] { "system settings", "settings", "configuration", "vat rate", "company details" },
            ["ai_cost_estimate"] = new[] { "estimate", "estimation", "how much", "cost of building", "ai draft", "draft estimate", "price per m2", "square meter" },
            ["mobile_app_overview"] = new[] { "mobile app", "android", "app login", "phone app", "offline sync", "sync", "reset password on mobile" },
        };

        private static readonly List<SystemActionGuide> ExtraGuides = new()
        {
            new SystemActionGuide
            {
                ActionKey = "track_quote_status", Category = "Quotations & Tenders",
                Title = "Track a Quotation / Proposal Status",
                AllowedRoles = new() { "Client", "Office", "Manager", "Admin", "Anonymous" },
                NavigationPath = "Client Portal > Quotations (Track) OR public page > Track Proposal",
                Description = "See where a quote sits in the workflow: Draft, Submitted, Reviewed, Approved, Invoiced, Paid.",
                Steps = new()
                {
                    "1. Sign in and open your Client Portal (Dashboard > Client Portal), or use 'Track Proposal' on the public site if you are not signed in.",
                    "2. Open the Quotations list - every quote you created shows its current status.",
                    "3. Click a quote (or enter its tracking reference, e.g. UBU-Q7938 in Track) to see the status timeline and details.",
                    "4. Statuses move Draft > Submitted > Reviewed > Approved > Invoiced > Paid; you act when it reaches Approved."
                },
                Tips = new() { "Keep your tracking reference - it lets you check progress without signing in.", "I can also look up your quotes live: just ask 'what is the status of my quotes?'." }
            },
            new SystemActionGuide
            {
                ActionKey = "accept_quote_and_pay", Category = "Client Portal",
                Title = "Accept a Quotation, Sign and Pay Invoices",
                AllowedRoles = new() { "Client" },
                NavigationPath = "Client Portal > Quotations > open quote > Review / Approve > Payment",
                Description = "Clients review the proposed quote, digitally accept it, then pay the issued invoice and download the receipt.",
                Steps = new()
                {
                    "1. Open Client Portal > Quotations and click the quote with status 'Approved' / awaiting your acceptance.",
                    "2. Review the line items, totals (incl. 15% VAT) and milestone schedule.",
                    "3. Accept using the digital signature step and confirm.",
                    "4. An invoice is generated 1:1 with the quote. Open it from your portal and choose the Payment step.",
                    "5. Pay and upload/record proof of payment if required; the office confirms it and a receipt becomes available."
                },
                Tips = new() { "The first milestone is normally a deposit that must be paid before site establishment.", "Contact your Quantity Surveyor before accepting if any scope line looks wrong." }
            },
            new SystemActionGuide
            {
                ActionKey = "create_project", Category = "Projects",
                Title = "Create a New Project / Job File",
                AllowedRoles = new() { "Office", "Manager", "Admin" },
                NavigationPath = "Office > Create Project (or convert an approved quote to a project)",
                Description = "Open a job so a Site Manager can be assigned and field teams can start logging work.",
                Steps = new()
                {
                    "1. Go to Office > Create Project (or open an approved quote and convert it to an active project).",
                    "2. Fill in title, client, project type, location, estimated cost and the assigned Site Manager.",
                    "3. Set the site location/geofence so GPS attendance and site reports can be verified.",
                    "4. Save - the project appears in Projects and on the Site Manager's dashboard/mobile app."
                },
                Tips = new() { "A correct geofence radius avoids false off-site flags on attendance and photos.", "Link the project to its quote reference so invoicing stays 1:1." }
            },
            new SystemActionGuide
            {
                ActionKey = "manage_projects", Category = "Projects",
                Title = "Manage, Reassign or Delete Projects",
                AllowedRoles = new() { "Manager", "Admin" },
                NavigationPath = "Sidebar > Project Management",
                Description = "Manager-level project administration, including single and bulk (master) deletion with audit logging.",
                Steps = new()
                {
                    "1. Open Project Management from the sidebar.",
                    "2. Search/filter to find the project(s).",
                    "3. Open a project to update status, site manager or details; or tick several rows to use bulk actions.",
                    "4. Deletions are audit-logged - confirm only when you are sure."
                },
                Tips = new() { "Prefer closing a project over deleting it so history and invoices stay intact." }
            },
            new SystemActionGuide
            {
                ActionKey = "record_site_progress_web", Category = "Site Operations",
                Title = "Record Site Progress & Review Site Reports (Web)",
                AllowedRoles = new() { "SiteManager", "Office", "Manager", "Admin" },
                NavigationPath = "Project > Site Reports",
                Description = "Log progress %, construction stage and notes with a photo; review reports and off-site GPS flags.",
                Steps = new()
                {
                    "1. Open the project and go to its Site Reports page.",
                    "2. Add a report: description, construction stage, progress percentage and a photo.",
                    "3. GPS is compared with the project geofence; reports taken outside it are flagged 'Off-Site'.",
                    "4. Managers and clients can follow the progress percentage on the project dashboards."
                },
                Tips = new() { "Take photos on site - off-site reports are flagged and may be rejected.", "On mobile the same flow works offline and syncs later." }
            },
            new SystemActionGuide
            {
                ActionKey = "review_attendance", Category = "Site Operations",
                Title = "Review & Approve Team Attendance",
                AllowedRoles = new() { "SiteManager", "Manager", "Office", "Admin" },
                NavigationPath = "Project > Attendance",
                Description = "See check-ins with GPS distance, spot off-site check-ins and approve valid records.",
                Steps = new()
                {
                    "1. Open the project and go to Attendance.",
                    "2. Review each record: time, GPS location, distance from site and off-site flag.",
                    "3. Approve valid records; investigate flagged ones with the worker before approving."
                },
                Tips = new() { "Unapproved attendance should be cleared before payroll/billing cut-off." }
            },
            new SystemActionGuide
            {
                ActionKey = "expenses_and_budget", Category = "Finance",
                Title = "Track Expenses, Budget and Variance",
                AllowedRoles = new() { "SiteManager", "Office", "Manager", "Admin" },
                NavigationPath = "Dashboard > Project Details (budget & expenses)",
                Description = "Budget remaining = estimated project cost minus APPROVED expenses. Variance % flags overruns.",
                Steps = new()
                {
                    "1. Open the project details page from the dashboard.",
                    "2. Log an expense with description, amount and supplier/receipt.",
                    "3. A manager/office user approves it; only approved expenses reduce the remaining budget.",
                    "4. Check the budget health indicator - negative remaining means OVER BUDGET."
                },
                Tips = new() { "Ask me 'what is the budget for my project?' and I will fetch live figures if your role allows.", "Field workers and clients cannot see internal expense ledgers." }
            },
            new SystemActionGuide
            {
                ActionKey = "support_ticket", Category = "Support",
                Title = "Raise or Follow a Support Ticket",
                AllowedRoles = new() { "All Roles" },
                NavigationPath = "Sidebar > Support",
                Description = "Report a problem or request help from the support team.",
                Steps = new()
                {
                    "1. Click Support in the sidebar.",
                    "2. Choose Create, describe the issue (what you did, what you expected, what happened) and submit.",
                    "3. Return to Support > Index to see updates and replies."
                },
                Tips = new() { "Include the quote reference or project name so support can find it fast." }
            },
            new SystemActionGuide
            {
                ActionKey = "notifications", Category = "Account",
                Title = "View Notifications",
                AllowedRoles = new() { "All Roles" },
                NavigationPath = "Top bar > Notification bell",
                Description = "Quote status changes, approvals, invoices and system messages.",
                Steps = new()
                {
                    "1. Click the bell icon in the top bar for the latest unread items.",
                    "2. Open Notifications to see the full list and mark items read."
                },
                Tips = new() { "Email notifications are sent too when email is enabled by your administrator." }
            },
            new SystemActionGuide
            {
                ActionKey = "account_security", Category = "Account",
                Title = "Change Password / Set Up Multi-Factor Authentication",
                AllowedRoles = new() { "All Roles" },
                NavigationPath = "Account > Change Password / Set Up MFA",
                Description = "Keep your account secure; mandatory password change is enforced on first login for seeded/new accounts.",
                Steps = new()
                {
                    "1. Open your account menu and choose Change Password, enter the current and a new strong password, then save.",
                    "2. For MFA choose Set Up MFA, scan the QR code with an authenticator app and enter the 6-digit code to confirm.",
                    "3. On next login you will be asked for the authenticator code after your password."
                },
                Tips = new() { "I can never see or reset your password - if you are locked out, ask an Administrator to reset it.", "Never share MFA codes, even with support." }
            },
            new SystemActionGuide
            {
                ActionKey = "reports_and_exports", Category = "Management & Reporting",
                Title = "Dashboards, Reports, Exports and AI Report",
                AllowedRoles = new() { "Manager", "Executive", "Office", "Admin" },
                NavigationPath = "Sidebar > Reports / Portfolio / Regional Map / Live Stats / Export / AI Report",
                Description = "Corporate performance, portfolio health, regional distribution and data export.",
                Steps = new()
                {
                    "1. Pick the view from the sidebar: Executive (read-only KPIs), Management (operations), Portfolio, Regional Map, Live Stats or Reports.",
                    "2. Apply the date/status filters at the top.",
                    "3. Use Export to download data, or AI Report for a generated narrative summary."
                },
                Tips = new() { "Executives have read-only access; operational changes need a Manager or Office user." }
            },
            new SystemActionGuide
            {
                ActionKey = "audit_logs", Category = "Administration",
                Title = "View the Audit Trail",
                AllowedRoles = new() { "Admin" },
                NavigationPath = "Admin > Audit Logs",
                Description = "Immutable record of sensitive actions such as deletions, role changes and logins.",
                Steps = new()
                {
                    "1. Sign in as an Administrator and open Admin > Audit Logs.",
                    "2. Filter by user, action or date range.",
                    "3. Open an entry to see who did what and when (deletions include the recorded reason)."
                },
                Tips = new() { "Review the log after bulk deletions or role changes." }
            },
            new SystemActionGuide
            {
                ActionKey = "system_settings", Category = "Administration",
                Title = "Change System Settings",
                AllowedRoles = new() { "Admin" },
                NavigationPath = "Admin > System Settings",
                Description = "Global configuration used across quoting, invoicing and notifications.",
                Steps = new()
                {
                    "1. Open Admin > System Settings.",
                    "2. Edit the setting and save; changes apply system-wide.",
                    "3. Verify the effect on a test quote or invoice."
                },
                Tips = new() { "Changes are audit-logged - document why you changed a value." }
            },
            new SystemActionGuide
            {
                ActionKey = "ai_cost_estimate", Category = "Quotations & Tenders",
                Title = "Get an AI / QS Cost Estimate",
                AllowedRoles = new() { "Client", "Office", "Manager", "Admin" },
                NavigationPath = "Office > AI Draft Estimate (staff) OR ask the assistant (everyone)",
                Description = "Indicative South African QS estimate (ZAR) by building type, area, location and finish grade.",
                Steps = new()
                {
                    "1. Tell me the building type, floor area in m2, location and finish (Standard/Premium) - I will itemise it.",
                    "2. Staff can also open Office > AI Draft Estimate on a quote to draft the figure.",
                    "3. Treat the result as indicative; the binding price comes from the QS-prepared, manager-approved quote."
                },
                Tips = new() { "Estimates exclude land, professional fees and abnormal site conditions unless stated." }
            },
            new SystemActionGuide
            {
                ActionKey = "mobile_app_overview", Category = "Mobile",
                Title = "Using the Android App (Field Worker / Site Manager)",
                AllowedRoles = new() { "FieldWorker", "SiteManager" },
                NavigationPath = "Android app > Login > role home screen",
                Description = "Shift check-in, work logs with photos, material scanning, assigned tasks and the AI assistant - works offline and syncs.",
                Steps = new()
                {
                    "1. Sign in with the credentials issued to you; you must set a new password on first login.",
                    "2. Field Workers: check in to your shift (GPS verified), log work with photos, scan materials and view assigned tasks.",
                    "3. Site Managers: also submit GPS site reports, register deliveries and review team attendance.",
                    "4. Offline entries are queued and synced automatically when you are back online."
                },
                Tips = new() { "Enable location permission or check-in will be rejected.", "If a barcode is 'not registered', ask your Site Manager to register it." }
            },
        };

        /// <summary>Scores every guide against the text and returns the strongest match (or null).</summary>
        public static SystemActionGuide? FindBestGuide(string lowerQuery)
        {
            SystemActionGuide? best = null;
            var bestScore = 0;
            foreach (var g in AllGuides)
            {
                if (!Keywords.TryGetValue(g.ActionKey, out var words)) continue;
                var score = 0;
                foreach (var w in words)
                    if (lowerQuery.Contains(w, StringComparison.Ordinal))
                        score += w.Split(' ', '-').Length * 2 + 1; // phrases outrank single words
                if (score > bestScore) { best = g; bestScore = score; }
            }
            return best;
        }

        /// <summary>Compact "table of contents" for the system prompt so the model knows every workflow that exists.</summary>
        public static string BuildIndex(string? role)
        {
            var sb = new StringBuilder();
            foreach (var g in AllGuides)
            {
                var can = CanPerform(role, g);
                sb.AppendLine($"- `{g.ActionKey}` — {g.Title} | roles: {string.Join(", ", g.AllowedRoles)} | this user: {(can ? "ALLOWED" : "NOT allowed")}");
            }
            return sb.ToString();
        }

        /// <summary>Plain-language answer to "what can I do / who can do X" for a given role.</summary>
        public static string BuildCapabilitySummary(string? role)
        {
            var canon = CanonicalRole(role);
            var can = AllGuides.Where(g => CanPerform(role, g)).ToList();
            var cannot = AllGuides.Where(g => !CanPerform(role, g)).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Role: {canon}");
            sb.AppendLine("You CAN:");
            foreach (var g in can) sb.AppendLine($"- {g.Title} ({g.NavigationPath})");
            sb.AppendLine();
            sb.AppendLine("You CANNOT (who can instead):");
            foreach (var g in cannot) sb.AppendLine($"- {g.Title} -> {string.Join(", ", g.AllowedRoles)}");
            return sb.ToString();
        }

        /// <summary>Role overview used both in the prompt and the local fallback engine.</summary>
        public const string RoleOverview =
            "Roles: Admin (users, roles, audit, settings, health; can do almost everything) | Manager/Management (approve quotes, project management, reports, bulk deletes) | " +
            "Office/QS (review & cost quotes, create projects, invoices, payments, estimates) | Executive (read-only corporate dashboards) | " +
            "SiteManager (site reports, attendance review, expenses, materials, team) | FieldWorker (mobile: shift check-in, work logs, photos, scan materials, tasks) | " +
            "Client (request/track/accept quotes, pay invoices, follow own project progress).";
    }
}
