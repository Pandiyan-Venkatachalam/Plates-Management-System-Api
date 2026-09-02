using System;
using System.Collections.Generic;
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
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerRepository _customerRepo;

        public CustomerController(ICustomerRepository customerRepo)
        {
            _customerRepo = customerRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _customerRepo.GetCustomersAsync();
            var response = ApiResponse<object>.Success(data, "Customers retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _customerRepo.GetByIdAsync(id);
            if (data == null)
                return NotFound(ApiResponse<object>.Fail("Customer not found.", 404));

            var response = ApiResponse<Customer>.Success(data, "Customer retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("create-customer")]
        public async Task<IActionResult> CreateCustomer([FromBody] Customer customer)
        {
            customer.CreatedBy = User.Identity?.Name ?? "SYSTEM";
            await _customerRepo.AddCustomerAsync(customer);
            await _customerRepo.SaveChangesAsync();
            var response = ApiResponse<Customer>.Success(customer, "Customer created successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] Customer req)
        {
            var customer = await _customerRepo.GetByIdAsync(id);
            if (customer == null)
                return NotFound(ApiResponse<object>.Fail("Customer not found.", 404));

            customer.CustomerName = req.CustomerName;
            customer.Phone = req.Phone ?? "";
            customer.Email = req.Email ?? "";
            customer.Address = req.Address ?? "";
            customer.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
            customer.UpdatedAt = DateTime.UtcNow;

            _customerRepo.Update(customer);
            await _customerRepo.SaveChangesAsync();

            var response = ApiResponse<Customer>.Success(customer, "Customer updated successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _customerRepo.GetByIdAsync(id);
            if (customer == null)
                return NotFound(ApiResponse<object>.Fail("Customer not found.", 404));

            customer.IsDeleted = true;
            customer.UpdatedBy = User.Identity?.Name ?? "SYSTEM";
            customer.UpdatedAt = DateTime.UtcNow;

            _customerRepo.Update(customer);
            await _customerRepo.SaveChangesAsync();

            var response = ApiResponse<object>.Success(null, "Customer deleted successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
