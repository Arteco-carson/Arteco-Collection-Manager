using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FineArtApi.Data;
using FineArtApi.Models;
using System.Security.Claims;

namespace FineArtApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CollectionsController : ControllerBase
    {
        private readonly ArtContext _context;

        public CollectionsController(ArtContext context)
        {
            _context = context;
        }

        // GET: api/collections/user
        [HttpGet("user")]
        public async Task<ActionResult<IEnumerable<object>>> GetUserCollections()
        {
            return await GetCollections();
        }

        // GET: api/collections
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetCollections()
        {
            // Extract Profile ID from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id");
            

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int profileId))
            {
                return Unauthorized(new { message = "Security Identity missing or invalid." });
            }

            // Retrieve only collections belonging to this senior manager
            // Retrieve only collections belonging to the authenticated user
            var collections = await _context.Collections
                .Where(c => c.OwnerProfileId == profileId)
                .Select(c => new {
                    c.CollectionId,
                    c.CollectionName,
                    c.Description,
                    ArtworkCount = c.CollectionArtworks.Count()
                })
                .ToListAsync();

            return Ok(collections);
        }
    }
}