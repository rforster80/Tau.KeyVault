using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<KeyEntry> KeyEntries => Set<KeyEntry>();
    public DbSet<NatsConfig> NatsConfigs => Set<NatsConfig>();
    public DbSet<WebhookConfig> WebhookConfigs => Set<WebhookConfig>();
    public DbSet<KafkaConfig> KafkaConfigs => Set<KafkaConfig>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<AccessAuditLog> AccessAuditLogs => Set<AccessAuditLog>();
    public DbSet<EnvironmentApiKey> EnvironmentApiKeys => Set<EnvironmentApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<KeyEntry>(entity =>
        {
            entity.HasIndex(e => new { e.Key, e.Environment }).IsUnique();
        });

        modelBuilder.Entity<NatsConfig>(entity =>
        {
            entity.HasIndex(e => e.Environment);
        });

        modelBuilder.Entity<WebhookConfig>(entity =>
        {
            entity.HasIndex(e => e.Environment);
        });

        modelBuilder.Entity<KafkaConfig>(entity =>
        {
            entity.HasIndex(e => e.Environment);
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasIndex(e => e.Environment);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });

        modelBuilder.Entity<EnvironmentApiKey>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            // Authentication looks the credential up by hash, so this must be both unique
            // and indexed — it is on the hot path for every scoped request.
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => e.Environment);
        });

        modelBuilder.Entity<AccessAuditLog>(entity =>
        {
            // Indexed for the questions the log exists to answer: what happened to this key,
            // what has this credential been doing, and what happened in this window.
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.Key, e.Environment });
            entity.HasIndex(e => e.ActorId);
            entity.HasIndex(e => e.Action);
        });
    }
}
