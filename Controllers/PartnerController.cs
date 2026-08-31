using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PartnerController : ControllerBase
    {
        private readonly IPartnerRepository _partnerRepo;
        private readonly VpmsService _vpms;
        private readonly ApplicationDbContext _db;

        public PartnerController(IPartnerRepository partnerRepo, VpmsService vpms, ApplicationDbContext db)
        {
            _partnerRepo = partnerRepo;
            _vpms = vpms;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _partnerRepo.GetAllAsync();
            var response = ApiResponse<object>.Success(data, "Partners retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _partnerRepo.GetByIdAsync(id);
            if (data == null)
                return NotFound(ApiResponse<object>.Fail("Partner not found.", 404));

            var response = ApiResponse<Partner>.Success(data, "Partner retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePartner([FromBody] Partner partner)
        {
            await _partnerRepo.CreatePartnerAsync(partner, User.Identity?.Name ?? "SYSTEM");
            var response = ApiResponse<Partner>.Success(partner, "Partner created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePartner(int id, [FromBody] Partner req)
        {
            var partner = await _partnerRepo.GetByIdAsync(id);
            if (partner == null)
                return NotFound(ApiResponse<object>.Fail("Partner not found.", 404));

            partner.PartnerName = req.PartnerName;
            partner.ContactPhone = req.ContactPhone ?? "";
            partner.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
            partner.UpdatedAt = DateTime.UtcNow;

            _partnerRepo.Update(partner);
            await _partnerRepo.SaveChangesAsync();

            var response = ApiResponse<Partner>.Success(partner, "Partner updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePartner(int id)
        {
            var partner = await _partnerRepo.GetByIdAsync(id);
            if (partner == null)
                return NotFound(ApiResponse<object>.Fail("Partner not found.", 404));

            bool hasLedger = await _db.PartnerLedgers.AnyAsync(pl => pl.PartnerId == id);
            if (hasLedger)
                return StatusCode(400, ApiResponse<object>.Fail("Cannot delete this partner because they have active capital ledger entries linked to them.", 400));

            _partnerRepo.Delete(partner);
            await _partnerRepo.SaveChangesAsync();

            var response = ApiResponse<object>.Success(null, "Partner deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
