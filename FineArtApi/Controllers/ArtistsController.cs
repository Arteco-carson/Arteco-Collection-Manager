using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FineArtApi.Data;
using FineArtApi.Models;

namespace FineArtApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Corrected: Now fully protected
    public class ArtistsController : ControllerBase
    {
        private readonly ArtContext _context;

        public ArtistsController(ArtContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Artist>>> GetArtists()
        {
            return await _context.Artists
                .Where(a => _context.Artworks.Any(art => art.ArtistId == a.ArtistId))
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetArtist(int id)
        {
            try
            {
                var artist = await _context.Artists
                    .Include(a => a.Artworks)
                    .FirstOrDefaultAsync(a => a.ArtistId == id);

                if (artist == null) return NotFound();

                return Ok(new {
                    artist.ArtistId,
                    artist.FirstName,
                    artist.LastName,
                    Artworks = (artist.Artworks ?? new List<Artwork>())
                        .Select(a => new { a.ArtworkId, a.Title, a.Status, a.AcquisitionCost })
                        .ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving artist {id}.", details = ex.Message });
            }
        }
    }
}