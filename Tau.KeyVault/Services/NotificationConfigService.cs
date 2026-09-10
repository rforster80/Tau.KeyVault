using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

public class NotificationConfigService
{
    private readonly AppDbContext _db;
    private readonly string _encryptionSalt;
    private readonly KafkaProducerFactory _kafkaProducers;

    public NotificationConfigService(AppDbContext db, IConfiguration config, KafkaProducerFactory kafkaProducers)
    {
        _db = db;
        _kafkaProducers = kafkaProducers;
        _encryptionSalt = config[EncryptionService.SaltConfigKey]
            ?? throw new InvalidOperationException("Encryption:Salt is not configured in appsettings.json.");
    }

    // ── Kafka secret handling ────────────────────────────────────
    //  Broker credentials are stored encrypted with the vault salt, exactly as key values
    //  are, so a stolen database yields no usable broker access. They are decrypted only
    //  when a producer is built.

    private string? Encrypt(string? value) =>
        string.IsNullOrEmpty(value) ? value : EncryptionService.EncryptValue(value, _encryptionSalt);

    private string? Decrypt(string? value) =>
        string.IsNullOrEmpty(value) ? value : EncryptionService.DecryptValue(value, _encryptionSalt);

    /// <summary>Returns a copy with its secrets in plaintext, for building a producer.</summary>
    public KafkaConfig DecryptSecrets(KafkaConfig config)
    {
        var copy = CloneKafka(config);
        copy.SaslPassword = Decrypt(config.SaslPassword);
        copy.SslKeyPassword = Decrypt(config.SslKeyPassword);
        copy.OauthBearerClientSecret = Decrypt(config.OauthBearerClientSecret);
        return copy;
    }

    private static KafkaConfig CloneKafka(KafkaConfig c) => new()
    {
        Id = c.Id,
        Environment = c.Environment,
        BootstrapServers = c.BootstrapServers,
        Topic = c.Topic,
        LowercaseEnvironment = c.LowercaseEnvironment,
        Enabled = c.Enabled,
        CreatedAt = c.CreatedAt,
        ClientId = c.ClientId,
        SecurityProtocol = c.SecurityProtocol,
        SaslMechanism = c.SaslMechanism,
        SaslUsername = c.SaslUsername,
        SaslPassword = c.SaslPassword,
        SslCaLocation = c.SslCaLocation,
        SslCertificateLocation = c.SslCertificateLocation,
        SslKeyLocation = c.SslKeyLocation,
        SslKeyPassword = c.SslKeyPassword,
        EnableSslCertificateVerification = c.EnableSslCertificateVerification,
        SaslKerberosServiceName = c.SaslKerberosServiceName,
        SaslKerberosPrincipal = c.SaslKerberosPrincipal,
        SaslKerberosKeytab = c.SaslKerberosKeytab,
        OauthBearerClientId = c.OauthBearerClientId,
        OauthBearerClientSecret = c.OauthBearerClientSecret,
        OauthBearerTokenEndpointUrl = c.OauthBearerTokenEndpointUrl,
        OauthBearerScope = c.OauthBearerScope,
        OauthBearerExtensions = c.OauthBearerExtensions,
        Acks = c.Acks,
        MessageTimeoutMs = c.MessageTimeoutMs,
        RequestTimeoutMs = c.RequestTimeoutMs,
        EnableIdempotence = c.EnableIdempotence,
        CompressionType = c.CompressionType,
        AdditionalConfig = c.AdditionalConfig
    };

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

    // ── Kafka Config CRUD ────────────────────────────────────────
    public async Task<List<KafkaConfig>> GetKafkaConfigsAsync(string environment)
    {
        var env = NormalizeEnvironment(environment);
        return await _db.KafkaConfigs
            .Where(k => k.Environment == env)
            .OrderBy(k => k.BootstrapServers)
            .ToListAsync();
    }

    public async Task<List<KafkaConfig>> GetEnabledKafkaConfigsAsync(string environment)
    {
        var env = NormalizeEnvironment(environment);
        return await _db.KafkaConfigs
            .Where(k => k.Environment == env && k.Enabled)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a Kafka endpoint. <paramref name="config"/> carries secrets in plaintext; they are
    /// encrypted here before the row is written.
    /// </summary>
    public async Task<KafkaConfig> AddKafkaConfigAsync(string environment, KafkaConfig config)
    {
        var stored = CloneKafka(config);
        stored.Id = 0;
        stored.Environment = NormalizeEnvironment(environment);
        stored.CreatedAt = DateTime.UtcNow;
        stored.SaslPassword = Encrypt(config.SaslPassword);
        stored.SslKeyPassword = Encrypt(config.SslKeyPassword);
        stored.OauthBearerClientSecret = Encrypt(config.OauthBearerClientSecret);

        _db.KafkaConfigs.Add(stored);
        await _db.SaveChangesAsync();
        return stored;
    }

    /// <summary>
    /// Updates a Kafka endpoint. A null secret means "leave the stored one alone", so the UI
    /// can show a masked field without forcing the operator to retype credentials.
    /// </summary>
    public async Task<bool> UpdateKafkaConfigAsync(int id, KafkaConfig updated)
    {
        var config = await _db.KafkaConfigs.FindAsync(id);
        if (config is null) return false;

        config.BootstrapServers = updated.BootstrapServers;
        config.Topic = updated.Topic;
        config.LowercaseEnvironment = updated.LowercaseEnvironment;
        config.Enabled = updated.Enabled;
        config.ClientId = updated.ClientId;
        config.SecurityProtocol = updated.SecurityProtocol;
        config.SaslMechanism = updated.SaslMechanism;
        config.SaslUsername = updated.SaslUsername;
        config.SslCaLocation = updated.SslCaLocation;
        config.SslCertificateLocation = updated.SslCertificateLocation;
        config.SslKeyLocation = updated.SslKeyLocation;
        config.EnableSslCertificateVerification = updated.EnableSslCertificateVerification;
        config.SaslKerberosServiceName = updated.SaslKerberosServiceName;
        config.SaslKerberosPrincipal = updated.SaslKerberosPrincipal;
        config.SaslKerberosKeytab = updated.SaslKerberosKeytab;
        config.OauthBearerClientId = updated.OauthBearerClientId;
        config.OauthBearerTokenEndpointUrl = updated.OauthBearerTokenEndpointUrl;
        config.OauthBearerScope = updated.OauthBearerScope;
        config.OauthBearerExtensions = updated.OauthBearerExtensions;
        config.Acks = updated.Acks;
        config.MessageTimeoutMs = updated.MessageTimeoutMs;
        config.RequestTimeoutMs = updated.RequestTimeoutMs;
        config.EnableIdempotence = updated.EnableIdempotence;
        config.CompressionType = updated.CompressionType;
        config.AdditionalConfig = updated.AdditionalConfig;

        if (updated.SaslPassword is not null) config.SaslPassword = Encrypt(updated.SaslPassword);
        if (updated.SslKeyPassword is not null) config.SslKeyPassword = Encrypt(updated.SslKeyPassword);
        if (updated.OauthBearerClientSecret is not null) config.OauthBearerClientSecret = Encrypt(updated.OauthBearerClientSecret);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteKafkaConfigAsync(int id)
    {
        var config = await _db.KafkaConfigs.FindAsync(id);
        if (config is null) return false;

        _db.KafkaConfigs.Remove(config);
        await _db.SaveChangesAsync();

        // Drop the cached producer too, otherwise it keeps a broker connection open for a
        // configuration that no longer exists.
        _kafkaProducers.Invalidate(id);
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
