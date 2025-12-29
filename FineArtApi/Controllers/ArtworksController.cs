using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FineArtApi.Data;
using FineArtApi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace FineArtApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ArtworksController : ControllerBase
    {
        private readonly ArtContext _context;

        public ArtworksController(ArtContext context)
        {
            _context = context;
        }

        // NEW: Endpoint for user-scoped artwork inventory (ISO27001 Compliance)
        [HttpGet("user")]
        public async Task<ActionResult<IEnumerable<object>>> GetUserArtworks([FromQuery] int? collectionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int profileId))
            {
                return Unauthorized(new { message = "Security Identity missing or invalid." });
            }

            // FIXED: Navigate through ca.Collection to reach the Owner
            var query = _context.Artworks
                .Include(a => a.Artist)
                .Include(a => a.ArtworkImages)
                .Where(a => a.CollectionArtworks.Any(ca => 
                    ca.Collection.OwnerProfileId == profileId))
                .AsQueryable();

            if (collectionId.HasValue)
            {
                // Further filter by collectionId if provided, still respecting the owner scope.
                query = query.Where(a => a.CollectionArtworks.Any(ca => ca.CollectionId == collectionId.Value));
            }

            var results = await query.Select(a => new {
                a.ArtworkId,
                a.Title,
                ArtistName = a.Artist != null ? $"{a.Artist.FirstName} {a.Artist.LastName}" : "Unknown",
                a.AcquisitionCost, // Values in GBP
                a.Status,
                ImageUrl = a.ArtworkImages.OrderByDescending(i => i.IsPrimary).Select(i => i.BlobUrl).FirstOrDefault()
            }).ToListAsync();

            return Ok(results);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetArtworks(
            [FromQuery] int? collectionId = null, 
            [FromQuery] int? artistId = null)
        {
            var query = _context.Artworks
                .Include(a => a.ArtworkImages)
                .Include(a => a.Artist)
                .Include(a => a.CollectionArtworks)
                .ThenInclude(ca => ca.Collection)
                .AsQueryable();

            if (collectionId.HasValue)
            {
                query = query.Where(a => a.CollectionArtworks.Any(ca => ca.CollectionId == collectionId.Value));
            }

            if (artistId.HasValue)
            {
                query = query.Where(a => a.ArtistId == artistId.Value);
            }

            var results = await query.Select(a => new {
                a.ArtworkId,
                a.Title,
                a.ArtistId,
                ArtistName = a.Artist != null ? $"{a.Artist.FirstName} {a.Artist.LastName}" : "Unknown",
                a.Medium,
                a.HeightCM,
                a.WidthCM,
                a.AcquisitionCost,
                a.Status,
                ImageUrl = a.ArtworkImages.OrderByDescending(i => i.IsPrimary).Select(i => i.BlobUrl).FirstOrDefault(),
                Collections = a.CollectionArtworks.Select(ca => ca.Collection.CollectionName).ToList()
            }).ToListAsync();

            return Ok(results);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetArtwork(int id)
        {
          try
          {
            var artwork = await _context.Artworks
                .Include(a => a.ArtworkImages)
                .Include(a => a.Artist) 
                .Include(a => a.CollectionArtworks)
                .ThenInclude(ca => ca.Collection)
                .FirstOrDefaultAsync(a => a.ArtworkId == id);

            if (artwork == null) return NotFound();

            return Ok(new
            {
              artwork.ArtworkId,
              artwork.Title,
              artwork.ArtistId,
              ArtistName = artwork.Artist != null ? $"{artwork.Artist.FirstName} {artwork.Artist.LastName}" : "Unknown Artist",
              artwork.Medium,
              artwork.HeightCM,
              artwork.WidthCM,
              artwork.AcquisitionCost,
              artwork.Status,
              ArtworkImages = (artwork.ArtworkImages ?? new List<ArtworkImage>())
                  .Select(i => new { Id = i.ImageId, i.BlobUrl, i.IsPrimary })
                  .ToList(),
              Collections = artwork.CollectionArtworks != null 
                  ? artwork.CollectionArtworks.Where(ca => ca.Collection != null)
                                              .Select(ca => ca.Collection.CollectionName)
                                              .ToList()
                  : new List<string>()
            });
          } catch (Exception ex)
          {
            // Return full stack trace for debugging
            return StatusCode(500, new { message = $"Error retrieving artwork {id}.", details = ex.Message, stackTrace = ex.ToString() });
          }
        }

        [HttpPost("update-valuation/{id}")]
        public async Task<IActionResult> UpdateValuation(int id, [FromBody] ValuationUpdateRequest request)
        {
            var artwork = await _context.Artworks.FindAsync(id);

            if (artwork == null)
            {
                return NotFound(new { message = "Asset not found in UK Registry." });
            }

            artwork.AcquisitionCost = request.NewValuation;
            artwork.AcquisitionDate = request.EffectiveDate;
            artwork.LastModifiedAt = System.DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Asset valuation updated successfully." });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new { message = "Database error during valuation update.", details = ex.Message });
            }
        }

        public class ValuationUpdateRequest
        {
            public decimal NewValuation { get; set; }
            public System.DateTime EffectiveDate { get; set; }
        }
    }
}