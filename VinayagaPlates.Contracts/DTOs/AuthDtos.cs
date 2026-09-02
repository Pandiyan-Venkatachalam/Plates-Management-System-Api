using System;
using System.Collections.Generic;

namespace VinayagaPlates.Contracts.DTOs
{
    public record LoginRequest(string Username, string Password);
    
    public record RegisterRequest(
        string FullName, 
        string Username, 
        string Email, 
        string Phone, 
        string Password, 
        string Role);

    public record AuthResponse(
        string Token, 
        string FullName, 
        string Username, 
        List<string> Roles, 
        List<string> Permissions);
}
