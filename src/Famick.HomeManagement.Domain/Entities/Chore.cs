using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities
{
    /// <summary>
    /// Represents a chore/task that needs to be performed periodically.
    /// Example: "Water plants", "Clean kitchen", "Check smoke detectors", "Feed pets"
    /// </summary>
    public class Chore : BaseEntity, ITenantEntity
    {
        /// <summary>
        /// Tenant identifier for multi-tenancy isolation
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Chore name (unique per tenant)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description or instructions
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Period type: 'manually', 'dynamic-regular', 'daily', 'weekly', 'monthly'
        /// </summary>
        public string PeriodType { get; set; } = "manually";

        /// <summary>
        /// Number of days for dynamic-regular, or day-of-month for monthly
        /// </summary>
        public int? PeriodDays { get; set; }

        /// <summary>
        /// Track only date, not time (for chores that don't need precise timing)
        /// </summary>
        public bool TrackDateOnly { get; set; } = false;

        /// <summary>
        /// Roll over to current day if not completed by scheduled time
        /// </summary>
        public bool Rollover { get; set; } = false;

        /// <summary>
        /// How chores are assigned to users (e.g., "round-robin", "specific-user")
        /// </summary>
        public string? AssignmentType { get; set; }

        /// <summary>
        /// Assignment configuration (JSON/CSV of assigned user IDs)
        /// </summary>
        public string? AssignmentConfig { get; set; }

        /// <summary>
        /// User assigned to the next execution
        /// </summary>
        public Guid? NextExecutionAssignedToUserId { get; set; }

        /// <summary>
        /// Consume product stock when chore is marked done
        /// </summary>
        public bool ConsumeProductOnExecution { get; set; } = false;

        /// <summary>
        /// Product to consume when chore is marked done (if ConsumeProductOnExecution is true)
        /// </summary>
        public Guid? ProductId { get; set; }

        /// <summary>
        /// Amount of product to consume
        /// </summary>
        public decimal? ProductAmount { get; set; }

        /// <summary>
        /// Optional equipment this chore is associated with (for maintenance tasks)
        /// </summary>
        public Guid? EquipmentId { get; set; }

        // Navigation properties
        /// <summary>
        /// Product to consume on execution (if specified)
        /// </summary>
        public virtual Product? Product { get; set; }

        /// <summary>
        /// User assigned to next execution (if specified)
        /// </summary>
        public virtual User? NextExecutionAssignedToUser { get; set; }

        /// <summary>
        /// Equipment associated with this chore (for maintenance tasks)
        /// </summary>
        public virtual Equipment? Equipment { get; set; }

        /// <summary>
        /// Execution log entries for this chore
        /// </summary>
        public virtual ICollection<ChoreLog>? LogEntries { get; set; }
    }
}
