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
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

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

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasIndex(e => e.Environment);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }
}
