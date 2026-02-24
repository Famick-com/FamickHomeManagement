using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities
{
    /// <summary>
    /// Represents a chore execution log entry (when a chore was completed, skipped, or undone).
    /// Tracks the history of chore completions for reporting and scheduling.
    /// </summary>
    public class ChoreLog : BaseEntity, ITenantEntity
    {
        /// <summary>
        /// Tenant identifier for multi-tenancy isolation
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Chore this log entry belongs to
        /// </summary>
        public Guid ChoreId { get; set; }

        /// <summary>
        /// When the chore was actually done (or marked as done)
        /// </summary>
        public DateTime? TrackedTime { get; set; }

        /// <summary>
        /// User who completed the chore
        /// </summary>
        public Guid? DoneByUserId { get; set; }

        /// <summary>
        /// Whether this log entry was undone (completion was reversed)
        /// </summary>
        public bool Undone { get; set; } = false;

        /// <summary>
        /// When the completion was undone
        /// </summary>
        public DateTime? UndoneTimestamp { get; set; }

        /// <summary>
        /// Whether the chore was skipped instead of completed
        /// </summary>
        public bool Skipped { get; set; } = false;

        /// <summary>
        /// When the chore was scheduled to be executed
        /// </summary>
        public DateTime? ScheduledExecutionTime { get; set; }

        // Navigation properties
        /// <summary>
        /// The chore this log entry belongs to
        /// </summary>
        public virtual Chore? Chore { get; set; }

        /// <summary>
        /// The user who completed the chore
        /// </summary>
        public virtual User? DoneByUser { get; set; }
    }
}
