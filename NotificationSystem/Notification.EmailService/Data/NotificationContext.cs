using Microsoft.EntityFrameworkCore;

namespace Notification.EmailService.Data;

public class NotificationContext : DbContext
{
    public DbSet<NotificationLog> Notifications { get; set; }

    public NotificationContext(DbContextOptions<NotificationContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.To);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.Status)
                  .HasDefaultValue("Pending");

            entity.Property(e => e.RetryCount)
                  .HasDefaultValue(0);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is NotificationLog &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entityEntry in entries)
        {
            if (entityEntry.State == EntityState.Added)
            {
                ((NotificationLog)entityEntry.Entity).CreatedAt = DateTime.UtcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}