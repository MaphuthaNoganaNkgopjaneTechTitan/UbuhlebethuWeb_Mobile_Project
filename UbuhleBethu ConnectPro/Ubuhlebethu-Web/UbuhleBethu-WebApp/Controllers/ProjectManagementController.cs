using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.ApiClient;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    /// <summary>
    /// Dedicated project-management tab used strictly by Management/Manager to
    /// review every project (current and past) and delete one, several, or all of them.
    /// </summary>
    [Authorize(Roles = "Manager,Management")]
    public class ProjectManagementController : Controller
    {
        private static readonly string[] PastStatuses = { "completed", "closed", "archived", "cancelled", "canceled", "handed over", "finished" };

        private readonly IConnectProApiClient _apiClient;
        private readonly ILogger<ProjectManagementController> _logger;

        public ProjectManagementController(IConnectProApiClient apiClient, ILogger<ProjectManagementController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public static bool IsPast(Job j) =>
            PastStatuses.Contains((j.Status ?? string.Empty).Trim().ToLowerInvariant()) || j.Progress >= 100;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var jobs = await _apiClient.GetJobsAsync();
                return View(jobs ?? new List<Job>());
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Could not load projects for project management tab.");
                TempData["Error"] = "Could not load projects right now. Please try again shortly.";
                return View(new List<Job>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete([FromForm] List<Guid> projectIds, [FromForm] string? reason)
        {
            if (projectIds == null || projectIds.Count == 0)
            {
                TempData["Error"] = "Please select at least one project to delete.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _apiClient.DeleteProjectsAsync(projectIds, reason);
                TempData["Success"] = $"Deleted {projectIds.Count} project(s). Progress, order book, invoiced and outstanding totals have been updated everywhere.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete projects.");
                TempData["Error"] = "Failed to delete projects: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
