using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using VinayagaPlates.Domain.Entities;
using VinayagaPlates.Application;
using VinayagaPlates.Contracts.DTOs;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LocationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [HttpGet("get-all-locations")]
        public async Task<IActionResult> GetAllLocations()
        {
            var locations = await _context.Locations.ToListAsync();
            var response = ApiResponse<object>.Success(locations, "Locations retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> CreateLocation([FromBody] Location location)
        {
            _context.Locations.Add(location);
            await _context.SaveChangesAsync();
            var response = ApiResponse<object>.Success(location, "Location created successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
