using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Application.Constants;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleConstants.Master + "," + RoleConstants.Admin)]
    public class BackupController : ControllerBase
    {
        private readonly IBackupService _backupService;

        public BackupController(IBackupService backupService)
        {
            _backupService = backupService;
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadJsonBackup()
        {
            try
            {
                var fileBytes = await _backupService.GenerateJsonBackupAsync();
                var fileName = $"vpms-backup-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json";
                return File(fileBytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to generate backup", Error = ex.Message });
            }
        }
    }
}
