using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Api.Controllers
{
    public class InviteRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string Role { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/v1/invitations")]
    [Authorize(Roles = "Admin,SystemAdmin,Office")]
    public class InvitationsApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public InvitationsApiController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpPost("invite")]
        [HttpPost("/api/v1/admin/assign-user")]
        public async Task<IActionResult> InviteUser([FromBody] InviteRequest req)
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user != null)
                return BadRequest("User already exists.");

            var newUser = new ApplicationUser
            {
                UserName = req.Email,
                Email = req.Email,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(newUser);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(newUser, req.Role);

            // Generate an expiring token for email verification/password setup
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(newUser);
            
            // In a real application, send this token via Email or SMS here.
            // For now, return it so the client can construct the link for demonstration.
            var inviteLink = $"https://ubuhlebethu.com/setup-account?email={req.Email}&token={Uri.EscapeDataString(token)}";

            return Ok(new { Message = "Invitation created successfully.", InviteLink = inviteLink });
        }
    }
}
