using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

public class NotificationConfigService
{
    private readonly AppDbContext _db;

    public NotificationConfigService(AppDbContext db)
    {
        _db = db;
    }

    private static string NormalizeEnvironment(string environment) =>
        string.IsNullOrEmpty(environment) ? "" : environment.Trim().ToUpperInvariant();

    // ── Placeholder resolution ───────────────────────────────────
    public static string ResolvePlaceholders(string template, string environment, string? key = null, bool lowercaseEnv = true)
    {
        var env = lowercaseEnv ? environment.ToLowerInvariant() : environment;
        var result = template.Replace("{environment}", env);
        if (key != null)
            result = result.Replace("{key}", key);
        return result;
    }

    // ── NATS Config CRUD ─────────────────────────────────────────
    public async Task<List<NatsConfig>> GetNatsConfigsAsync(string environment)
    {
        environment = NormalizeEnvironment(environment);
        return await _db.NatsConfigs
            .Where(n => n.Environment == environment)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<NatsConfig> AddNatsConfigAsync(string environment, string serverUrl, string queue, bool lowercaseEnv = true, bool enabled = true)
    {
        var config = new NatsConfig
        {
            Environment = NormalizeEnvironment(environment),
            ServerUrl = serverUrl.Trim(),
            Queue = queue.Trim(),
            LowercaseEnvironment = lowercaseEnv,
            Enabled = enabled,
            CreatedAt = DateTime.UtcNow
        };
        _db.NatsConfigs.Add(config);
        await _db.SaveChangesAsync();
        return config;
    }

    public async Task<bool> UpdateNatsConfigAsync(int id, string serverUrl, string queue, bool lowercaseEnv, bool enabled)
    {
        var config = await _db.NatsConfigs.FindAsync(id);
        if (config == null) return false;

        config.ServerUrl = serverUrl.Trim();
        config.Queue = queue.Trim();
        config.LowercaseEnvironment = lowercaseEnv;
        config.Enabled = enabled;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteNatsConfigAsync(int id)
    {
        var config = await _db.NatsConfigs.FindAsync(id);
        if (config == null) return false;

        _db.NatsConfigs.Remove(config);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Webhook Config CRUD ──────────────────────────────────────
    public async Task<List<WebhookConfig>> GetWebhookConfigsAsync(string environment)
    {
        environment = NormalizeEnvironment(environment);
        return await _db.WebhookConfigs
            .Where(w => w.Environment == environment)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<WebhookConfig> AddWebhookConfigAsync(string environment, string url, bool lowercaseEnv = true, bool enabled = true)
    {
        var config = new WebhookConfig
        {
            Environment = NormalizeEnvironment(environment),
            Url = url.Trim(),
            LowercaseEnvironment = lowercaseEnv,
            Enabled = enabled,
            CreatedAt = DateTime.UtcNow
        };
        _db.WebhookConfigs.Add(config);
        await _db.SaveChangesAsync();
        return config;
    }

    public async Task<bool> UpdateWebhookConfigAsync(int id, string url, bool lowercaseEnv, bool enabled)
    {
        var config = await _db.WebhookConfigs.FindAsync(id);
        if (config == null) return false;

        config.Url = url.Trim();
        config.LowercaseEnvironment = lowercaseEnv;
        config.Enabled = enabled;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteWebhookConfigAsync(int id)
    {
        var config = await _db.WebhookConfigs.FindAsync(id);
        if (config == null) return false;

        _db.WebhookConfigs.Remove(config);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Helpers for getting all configs for notification dispatch ─
    public async Task<List<NatsConfig>> GetEnabledNatsConfigsAsync(string environment)
    {
        environment = NormalizeEnvironment(environment);
        return await _db.NatsConfigs
            .Where(n => n.Environment == environment && n.Enabled)
            .ToListAsync();
    }

    public async Task<List<WebhookConfig>> GetEnabledWebhookConfigsAsync(string environment)
    {
        environment = NormalizeEnvironment(environment);
        return await _db.WebhookConfigs
            .Where(w => w.Environment == environment && w.Enabled)
            .ToListAsync();
    }
}
