using Famick.HomeManagement.Core.DTOs.Transfer;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Web.Data;

/// <summary>
/// Separate DbContext for transfer tracking tables.
/// These tables are local to the self-hosted instance only.
/// </summary>
public class TransferDbContext : DbContext
{
    public TransferDbContext(DbContextOptions<TransferDbContext> options)
        : base(options)
    {
    }

    public DbSet<TransferSession> TransferSessions => Set<TransferSession>();
    public DbSet<TransferItemLog> TransferItemLogs => Set<TransferItemLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TransferSession>(entity =>
        {
            entity.ToTable("transfer_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CloudUrl).HasMaxLength(500);
            entity.Property(e => e.CloudEmail).HasMaxLength(256);
            entity.Property(e => e.EncryptedRefreshToken).HasMaxLength(2000);
            entity.Property(e => e.CurrentCategory).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasMany(e => e.Items)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.TransferSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransferItemLog>(entity =>
        {
            entity.ToTable("transfer_item_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasIndex(e => new { e.TransferSessionId, e.Category, e.SourceId });
        });
    }
}
