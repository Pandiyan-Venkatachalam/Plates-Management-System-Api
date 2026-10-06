using System;
using System.Threading.Tasks;

namespace VinayagaPlates.Application.Services
{
    public interface IBackupService
    {
        Task<byte[]> GenerateJsonBackupAsync();
    }
}
