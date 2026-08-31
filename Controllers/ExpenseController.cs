using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Contracts.DTOs;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExpenseController : ControllerBase
    {
        private readonly IExpenseRepository _expenseRepo;

        public ExpenseController(IExpenseRepository expenseRepo)
        {
            _expenseRepo = expenseRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllExpenses()
        {
            var data = await _expenseRepo.GetExpensesOnlyAsync();
            return StatusCode(200, ApiResponse<object>.Success(data, "Expenses retrieved successfully."));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetExpenseById(int id)
        {
            var expense = await _expenseRepo.GetExpenseByIdAsync(id);
            if (expense == null)
                return StatusCode(404, ApiResponse<string>.Fail("Expense not found.", 404));

            return StatusCode(200, ApiResponse<object>.Success(expense, "Expense retrieved successfully."));
        }

        [HttpPost("create-expense")]
        public async Task<IActionResult> CreateExpense([FromBody] ExpenseCreateRequest req)
        {
            try
            {
                var createdExpense = await _expenseRepo.CreateExpenseAsync(
                    req.Description,
                    req.Amount,
                    req.AccountId,
                    User.Identity?.Name ?? "SYSTEM");

                return StatusCode(201, ApiResponse<object>.Success(createdExpense, "Expense recorded successfully.", 201));
            }
            catch (ArgumentException ex)
            {
                return StatusCode(400, ApiResponse<string>.Fail(ex.Message, 400));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] ExpenseCreateRequest req)
        {
            try
            {
                var updatedExpense = await _expenseRepo.UpdateExpenseAsync(id, req.Description, req.Amount, req.AccountId);

                return StatusCode(200, ApiResponse<object>.Success(updatedExpense, "Expense updated successfully."));
            }
            catch (ArgumentException ex)
            {
                if (ex.Message.Contains("not found"))
                {
                    return StatusCode(404, ApiResponse<string>.Fail(ex.Message, 404));
                }
                return StatusCode(400, ApiResponse<string>.Fail(ex.Message, 400));
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var deleted = await _expenseRepo.DeleteExpenseAsync(id);
            if (!deleted)
                return StatusCode(404, ApiResponse<string>.Fail("Expense not found.", 404));

            return StatusCode(200, ApiResponse<string>.Success("Expense deleted successfully.", "Expense deleted successfully."));
        }
    }
}
