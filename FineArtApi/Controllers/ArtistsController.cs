using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FineArtApi.Data;
using FineArtApi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FineArtApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ArtistsController : ControllerBase
    {
        private readonly ArtContext _context;

        public ArtistsController(ArtContext context)
        {
            _context = context;
        }

        // GET: api/Artists
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Artist>>> GetArtists()
        {
            return await _context.Artists.OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ToListAsync();
        }

        // POST: api/Artists
        [HttpPost]
        public async Task<ActionResult<Artist>> PostArtist(ArtistCreateDto artistDto)
        {
            var artist = new Artist
            {
                FirstName = artistDto.FirstName ?? string.Empty,
                LastName = artistDto.LastName,
                Pseudonym = artistDto.Pseudonym,
                Nationality = artistDto.Nationality,
                BirthYear = artistDto.BirthYear,
                DeathYear = artistDto.DeathYear,
                Biography = artistDto.Biography,
                CreatedAt = System.DateTime.UtcNow
            };

            _context.Artists.Add(artist);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetArtist), new { id = artist.ArtistId }, artist);
        }

        // GET: api/Artists/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetArtist(int id)
        {
            var artist = await _context.Artists
                .Where(a => a.ArtistId == id)
                .Select(a => new {
                    a.ArtistId,
                    a.FirstName,
                    a.LastName,
                    a.Pseudonym,
                    a.Nationality,
                    a.BirthYear,
                    a.DeathYear,
                    a.Biography,
                    Artworks = a.Artworks.Select(w => new {
                        w.ArtworkId,
                        w.Title,
                        w.AcquisitionCost
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (artist == null)
            {
                return NotFound();
            }

            return artist;
        }
    }

    public class ArtistCreateDto
    {
        public string? FirstName { get; set; }
        public required string LastName { get; set; }
        public string? Pseudonym { get; set; }
        public string? Nationality { get; set; }
        public int? BirthYear { get; set; }
        public int? DeathYear { get; set; }
        public string? Biography { get; set; }
    }
}
