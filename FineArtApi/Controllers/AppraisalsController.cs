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
    [Authorize] // Senior Management Compliance: Ensuring financial privacy
    public class AppraisalsController : ControllerBase
    {
        private readonly ArtContext _context;

        public AppraisalsController(ArtContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAppraisals()
        {
            // Extract the ProfileId from the JWT Token claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            
            int profileId = int.Parse(userIdClaim.Value);

            // Filter logic: Only return appraisals for artworks owned by this user profile
            return await _context.Appraisals
                .Include(a => a.Artwork)
                .Where(a => a.Artwork != null && a.Artwork.CreatedByProfileId == profileId) 
                .OrderByDescending(a => a.ValuationDate) 
                .Select(a => new {
                    a.AppraisalId,
                    a.ArtworkId,
                    // Fixed CS8602: Safe navigation for Title
                    ArtworkTitle = a.Artwork != null ? a.Artwork.Title : "Unassigned Asset",
                    a.ValuationAmount,
                    a.CurrencyCode,
                    a.ValuationDate,
                    a.AppraiserName,
                    a.InsuranceValue,
                    a.Notes
                })
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Appraisal>> PostAppraisal(Appraisal appraisal)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            
            int profileId = int.Parse(userIdClaim.Value);

            // Validation: Ensure the user owns the artwork they are attempting to appraise
            var artwork = await _context.Artworks.FindAsync(appraisal.ArtworkId);
            
            if (artwork == null || artwork.CreatedByProfileId != profileId)
            {
                return Unauthorized(new { message = "You can only record valuations for assets in your own collection." });
            }

            _context.Appraisals.Add(appraisal);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAppraisals), new { id = appraisal.AppraisalId }, appraisal);
        }
    }
}