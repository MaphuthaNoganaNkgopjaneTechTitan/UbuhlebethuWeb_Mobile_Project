using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.ApiClient;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize(Roles = "Manager,Admin,SystemAdmin,Management,Office,OfficeStaff,QS,Executive,SiteManager")]
    public class AttendanceController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        public AttendanceController(IConnectProApiClient apiClient) => _apiClient = apiClient;

        public async Task<IActionResult> Project(Guid id, DateTime? date = null)
        {
            ViewBag.ProjectId = id;
            ViewBag.Date = date;
            ViewBag.FilterDate = date?.ToString("yyyy-MM-dd");
            try
            {
                var records = await _apiClient.GetAttendanceAsync(id, date);
                return View(records ?? new List<AttendanceDto>());
            }
            catch (Exception)
            {
                return View(new List<AttendanceDto>());
            }
        }
    }
}
