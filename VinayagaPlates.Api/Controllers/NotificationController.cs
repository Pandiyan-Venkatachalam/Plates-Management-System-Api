using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application;
using VinayagaPlates.Application.Services;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IWhatsAppService _whatsAppService;

        public NotificationController(ApplicationDbContext db, IWhatsAppService whatsAppService)
        {
            _db = db;
            _whatsAppService = whatsAppService;
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentNotifications([FromQuery] int limit = 30)
        {
            try
            {
                var auditLogs = await _db.AuditLogs
                    .OrderByDescending(a => a.Timestamp)
                    .Take(limit)
                    .ToListAsync();

                var notifications = auditLogs.Select(a => MapAuditToNotification(a)).ToList();

                var response = ApiResponse<List<NotificationDto>>.Success(notifications, "Recent notifications retrieved successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var errorResp = ApiResponse<List<NotificationDto>>.Fail(ex.Message, 500);
                return StatusCode(500, errorResp);
            }
        }

        [HttpGet("partner-recipients")]
        public async Task<IActionResult> GetPartnerRecipients()
        {
            try
            {
                var recipients = await _whatsAppService.GetPartnerWhatsAppRecipientsAsync();
                var response = ApiResponse<List<PartnerWhatsAppDto>>.Success(recipients, "Partner recipients retrieved successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var errorResp = ApiResponse<List<PartnerWhatsAppDto>>.Fail(ex.Message, 500);
                return StatusCode(500, errorResp);
            }
        }

        [HttpPost("broadcast-whatsapp")]
        public async Task<IActionResult> BroadcastWhatsApp([FromBody] SendWhatsAppRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                var badResp = ApiResponse<object>.Fail("Message content cannot be empty", 400);
                return BadRequest(badResp);
            }

            try
            {
                var count = await _whatsAppService.BroadcastToPartnersAsync(request.Message, request.EventType ?? "GENERAL");
                var response = ApiResponse<object>.Success(new { recipientsCount = count }, $"WhatsApp notification broadcasted to {count} partner(s).");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var errorResp = ApiResponse<object>.Fail(ex.Message, 500);
                return StatusCode(500, errorResp);
            }
        }

        [HttpPost("record-activity")]
        public async Task<IActionResult> RecordActivity([FromBody] NotificationDto activity)
        {
            try
            {
                var currentUsername = User?.FindFirst(ClaimTypes.Name)?.Value 
                    ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? activity.PerformedBy 
                    ?? "Admin";

                var audit = new AuditLog
                {
                    Username = currentUsername,
                    ActionName = $"{activity.Category}_{activity.ActionType}",
                    TableName = activity.Category,
                    RecordId = activity.ReferenceId,
                    NewValues = activity.Message,
                    Timestamp = DateTime.UtcNow
                };

                _db.AuditLogs.Add(audit);
                await _db.SaveChangesAsync();

                var response = ApiResponse<object>.Success(new { auditId = audit.AuditId }, "Activity logged successfully.");
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                var errorResp = ApiResponse<object>.Fail(ex.Message, 500);
                return StatusCode(500, errorResp);
            }
        }

        private static NotificationDto MapAuditToNotification(AuditLog a)
        {
            var actionUpper = (a.ActionName ?? "").ToUpper();
            var tableUpper = (a.TableName ?? "").ToUpper();

            string category = "GENERAL";
            string actionType = "UPDATE";

            if (tableUpper.Contains("SALE") || actionUpper.Contains("SALE")) category = "SALES";
            else if (tableUpper.Contains("PURCHASE") || actionUpper.Contains("PURCHASE")) category = "PURCHASE";
            else if (tableUpper.Contains("BATCH") || tableUpper.Contains("STOCK") || tableUpper.Contains("INVENTORY") || actionUpper.Contains("BATCH") || actionUpper.Contains("STOCK")) category = "STOCK";
            else if (tableUpper.Contains("PRODUCT") || actionUpper.Contains("PRODUCT")) category = "PRODUCT";
            else if (tableUpper.Contains("PARTNER") || actionUpper.Contains("PARTNER")) category = "PARTNER";
            else if (tableUpper.Contains("EXPENSE") || tableUpper.Contains("ACCOUNT") || actionUpper.Contains("EXPENSE")) category = "EXPENSE";
            else if (tableUpper.Contains("CUSTOMER") || actionUpper.Contains("CUSTOMER")) category = "CUSTOMER";
            else if (tableUpper.Contains("SUPPLIER") || actionUpper.Contains("SUPPLIER")) category = "SUPPLIER";

            if (actionUpper.Contains("CREATE") || actionUpper.Contains("ADD") || actionUpper.Contains("INSERT")) actionType = "CREATE";
            else if (actionUpper.Contains("ADJUST")) actionType = "ADJUST";
            else if (actionUpper.Contains("DELETE") || actionUpper.Contains("REMOVE")) actionType = "DELETE";
            else if (actionUpper.Contains("PAID") || actionUpper.Contains("COLLECT")) actionType = "PAYMENT";
            else actionType = "UPDATE";

            string title = $"{category} {actionType}";
            if (category == "SALES" && actionType == "CREATE") title = "New Sales Invoice Created";
            else if (category == "SALES" && actionType == "UPDATE") title = "Sales Invoice Updated";
            else if (category == "SALES" && actionType == "PAYMENT") title = "Sales Payment Received";
            else if (category == "PURCHASE" && actionType == "CREATE") title = "New Purchase Order Recorded";
            else if (category == "PURCHASE" && actionType == "UPDATE") title = "Purchase Order Updated";
            else if (category == "STOCK" && actionType == "ADJUST") title = "Inventory Batch Adjusted";
            else if (category == "STOCK" && actionType == "CREATE") title = "New Stock Batch Received";
            else if (category == "PRODUCT" && actionType == "CREATE") title = "New Product Added to Catalog";
            else if (category == "EXPENSE" && actionType == "CREATE") title = "New Expense Recorded";
            else if (category == "PARTNER") title = "Partner Ledger Entry Updated";

            return new NotificationDto
            {
                Id = a.AuditId,
                Title = title,
                Message = !string.IsNullOrWhiteSpace(a.NewValues) ? a.NewValues : $"{a.ActionName} on {a.TableName} #{a.RecordId}",
                Category = category,
                ActionType = actionType,
                PerformedBy = !string.IsNullOrWhiteSpace(a.Username) ? a.Username : "Admin",
                Timestamp = a.Timestamp,
                ReferenceId = a.RecordId ?? string.Empty,
                IsRead = false
            };
        }
    }
}
