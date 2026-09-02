using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly IAccountRepository _accountRepo;

        public AccountController(IAccountRepository accountRepo)
        {
            _accountRepo = accountRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _accountRepo.GetAllAsync();
            return StatusCode(200, ApiResponse<object>.Success(data, "Accounts retrieved successfully."));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var acc = await _accountRepo.GetByIdAsync(id);
            if (acc == null)
                return StatusCode(404, ApiResponse<string>.Fail("Account not found.", 404));

            return StatusCode(200, ApiResponse<object>.Success(acc, "Account retrieved successfully."));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AccountCreateRequest req)
        {
            var acc = new BusinessAccount
            {
                AccountName = req.AccountName,
                AccountType = req.AccountType,
                CreatedBy = User.Identity?.Name ?? "SYSTEM",
                CreatedAt = DateTime.UtcNow
            };

            await _accountRepo.AddAsync(acc);
            await _accountRepo.SaveChangesAsync();

            return StatusCode(201, ApiResponse<object>.Success(acc, "Account created successfully.", 201));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AccountUpdateRequest req)
        {
            var acc = await _accountRepo.GetByIdAsync(id);
            if (acc == null)
                return StatusCode(404, ApiResponse<string>.Fail("Account not found.", 404));

            acc.AccountName = req.AccountName;
            acc.AccountType = req.AccountType;
            acc.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
            acc.UpdatedAt = DateTime.UtcNow;

            _accountRepo.Update(acc);
            await _accountRepo.SaveChangesAsync();

            return StatusCode(200, ApiResponse<object>.Success(acc, "Account updated successfully."));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var acc = await _accountRepo.GetByIdAsync(id);
            if (acc == null)
                return StatusCode(404, ApiResponse<string>.Fail("Account not found.", 404));

            var txs = await _accountRepo.GetTransactionsAsync();
            if (txs != null && System.Linq.Enumerable.Any(txs, t => t.AccountId == id))
            {
                return StatusCode(400, ApiResponse<string>.Fail("Cannot delete this account because it has active transaction records linked to it.", 400));
            }

            _accountRepo.Delete(acc);
            await _accountRepo.SaveChangesAsync();

            return StatusCode(200, ApiResponse<string>.Success("Account deleted successfully.", "Account deleted successfully."));
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetAllTransactions()
        {
            var data = await _accountRepo.GetTransactionsAsync();
            return StatusCode(200, ApiResponse<object>.Success(data, "Transactions retrieved successfully."));
        }
    }
}
