using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FineArtApi.Data;
using FineArtApi.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FineArtApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // This triggers the 401 if the token isn't perfect
    public class UserController : ControllerBase
    {
        private readonly ArtContext _context;

        public UserController(ArtContext context)
        {
            _context = context;
        }

        [HttpGet("profile")]
        public async Task<ActionResult<object>> GetProfile()
        {
            // The ClaimTypes.NameIdentifier must match what was set in your Login method
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id");
            
            if (userIdClaim == null) 
                return Unauthorized(new { message = "Security Identity claim missing from token." });

            if (!int.TryParse(userIdClaim.Value, out int profileId))
            {
                return BadRequest(new { message = "Invalid Profile Identity format." });
            }

            var user = await _context.UserProfiles
                .FirstOrDefaultAsync(u => u.ProfileId == profileId);

            if (user == null) return NotFound();

            // Explicitly mapping to camelCase for the React Frontend
            return Ok(new
            {
                firstName = user.FirstName ?? "",
                lastName = user.LastName ?? "",
                username = user.Username,
                userRole = user.UserRole,
                emailAddress = user.EmailAddress,
                telephoneNumber = user.TelephoneNumber
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserProfileUpdateDto updateDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int profileId))
            {
                return Unauthorized(new { message = "Security Identity missing or invalid." });
            }

            var user = await _context.UserProfiles.FindAsync(profileId);
            if (user == null) return NotFound();

            user.FirstName = updateDto.FirstName;
            user.LastName = updateDto.LastName;
            user.EmailAddress = updateDto.EmailAddress;
            user.TelephoneNumber = updateDto.TelephoneNumber;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully." });
        }
    }

    public class UserProfileUpdateDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string EmailAddress { get; set; } = null!;
        public string? TelephoneNumber { get; set; }
    }
}