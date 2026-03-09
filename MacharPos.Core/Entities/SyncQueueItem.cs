using System;

namespace MacharPos.Core.Entities
{
    public enum SyncOperationType
    {
        Create = 1,
        Update = 2,
        Delete = 3
    }

    public class SyncQueueItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string EntityType { get; set; } = "";
        public Guid EntityId { get; set; }

        public SyncOperationType OperationType { get; set; }

        public string PayloadJson { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Synced { get; set; } = false;

        public DateTime? SyncedAt { get; set; }
    }
}