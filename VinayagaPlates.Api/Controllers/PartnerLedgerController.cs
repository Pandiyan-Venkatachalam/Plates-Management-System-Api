using System;
using System.Linq;
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
    public class PartnerLedgerController : ControllerBase
    {
        private readonly IPartnerLedgerRepository _ledgerRepo;
        private readonly IPartnerRepository _partnerRepo;
        private readonly VpmsService _vpms;
        private readonly ApplicationDbContext _db;

        public PartnerLedgerController(IPartnerLedgerRepository ledgerRepo, IPartnerRepository partnerRepo, VpmsService vpms, ApplicationDbContext db)
        {
            _ledgerRepo = ledgerRepo;
            _partnerRepo = partnerRepo;
            _vpms = vpms;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _ledgerRepo.GetLedgerWithPartnerAsync();
            var list = System.Linq.Enumerable.Select(data, l => new PartnerLedgerResponse(
                l.LedgerId,
                l.PartnerId,
                l.Partner?.PartnerName ?? "",
                l.TransactionType,
                l.Amount,
                l.Description,
                l.CreatedAt
            ));
            var response = ApiResponse<object>.Success(list, "Ledger entries retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var l = await _ledgerRepo.GetByIdAsync(id);
            if (l == null)
                return StatusCode(404, ApiResponse<string>.Fail("Ledger entry not found.", 404));

            var res = new PartnerLedgerResponse(
                l.LedgerId,
                l.PartnerId,
                l.Partner?.PartnerName ?? "",
                l.TransactionType,
                l.Amount,
                l.Description,
                l.CreatedAt
            );

            var response = ApiResponse<PartnerLedgerResponse>.Success(res, "Ledger entry retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PartnerLedgerCreateRequest req)
        {
            var ledger = new PartnerLedger
            {
                PartnerId = req.PartnerId,
                TransactionType = req.TransactionType,
                Amount = req.Amount,
                Description = req.Description,
                CreatedBy = User.Identity?.Name ?? "SYSTEM",
                CreatedAt = DateTime.UtcNow
            };

            await _ledgerRepo.AddAsync(ledger);
            await _ledgerRepo.SaveChangesAsync();

            var partner = await _partnerRepo.GetByIdAsync(ledger.PartnerId);
            var res = new PartnerLedgerResponse(
                ledger.LedgerId,
                ledger.PartnerId,
                partner?.PartnerName ?? "",
                ledger.TransactionType,
                ledger.Amount,
                ledger.Description,
                ledger.CreatedAt
            );

            var response = ApiResponse<PartnerLedgerResponse>.Success(res, "Ledger entry created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("create-transaction")]
        public async Task<IActionResult> CreateTransaction([FromBody] PartnerTransactionRequest req)
        {
            try
            {
                await _vpms.RecordPartnerTransactionAsync(
                    req.PartnerId,
                    req.TransactionType,
                    req.Amount,
                    req.Description,
                    req.AccountName,
                    User.Identity?.Name ?? "SYSTEM");

                var response = ApiResponse<object>.Success(null, "Partner transaction recorded successfully.", 201);
                return StatusCode(response.StatusCode, response);
            }
            catch (ArgumentException ex)
            {
                var response = ApiResponse<object>.Fail(ex.Message, 400);
                return StatusCode(response.StatusCode, response);
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PartnerLedgerUpdateRequest req)
        {
            var ledger = await _ledgerRepo.GetByIdAsync(id);
            if (ledger == null)
                return StatusCode(404, ApiResponse<string>.Fail("Ledger entry not found.", 404));

            var oldAmount = ledger.Amount;
            var oldDesc = ledger.Description;

            ledger.PartnerId = req.PartnerId;
            ledger.TransactionType = req.TransactionType;
            ledger.Amount = req.Amount;
            ledger.Description = req.Description;
            ledger.CreatedBy = User.Identity?.Name ?? "SYSTEM";

            _ledgerRepo.Update(ledger);
            await _ledgerRepo.SaveChangesAsync();

            // Synchronize linked AccountTransaction
            var allTxs = await _db.AccountTransactions.ToListAsync();
            var matchedTx = allTxs.FirstOrDefault(t => 
                t.ReferenceType == "PARTNER_TRANSACTION" && 
                (t.ReferenceId == $"LEDGER-{id}" || (t.Amount == oldAmount && t.Description.Contains(oldDesc ?? ""))));
            if (matchedTx != null)
            {
                matchedTx.Amount = req.Amount;
                matchedTx.TransactionType = req.TransactionType == "INVESTMENT" ? "CREDIT" : "DEBIT";
                matchedTx.Description = $"{req.TransactionType} by Partner. Details: {req.Description}";
                _db.AccountTransactions.Update(matchedTx);
                await _db.SaveChangesAsync();
            }

            var partner = await _partnerRepo.GetByIdAsync(ledger.PartnerId);
            var res = new PartnerLedgerResponse(
                ledger.LedgerId,
                ledger.PartnerId,
                partner?.PartnerName ?? "",
                ledger.TransactionType,
                ledger.Amount,
                ledger.Description,
                ledger.CreatedAt
            );

            var response = ApiResponse<PartnerLedgerResponse>.Success(res, "Ledger entry updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ledger = await _ledgerRepo.GetByIdAsync(id);
            if (ledger == null)
                return StatusCode(404, ApiResponse<string>.Fail("Ledger entry not found.", 404));

            // Clean up corresponding AccountTransaction if any
            var allTxs = await _db.AccountTransactions.ToListAsync();
            var matchedTx = allTxs.FirstOrDefault(t => 
                t.ReferenceType == "PARTNER_TRANSACTION" && 
                (t.ReferenceId == $"LEDGER-{id}" || (t.Amount == ledger.Amount && t.Description.Contains(ledger.Description ?? ""))));
            if (matchedTx != null)
            {
                _db.AccountTransactions.Remove(matchedTx);
            }

            _ledgerRepo.Delete(ledger);
            await _ledgerRepo.SaveChangesAsync();
            await _db.SaveChangesAsync();

            var response = ApiResponse<object>.Success(null, "Ledger entry deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
