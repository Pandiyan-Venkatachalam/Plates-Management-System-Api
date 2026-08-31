using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VinayagaPlates.Application.DTOs;
using VinayagaPlates.Application.Services;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _service;
        public RoleController(IRoleService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _service.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var role = await _service.GetByIdAsync(id);
            return role == null ? NotFound() : Ok(role);
        }

        [HttpPost]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> Create([FromBody] RoleDto dto)
        {
            var created = await _service.CreateAsync(dto, User?.Identity?.Name);
            return CreatedAtAction(nameof(GetById), new { id = created.RoleId }, created);
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> Update(int id, [FromBody] RoleDto dto)
        {
            var success = await _service.UpdateAsync(id, dto, User?.Identity?.Name);
            return success ? NoContent() : NotFound();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminPolicy")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id, User?.Identity?.Name);
            return success ? NoContent() : NotFound();
        }
    }
}
