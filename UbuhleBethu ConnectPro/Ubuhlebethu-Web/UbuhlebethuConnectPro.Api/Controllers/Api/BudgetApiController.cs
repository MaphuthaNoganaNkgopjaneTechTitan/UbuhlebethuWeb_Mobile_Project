using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class BudgetSummaryDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;
        public decimal EstimatedBudget { get; set; }
        public decimal TotalApprovedExpenses { get; set; }
        public decimal BudgetRemaining { get; set; }
        public decimal VariancePercent { get; set; }
        public bool IsOverBudget => BudgetRemaining < 0;
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/projects")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BudgetApiController : ControllerBase
    {
        private readonly IProjectRepository _projectRepo;
        private readonly IExpenseRepository _expenseRepo;

        public BudgetApiController(IProjectRepository projectRepo, IExpenseRepository expenseRepo)
        {
            _projectRepo = projectRepo;
            _expenseRepo = expenseRepo;
        }

        [HttpGet("{projectId:guid}/budget")]
        public async Task<IActionResult> GetProjectBudget(Guid projectId, CancellationToken ct)
        {
            var project = await _projectRepo.GetByIdAsync(projectId, ct);
            if (project == null)
            {
                return NotFound(new { error = "Project not found" });
            }

            // Check authorization - Staff, managers, site managers, and workers can view project budgets; Client can view their own project's budget
            var userName = User.Identity?.Name ?? string.Empty;
            var isStaffOrManager = User.IsInRole("Admin") ||
                                   User.IsInRole("SystemAdmin") ||
                                   User.IsInRole("Office") ||
                                   User.IsInRole("OfficeStaff") ||
                                   User.IsInRole("Manager") ||
                                   User.IsInRole("Management") ||
                                   User.IsInRole("Executive") ||
                                   User.IsInRole("SiteManager") ||
                                   User.IsInRole("Site Manager") ||
                                   User.IsInRole("FieldWorker") ||
                                   User.IsInRole("Worker");
            
            if (!isStaffOrManager && !string.Equals(project.ClientUserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var totalApprovedExpenses = await _expenseRepo.GetTotalApprovedExpensesAsync(projectId, ct);
            var budgetRemaining = project.EstimatedCost - totalApprovedExpenses;
            var variancePercent = project.EstimatedCost > 0 
                ? Math.Round((totalApprovedExpenses / project.EstimatedCost) * 100, 2) 
                : 0;

            var summary = new BudgetSummaryDto
            {
                ProjectId = project.Id,
                ProjectTitle = project.Title,
                EstimatedBudget = project.EstimatedCost,
                TotalApprovedExpenses = totalApprovedExpenses,
                BudgetRemaining = budgetRemaining,
                VariancePercent = variancePercent
            };

            return Ok(summary);
        }
    }
}
