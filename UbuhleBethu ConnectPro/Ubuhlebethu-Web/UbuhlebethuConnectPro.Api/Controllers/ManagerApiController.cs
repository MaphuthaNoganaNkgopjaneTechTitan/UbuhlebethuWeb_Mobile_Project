using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models.DTOs;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Api.Controllers
{
    [ApiController]
    [Route("api/manager")]
    [Route("api/v1/manager")]
    [Authorize]
    public class ManagerApiController : ControllerBase
    {
        private readonly IQuotationService _quotationService;

        public ManagerApiController(IQuotationService quotationService)
        {
            _quotationService = quotationService;
        }

        // Handles: POST api/manager/accept-quote/{id}
        [HttpPost("accept-quote/{id}")]
        public async Task<IActionResult> AcceptQuote(Guid id)
        {
            var username = User.Identity?.Name ?? "manager";
            await _quotationService.ManagerApproveQuoteAsync(id, username);
            return Ok(new { message = "Quote approved by manager." });
        }

        // Handles: POST api/manager/reject-quote/{id}
        [HttpPost("reject-quote/{id}")]
        public async Task<IActionResult> RejectQuote(Guid id, [FromBody] ManagerRejectDto dto)
        {
            var username = User.Identity?.Name ?? "manager";
            await _quotationService.ManagerRejectQuoteAsync(id, dto?.Reason ?? "", username);
            return Ok(new { message = "Quote rejected by manager." });
        }
    }
}