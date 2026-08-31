using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Interfaces;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;
using VinayagaPlates.Application.Constants;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly VpmsService _vpms;
        private readonly IJwtTokenGenerator _tokenGen;

        public AuthController(VpmsService vpms, IJwtTokenGenerator tokenGen)
        {
            _vpms = vpms;
            _tokenGen = tokenGen;
        }

        [HttpPost("seed")]
        public async Task<IActionResult> Seed()
        {
            await _vpms.SeedAsync();
            var response = ApiResponse<string>.Success("Database seeded successfully.", "Database seeded successfully.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _vpms.AuthenticateAsync(req.Username, req.Password);
            if (user == null)
            {
                var fail = ApiResponse<AuthResponse>.Fail("Invalid credentials or inactive account.", 401);
                return StatusCode(fail.StatusCode, fail);
            }

            var token = _tokenGen.GenerateToken(user);

            var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.PermissionName)
                .Distinct()
                .ToList();

            var authResp = new AuthResponse(token, user.FullName, user.Username, roles, permissions);
            var response = ApiResponse<AuthResponse>.Success(authResp, "Login successful.");
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("register")]
        [Authorize(Policy = $"{RoleConstants.Admin}Policy")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var success = await _vpms.RegisterUserAsync(req, User.Identity?.Name ?? RoleConstants.Admin);
            if (!success)
            {
                var fail = ApiResponse<string>.Fail("Username already exists or role is invalid.", 400);
                return StatusCode(fail.StatusCode, fail);
            }

            var response = ApiResponse<string>.Success("User registered successfully.", "User registered successfully.", 201);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("users")]
        [Authorize(Policy = $"{RoleConstants.Admin}Policy")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _vpms.GetAllUsersAsync();
            var response = ApiResponse<object>.Success(users, "Users retrieved successfully.");
            return StatusCode(response.StatusCode, response);
        }
    }
}
