using System;

namespace VinayagaPlates.Domain.Entities
{
    public class AuditLog
    {
        public int AuditId { get; set; }
        public string Username { get; set; }
        public string ActionName { get; set; }
        public string TableName { get; set; }
        public string RecordId { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
