using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.WebApp.ApiClient;

namespace UbuhlebethuConnectPro.WebApp.Controllers
{
    [Authorize]
    public class SiteReportsController : Controller
    {
        private readonly IConnectProApiClient _apiClient;

        private static readonly byte[] PlaceholderSvgBytes = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"400\" viewBox=\"0 0 600 400\">" +
            "<rect fill=\"#1e293b\" width=\"600\" height=\"400\"/>" +
            "<circle cx=\"300\" cy=\"170\" r=\"48\" fill=\"#334155\"/>" +
            "<text fill=\"#94a3b8\" font-family=\"system-ui,-apple-system,sans-serif\" font-size=\"36\" x=\"300\" y=\"182\" text-anchor=\"middle\">📷</text>" +
            "<text fill=\"#f8fafc\" font-family=\"system-ui,-apple-system,sans-serif\" font-size=\"20\" font-weight=\"600\" x=\"300\" y=\"250\" text-anchor=\"middle\">Photo Unavailable or Syncing</text>" +
            "<text fill=\"#94a3b8\" font-family=\"system-ui,-apple-system,sans-serif\" font-size=\"14\" x=\"300\" y=\"280\" text-anchor=\"middle\">The photo has not yet synced from the mobile worker&apos;s device.</text>" +
            "</svg>");

        public SiteReportsController(IConnectProApiClient apiClient) => _apiClient = apiClient;

        public async Task<IActionResult> Project(Guid id)
        {
            ViewBag.ProjectId = id;
            try
            {
                var reports = await _apiClient.GetSiteReportsAsync(id);
                return View(reports ?? new List<SiteReportDto>());
            }
            catch (Exception)
            {
                // Graceful fallback to prevent raw 502 error for office/management staff
                return View(new List<SiteReportDto>());
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Photo(Guid id)
        {
            try
            {
                var photoData = await _apiClient.GetSiteReportPhotoAsync(id);
                if (photoData == null)
                    return File(PlaceholderSvgBytes, "image/svg+xml");

                return File(photoData.Value.Stream, photoData.Value.ContentType);
            }
            catch
            {
                return File(PlaceholderSvgBytes, "image/svg+xml");
            }
        }
    }
}
