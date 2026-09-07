using System;

namespace VinayagaPlates.Domain.Entities
{
    public class AuditLog
    {
        public int AuditId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string RecordId { get; set; } = string.Empty;
        public string OldValues { get; set; } = string.Empty;
        public string NewValues { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
