using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user);
    }
}
