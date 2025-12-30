using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FineArtApi.Data;
using FineArtApi.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

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

        // GET: api/collections/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetCollection(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int profileId))
            {
                return Unauthorized(new { message = "Security Identity missing or invalid." });
            }

            var collection = await _context.Collections
                .Where(c => c.CollectionId == id && c.OwnerProfileId == profileId)
                .Select(c => new {
                    c.CollectionId,
                    c.CollectionName,
                    c.Description,
                    Artworks = c.CollectionArtworks.Select(ca => new {
                        ca.Artwork.ArtworkId,
                        ca.Artwork.Title,
                        ArtistName = ca.Artwork.Artist != null ? $"{ca.Artwork.Artist.FirstName} {ca.Artwork.Artist.LastName}" : "Unknown",
                        ImageUrl = ca.Artwork.ArtworkImages.OrderByDescending(i => i.IsPrimary).Select(i => i.BlobUrl).FirstOrDefault()
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (collection == null)
            {
                return NotFound();
            }

            return Ok(collection);
        }

        // POST: api/collections
        [HttpPost]
        public async Task<ActionResult<Collection>> PostCollection([FromBody] CollectionCreateDto collectionDto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int profileId))
            {
                return Unauthorized(new { message = "Security Identity missing or invalid." });
            }

            var collection = new Collection
            {
                CollectionName = collectionDto.CollectionName,
                Description = collectionDto.Description,
                OwnerProfileId = profileId
            };

            _context.Collections.Add(collection);
            await _context.SaveChangesAsync();

            if (collectionDto.ArtworkIds != null && collectionDto.ArtworkIds.Any())
            {
                var artworksToMove = collectionDto.ArtworkIds;

                // Find and remove existing links for these artworks in other collections owned by the same user
                var existingLinks = await _context.CollectionArtworks
                    .Where(ca => artworksToMove.Contains(ca.ArtworkId) && ca.Collection.OwnerProfileId == profileId)
                    .ToListAsync();

                if (existingLinks.Any())
                {
                    _context.CollectionArtworks.RemoveRange(existingLinks);
                }

                // Add the new links for the new collection
                foreach (var artworkId in artworksToMove)
                {
                    var newLink = new CollectionArtwork
                    {
                        CollectionId = collection.CollectionId,
                        ArtworkId = artworkId,
                        DateAdded = DateTime.UtcNow,
                        AddedByProfileId = profileId
                    };
                    _context.CollectionArtworks.Add(newLink);
                }

                await _context.SaveChangesAsync();
            }

            var result = new
            {
                collection.CollectionId,
                collection.CollectionName,
                collection.Description,
                ArtworkCount = collection.CollectionArtworks.Count
            };

            return CreatedAtAction(nameof(GetCollection), new { id = collection.CollectionId }, result);
        }
    }

    public class CollectionCreateDto
    {
        [Required]
        [StringLength(100)]
        public string CollectionName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public List<int> ArtworkIds { get; set; } = new List<int>();
    }
}