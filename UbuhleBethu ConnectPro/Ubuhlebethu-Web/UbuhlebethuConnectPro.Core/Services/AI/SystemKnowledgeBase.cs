using System;
using System.Collections.Generic;
using System.Linq;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    public class SystemActionGuide
    {
        public string ActionKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> AllowedRoles { get; set; } = new();
        public List<string> Steps { get; set; } = new();
        public List<string> Tips { get; set; } = new();
        public string Description { get; set; } = string.Empty;
        public string NavigationPath { get; set; } = string.Empty;
    }

    public class CapabilityAssessment
    {
        public bool CanPerform { get; set; }
        public string UserRole { get; set; } = string.Empty;
        public string ActionTitle { get; set; } = string.Empty;
        public List<string> AllowedRoles { get; set; } = new();
        public string NavigationPath { get; set; } = string.Empty;
        public List<string> ExactSteps { get; set; } = new();
        public List<string> ProTips { get; set; } = new();
        public string PermissionExplanation { get; set; } = string.Empty;
        public string ContactRecommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive system knowledge base containing exact operating steps,
    /// RBAC permissions matrix, and best practice tips for every workflow in UbuhleBethu ConnectPro.
    /// </summary>
    public static partial class SystemKnowledgeBase
    {
        private static readonly List<SystemActionGuide> Guides = new()
        {
            new SystemActionGuide
            {
                ActionKey = "request_quote",
                Title = "Submit a New Quotation Request",
                Category = "Quotations & Tenders",
                AllowedRoles = new List<string> { "Client", "Office", "Manager", "Admin", "Anonymous" },
                NavigationPath = "Client Portal > Request Quote OR Public Landing Page > Get a Quote",
                Description = "Submit building requirements, dimensions, plans, and budget for Quantity Surveyor pricing.",
                Steps = new List<string>
                {
                    "1. Navigate to the Quotations tab or click 'Request a Quote' on the public portal.",
                    "2. Enter your project details: Project Title, Building Category (Residential, Commercial, Civil, Clinic, School), and Site Address.",
                    "3. Enter the total floor area in square meters (m²) and your estimated budget target.",
                    "4. Specify your preferred start date and upload any architectural drawings or scope specifications (PDF/PNG/JPG).",
                    "5. Click 'Submit Quote Request'. You will receive a unique tracking reference code (e.g., UBU-Q7938) to check proposal progress."
                },
                Tips = new List<string>
                {
                    "Provide accurate site GPS coordinates or municipal stand numbers to ensure accurate delivery and geofencing setup.",
                    "Mention site accessibility (soil type, steep gradients) in the description so the QS includes earthworks allowances."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "cost_quote",
                Title = "Review, Cost & Prepare Quotation (QS Costing)",
                Category = "Quotations & Tenders",
                AllowedRoles = new List<string> { "Office", "QS", "Manager", "Admin" },
                NavigationPath = "Dashboard > Operations > Pending Quotes > Click 'Review' on quote",
                Description = "Analyze architectural scope, apply bill of quantities rates, adjust markup, and generate client proposal PDF.",
                Steps = new List<string>
                {
                    "1. Go to 'Operations > Pending Quotes' in the left sidebar.",
                    "2. Click the quote reference to open the detailed proposal worksheet.",
                    "3. Review the client's submitted floor area and project type.",
                    "4. Use the AI Cost Estimator suggestion or manually adjust line items: Material Subtotal, Labor Rates, Plant Hire, and Contingency.",
                    "5. Specify the company markup percentage (standard 15% - 25%).",
                    "6. Enter the 'Estimated Budget' and click 'Send to Manager for Approval'.",
                    "7. Once approved by the Manager, click 'Export Quote PDF' to generate the official branded UbuhleBethu proposal."
                },
                Tips = new List<string>
                {
                    "Always verify whether the quote requires NHBRC home builder warranty registration (mandatory for residential dwellings in SA).",
                    "Use the built-in AI Material Estimator to cross-reference current brick and cement market prices."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "approve_quote",
                Title = "Manager Approval of Quotation & Contract Conversion",
                Category = "Management & Approvals",
                AllowedRoles = new List<string> { "Manager", "Management", "Admin" },
                NavigationPath = "Dashboard > Review Quotes > Select Quote > 'Accept' or 'Reject'",
                Description = "Verify profit margins, cash flow milestones, and authorize the quotation for client release.",
                Steps = new List<string>
                {
                    "1. Navigate to 'Management: Review Quotes' from the sidebar.",
                    "2. Select the quote under 'Pending Manager Approval'.",
                    "3. Audit the cost worksheet: check labor breakdown, material subtotal, and net profit margin.",
                    "4. If revisions are required: click 'Reject / Request Revision', enter constructive feedback, and the QS will be notified.",
                    "5. If acceptable: click 'Approve Quote'. The status transitions to 'Approved by Manager' and is dispatched to the client.",
                    "6. Upon client contract acceptance, click 'Convert to Active Project' to create the job file and assign a Site Manager."
                },
                Tips = new List<string>
                {
                    "Ensure a minimum of 10% contingency is included on civil and clinic projects due to geotechnical risks.",
                    "Check client payment milestones: standard 30% upfront deposit must be invoiced before site establishment."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "invoicing_and_milestones",
                Title = "Issue Milestone Invoices & Record Payment Proof",
                Category = "Finance & Invoicing",
                AllowedRoles = new List<string> { "Office", "QS", "Manager", "Admin" },
                NavigationPath = "Projects > Select Project > Invoices & Milestones OR Sidebar > Management & Finance",
                Description = "Manage the 4-stage construction progress billing schedule and reconcile proof of payment receipts.",
                Steps = new List<string>
                {
                    "1. Open the target project from 'Project Management' and select the 'Job Details' tab.",
                    "2. Review the four project milestone gates: (1) Deposit 30% [Active on start], (2) Roof Height 40% [Unlocked at 40% progress], (3) Practical Completion 25% [Unlocked at 90% progress], (4) Retention 5% [Unlocked at 100%].",
                    "3. Click 'Generate Milestone Invoice' for the unlocked milestone.",
                    "4. Review due date, VAT calculation (15%), and banking details.",
                    "5. Click 'Print / Download Official Tax Invoice' for client dispatch.",
                    "6. When EFT proof of payment is received: click 'Record Payment', select method (EFT/Bank Transfer), enter transaction reference, and attach POP."
                },
                Tips = new List<string>
                {
                    "Do not issue Milestone 2 (Roof Height) without verified site reports and engineer inspection photos logged in the system.",
                    "Retention payments (5%) should only be marked paid after the 90-day defect liability period expires."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "mobile_worker_attendance",
                Title = "Field Worker Shift Check-In & Anti-Cheating GPS Verification",
                Category = "Mobile Field Operations",
                AllowedRoles = new List<string> { "FieldWorker", "SiteManager" },
                NavigationPath = "Mobile Android App > Field Worker Portal > Shift & Attendance Check-In",
                Description = "Clock in and out of active construction shifts with automated GPS geofence boundary validation.",
                Steps = new List<string>
                {
                    "1. Launch the Ubuhlebethu mobile app and sign in with your worker credentials.",
                    "2. In the 'Field Worker Portal', ensure your name is entered and select the active project from the dropdown.",
                    "3. Click 'START JOB'. The mobile app captures your device GPS location and verifies you are within the 300m project geofence.",
                    "4. If inside the geofence, your shift starts immediately with green confirmation. If off-site, the shift is flagged for site manager approval.",
                    "5. At the end of your working day, return to the portal and click 'FINISH JOB' to record your checkout time and daily rate."
                },
                Tips = new List<string>
                {
                    "Ensure Location Services / High Accuracy GPS is enabled in Android device settings before pressing Start Job.",
                    "If working in an area with poor cellular signal, the app securely queues your timestamp and syncs automatically once back in coverage."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "mobile_work_log_and_photo",
                Title = "Submit Daily Work Log & Progress Photos",
                Category = "Mobile Field Operations",
                AllowedRoles = new List<string> { "FieldWorker", "SiteManager", "Manager" },
                NavigationPath = "Mobile Android App > Log Work, Upload Photo & Tag GPS",
                Description = "Capture progress photos, record daily tasks completed, and broadcast live updates to the office.",
                Steps = new List<string>
                {
                    "1. Scroll to the 'Log Work, Upload Photo & Tag GPS' card in the mobile app.",
                    "2. Type a clear description of tasks completed today (e.g., 'Completed foundation brickwork and damp proofing on block B').",
                    "3. Tap 'PHOTO' to open the camera and take a clear, well-lit photo of the completed work.",
                    "4. Tap 'TAG GPS' to record verifiable on-site coordinates.",
                    "5. Tap 'Submit Work Log'. Photos are automatically compressed with WebP compression for rapid data upload.",
                    "6. The report and photo instantly broadcast via SignalR to the Manager Web Dashboard and Client Portal."
                },
                Tips = new List<string>
                {
                    "Always take landscape photos showing both the work detail and wider building context for QS audit verification.",
                    "If offline, your log is saved in local SQLite and marked as 'Syncing in background' until network connectivity returns."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "scan_materials",
                Title = "Scan Material Barcodes & Track Site Inventory",
                Category = "Mobile Field Operations",
                AllowedRoles = new List<string> { "FieldWorker", "SiteManager" },
                NavigationPath = "Mobile Android App > Scan Materials & Active Tasks > 'Scan Site Materials / Barcode'",
                Description = "Scan supplier delivery slips and material barcodes with ML Kit to record site inventory delivery.",
                Steps = new List<string>
                {
                    "1. Tap 'Scan Site Materials / Barcode' on the mobile dashboard.",
                    "2. Point the camera at the delivery note or packaging barcode (Code 128, EAN-13, QR Code).",
                    "3. When scanned with a beep/haptic feedback, the system looks up the catalog: item name, unit cost, and standard unit.",
                    "4. Confirm quantity received against the purchase order delivery manifest.",
                    "5. Click 'Confirm Delivery'. The inventory is allocated to the project and available on the office procurement register."
                },
                Tips = new List<string>
                {
                    "Inspect materials for physical damage before scanning. Reject damaged cement bags or bent reinforcement mesh before scanning.",
                    "If a barcode does not scan, check lens cleanliness or tap the screen to trigger autofocus."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "user_management",
                Title = "Create Users, Manage Roles & System Permissions",
                Category = "Administration & Security",
                AllowedRoles = new List<string> { "Admin", "SystemAdmin" },
                NavigationPath = "Web App > Administration > User Management > 'Create User' or 'Edit Roles'",
                Description = "Provision employee accounts, assign granular role-based access, and audit account security.",
                Steps = new List<string>
                {
                    "1. Sign in as an Administrator and click 'User Management' in the left navigation sidebar.",
                    "2. To create a user: click 'Create User', enter Username, Email, and temporary Password.",
                    "3. Select one or more role checkboxes from the Role Matrix: Admin, Manager, Office, SiteManager, FieldWorker, Client.",
                    "4. Click 'Create User'. The account is immediately created and roles are synchronized in Identity.",
                    "5. To modify an existing user's roles: click 'Edit Roles' next to their name in the table, toggle the roles, and click 'Save Roles'.",
                    "6. To lock out an account: click 'Deactivate' and provide an audit reason."
                },
                Tips = new List<string>
                {
                    "Practice Principle of Least Privilege (PoLP): Only grant 'Admin' to designated system owners.",
                    "Office and Quantity Surveyors should be assigned the 'Office' role; field supervisory staff should be assigned 'SiteManager'."
                }
            },
            new SystemActionGuide
            {
                ActionKey = "system_health",
                Title = "Monitor System Health, Database & Observability",
                Category = "Administration & Security",
                AllowedRoles = new List<string> { "Admin", "SystemAdmin" },
                NavigationPath = "Web App > Administration > System Health (/Admin/Health)",
                Description = "View real-time telemetry for PostgreSQL, Backblaze B2 S3 storage, process memory, and active rate limiters.",
                Steps = new List<string>
                {
                    "1. In the sidebar under Administration, click 'System Health'.",
                    "2. Check the Overall Status banner: Green confirms all systems operational.",
                    "3. Review the Database card: verifies PostgreSQL connectivity and query execution latency.",
                    "4. Review the Cloud & S3 Storage card: verifies Backblaze B2 presigned URL generation and local fallback status.",
                    "5. Review Process & Memory: monitors managed heap allocation, working set MB, and GC generation cycles.",
                    "6. Click 'Refresh Status' to trigger an on-demand probe across all infrastructure components."
                },
                Tips = new List<string>
                {
                    "API rate limiters (auth: 5 req/min, ai: 15 req/min) are active by default to protect from brute force and token exhaustion.",
                    "External monitoring tools and Kubernetes ingress can query `GET /healthz` for automated uptime alerts."
                }
            }
        };

        public static CapabilityAssessment AssessCapability(string? userRole, string actionOrQuery)
        {
            var role = string.IsNullOrWhiteSpace(userRole) ? "Anonymous" : userRole.Trim();
            var q = actionOrQuery.ToLowerInvariant();

            SystemActionGuide? guide = null;

            // 1. Direct key match
            guide = Guides.FirstOrDefault(g => q.Contains(g.ActionKey) || q.Contains(g.ActionKey.Replace("_", " ")));

            // 2. Weighted keyword routing across every guide (core + extended)
            guide ??= FindBestGuide(q);

            // 3. Legacy intent matching (safety net)
            if (guide == null)
            {
                if (q.Contains("cost") && q.Contains("quote"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "cost_quote");
                else if ((q.Contains("approve") || q.Contains("reject") || q.Contains("accept")) && q.Contains("quote"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "approve_quote");
                else if (q.Contains("request") && q.Contains("quote"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "request_quote");
                else if (q.Contains("invoice") || q.Contains("milestone") || q.Contains("payment"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "invoicing_and_milestones");
                else if (q.Contains("attendance") || q.Contains("clock in") || q.Contains("start job") || q.Contains("check in") || q.Contains("shift"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "mobile_worker_attendance");
                else if (q.Contains("work log") || q.Contains("photo") || (q.Contains("site") && q.Contains("report")))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "mobile_work_log_and_photo");
                else if (q.Contains("barcode") || q.Contains("scan") || q.Contains("material"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "scan_materials");
                else if (q.Contains("user") || q.Contains("role") || q.Contains("permission") || q.Contains("deactivate"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "user_management");
                else if (q.Contains("health") || q.Contains("monitor") || q.Contains("telemetry") || q.Contains("observability"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "system_health");
                else if (q.Contains("quote"))
                    guide = Guides.FirstOrDefault(g => g.ActionKey == "cost_quote" && (q.Contains("review") || q.Contains("prepare"))) ?? Guides.FirstOrDefault(g => g.ActionKey == "request_quote");
            }

            if (guide == null)
            {
                // Default fallback guide
                return new CapabilityAssessment
                {
                    CanPerform = true,
                    UserRole = role,
                    ActionTitle = "General System Operation",
                    AllowedRoles = new List<string> { "All Roles" },
                    NavigationPath = "Main Navigation Sidebar",
                    ExactSteps = new List<string>
                    {
                        "1. Identify the module you need from the left navigation sidebar (Projects, Quotations, Invoices, Attendance, Support).",
                        "2. Click the item to load its primary management console.",
                        "3. Use the search and filter controls at the top of the table to locate specific jobs or records.",
                        "4. Click on any record row to view full details or perform authorized updates."
                    },
                    ProTips = new List<string>
                    {
                        "Ask me specifically about quotes, milestone invoicing, mobile attendance, photos, barcode scanning, or user management for exact step-by-step procedures."
                    },
                    PermissionExplanation = $"As a {role}, you have access to your role-specific dashboard and actions."
                };
            }

            var canPerform = CanPerform(role, guide);




            string permExplanation;
            string contactRec = string.Empty;

            if (canPerform)
            {
                permExplanation = $"**Access Granted**: As a verified **{role}**, you have full authority to execute '{guide.Title}'.";
            }
            else
            {
                var allowedList = string.Join(", ", guide.AllowedRoles);
                permExplanation = $"**Access Restricted**: You are signed in as **{role}**, which does not have permission to execute '{guide.Title}'. This capability is strictly restricted to authorized roles: **{allowedList}**.";

                contactRec = CanonicalRole(role) switch
                {
                    "Client" => "Please contact your assigned Project Manager or Quantity Surveyor to request this change on your behalf.",
                    "FieldWorker" => "Please inform your on-site Site Manager or Quantity Surveyor to authorize and process this request.",
                    "SiteManager" => "Please submit an expense requisition or contact the Project Manager for financial approvals.",
                    _ => $"Please request an Administrator ({string.Join(", ", guide.AllowedRoles)}) to grant you the necessary role permissions."
                };
            }

            return new CapabilityAssessment
            {
                CanPerform = canPerform,
                UserRole = role,
                ActionTitle = guide.Title,
                AllowedRoles = guide.AllowedRoles,
                NavigationPath = guide.NavigationPath,
                ExactSteps = guide.Steps,
                ProTips = guide.Tips,
                PermissionExplanation = permExplanation,
                ContactRecommendation = contactRec
            };
        }
    }
}
