using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;

namespace UbuhlebethuConnectPro.Api.Controllers
{
    [ApiController]
    [Route("api/v1/settings")]
    [Authorize(Roles = "Admin,SystemAdmin")]
    public class SystemSettingsApiController : ControllerBase
    {
        private readonly ISystemSettingsRepository _settingsRepo;

        public SystemSettingsApiController(ISystemSettingsRepository settingsRepo)
        {
            _settingsRepo = settingsRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _settingsRepo.GetSettingsAsync();
            return Ok(settings);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] SystemSettings settings)
        {
            await _settingsRepo.UpdateSettingsAsync(settings);
            return Ok(settings);
        }
    }
}
