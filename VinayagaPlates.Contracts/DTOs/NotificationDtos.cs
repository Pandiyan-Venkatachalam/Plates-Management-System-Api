using System;
using System.Collections.Generic;

namespace VinayagaPlates.Contracts.DTOs
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = "GENERAL"; // SALES, PURCHASE, STOCK, PRODUCT, PARTNER, EXPENSE, CUSTOMER, SUPPLIER
        public string ActionType { get; set; } = "CREATE"; // CREATE, UPDATE, ADJUST, DELETE
        public string PerformedBy { get; set; } = "Admin";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ReferenceId { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
    }

    public class SendWhatsAppRequestDto
    {
        public int? PartnerId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
    }

    public class PartnerWhatsAppDto
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
    }
}
