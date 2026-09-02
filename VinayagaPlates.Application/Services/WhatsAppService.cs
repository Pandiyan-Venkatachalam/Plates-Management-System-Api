using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(ApplicationDbContext db, IConfiguration config, ILogger<WhatsAppService> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        public async Task<List<PartnerWhatsAppDto>> GetPartnerWhatsAppRecipientsAsync()
        {
            try
            {
                var partners = await _db.Partners
                    .Where(p => !p.IsDeleted && !string.IsNullOrWhiteSpace(p.ContactPhone))
                    .Select(p => new PartnerWhatsAppDto
                    {
                        PartnerId = p.PartnerId,
                        PartnerName = p.PartnerName,
                        ContactPhone = CleanPhoneNumber(p.ContactPhone)
                    })
                    .ToListAsync();

                return partners.Where(p => !string.IsNullOrEmpty(p.ContactPhone)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching partner WhatsApp recipients");
                return new List<PartnerWhatsAppDto>();
            }
        }

        public async Task<bool> SendWhatsAppMessageAsync(string phoneNumber, string message)
        {
            var cleanedPhone = CleanPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(cleanedPhone)) return false;

            try
            {
                _logger.LogInformation("WhatsApp Alert prepared for {Phone}: {Message}", cleanedPhone, message);

                var instanceId = _config["WhatsApp:InstanceId"];
                var token = _config["WhatsApp:Token"];
                var webhookUrl = _config["WhatsApp:WebhookUrl"];

                if (!string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(token))
                {
                    // Direct UltraMsg / Standard Gateway Dispatch
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    var values = new Dictionary<string, string>
                    {
                        { "token", token },
                        { "to", cleanedPhone },
                        { "body", message }
                    };
                    var content = new FormUrlEncodedContent(values);
                    var url = $"https://api.ultramsg.com/{instanceId}/messages/chat";
                    await client.PostAsync(url, content);
                }
                else if (!string.IsNullOrEmpty(webhookUrl))
                {
                    // Generic Webhook Gateway
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    var payload = new
                    {
                        phone = cleanedPhone,
                        message = message,
                        timestamp = DateTime.UtcNow
                    };
                    var jsonContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                    await client.PostAsync(webhookUrl, jsonContent);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending WhatsApp to {Phone}", cleanedPhone);
                return false;
            }
        }

        public async Task<int> BroadcastToPartnersAsync(string message, string eventType)
        {
            var partners = await GetPartnerWhatsAppRecipientsAsync();
            int successCount = 0;

            foreach (var partner in partners)
            {
                var sent = await SendWhatsAppMessageAsync(partner.ContactPhone, message);
                if (sent) successCount++;
            }

            try
            {
                // Record in AuditLog
                _db.AuditLogs.Add(new AuditLog
                {
                    Username = "SYSTEM",
                    ActionName = $"WHATSAPP_ALERT_{eventType.ToUpper()}",
                    TableName = "Partners",
                    RecordId = string.Join(",", partners.Select(p => p.PartnerId)),
                    NewValues = message,
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording WhatsApp broadcast audit");
            }

            return successCount;
        }

        private static string CleanPhoneNumber(string rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone)) return string.Empty;
            var digitsOnly = Regex.Replace(rawPhone, @"[^\d]", "");
            
            // Standardize Indian 10-digit mobile numbers with country code 91
            if (digitsOnly.Length == 10)
            {
                return "91" + digitsOnly;
            }
            if (digitsOnly.Length == 12 && digitsOnly.StartsWith("91"))
            {
                return digitsOnly;
            }
            return digitsOnly;
        }
    }
}
