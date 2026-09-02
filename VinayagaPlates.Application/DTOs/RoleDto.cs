using System.Collections.Generic;

namespace VinayagaPlates.Application.DTOs
{
    public class RoleDto
    {
        public int? RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
    }
}
