using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

/// <summary>
/// Dispatches notifications to configured NATS servers, Kafka topics and webhooks when
/// key-value pairs change. All dispatch attempts are logged to the NotificationLogs table.
/// </summary>
public class NotificationDispatchService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationDispatchService> _logger;
    private readonly KafkaProducerFactory _kafkaProducers;
    private readonly NotificationConfigService _configs;

    public NotificationDispatchService(AppDbContext db, HttpClient httpClient,
        ILogger<NotificationDispatchService> logger, KafkaProducerFactory kafkaProducers,
        NotificationConfigService configs)
    {
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
        _kafkaProducers = kafkaProducers;
        _configs = configs;
    }

    private static string NormalizeEnvironment(string environment) =>
        string.IsNullOrEmpty(environment) ? "" : environment.Trim().ToUpperInvariant();

    /// <summary>
    /// Dispatches notifications for a key change to all enabled NATS and webhook configs.
    /// This runs fire-and-forget style — errors are logged but never thrown.
    /// </summary>
    public async Task DispatchAsync(string environment, string key)
    {
        var env = NormalizeEnvironment(environment);

        try
        {
            var natsConfigs = await _db.NatsConfigs
                .Where(n => n.Environment == env && n.Enabled)
                .ToListAsync();

            var kafkaConfigs = await _db.KafkaConfigs
                .Where(k => k.Environment == env && k.Enabled)
                .ToListAsync();

            var webhookConfigs = await _db.WebhookConfigs
                .Where(w => w.Environment == env && w.Enabled)
                .ToListAsync();

            // Dispatch NATS
            foreach (var nats in natsConfigs)
            {
                await DispatchNatsAsync(nats, env, key);
            }

            // Dispatch Kafka
            foreach (var kafka in kafkaConfigs)
            {
                await DispatchKafkaAsync(kafka, env, key);
            }

            // Dispatch webhooks
            foreach (var webhook in webhookConfigs)
            {
                await DispatchWebhookAsync(webhook, env, key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching notifications for env={Environment}, key={Key}", env, key);
        }
    }

    private async Task DispatchNatsAsync(NatsConfig config, string environment, string key)
    {
        var resolvedQueue = NotificationConfigService.ResolvePlaceholders(
            config.Queue, environment, key, config.LowercaseEnvironment);
        var target = $"{config.ServerUrl} → {resolvedQueue}";

        try
        {
            var opts = new NatsOpts { Url = config.ServerUrl };
            await using var connection = new NatsConnection(opts);
            await connection.ConnectAsync();

            var payload = JsonSerializer.Serialize(new
            {
                environment,
                key,
                timestamp = DateTime.UtcNow
            });

            await connection.PublishAsync(resolvedQueue, payload);

            _logger.LogInformation("NATS publish OK: {Target}", target);
            await LogAsync(environment, key, NotificationType.Nats, target, success: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NATS publish FAILED: {Target}", target);
            await LogAsync(environment, key, NotificationType.Nats, target, success: false, error: ex.Message);
        }
    }

    private async Task DispatchKafkaAsync(KafkaConfig config, string environment, string key)
    {
        var resolvedTopic = NotificationConfigService.ResolvePlaceholders(
            config.Topic, environment, key, config.LowercaseEnvironment);
        var target = $"{config.BootstrapServers} → {resolvedTopic}";

        try
        {
            // Secrets live encrypted in the row; decrypt only to build the producer.
            var connectable = _configs.DecryptSecrets(config);
            var producer = _kafkaProducers.GetProducer(connectable);

            var payload = JsonSerializer.Serialize(new
            {
                environment,
                key,
                timestamp = DateTime.UtcNow
            });

            // The vault key is the message key, so a consumer gets per-key ordering and
            // partitioning for free.
            var result = await producer.ProduceAsync(resolvedTopic, new Message<string, string>
            {
                Key = key,
                Value = payload
            });

            _logger.LogInformation("Kafka publish OK: {Target} (partition {Partition}, offset {Offset})",
                target, result.Partition.Value, result.Offset.Value);
            await LogAsync(environment, key, NotificationType.Kafka, target, success: true);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogWarning(ex, "Kafka publish FAILED: {Target}", target);
            await LogAsync(environment, key, NotificationType.Kafka, target, success: false,
                error: $"{ex.Error.Code}: {ex.Error.Reason}");
        }
        catch (Exception ex)
        {
            // Includes producer construction failures, e.g. a malformed security configuration.
            _logger.LogWarning(ex, "Kafka dispatch FAILED: {Target}", target);
            await LogAsync(environment, key, NotificationType.Kafka, target, success: false, error: ex.Message);
        }
    }

    private async Task DispatchWebhookAsync(WebhookConfig config, string environment, string key)
    {
        var resolvedUrl = NotificationConfigService.ResolvePlaceholders(
            config.Url, environment, key, config.LowercaseEnvironment);

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                environment,
                key,
                timestamp = DateTime.UtcNow
            });

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(resolvedUrl, content);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook OK: {Url} → {StatusCode}", resolvedUrl, statusCode);
                await LogAsync(environment, key, NotificationType.Webhook, resolvedUrl,
                    success: true, statusCode: statusCode);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                var errorMsg = $"HTTP {statusCode}: {Truncate(body, 500)}";
                _logger.LogWarning("Webhook FAILED: {Url} → {Error}", resolvedUrl, errorMsg);
                await LogAsync(environment, key, NotificationType.Webhook, resolvedUrl,
                    success: false, error: errorMsg, statusCode: statusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook FAILED: {Url}", resolvedUrl);
            await LogAsync(environment, key, NotificationType.Webhook, resolvedUrl,
                success: false, error: ex.Message);
        }
    }

    private async Task LogAsync(string environment, string key, NotificationType type,
        string target, bool success, string? error = null, int statusCode = 0)
    {
        try
        {
            _db.NotificationLogs.Add(new NotificationLog
            {
                Environment = environment,
                Key = key,
                Type = type,
                Target = target,
                Success = success,
                ErrorMessage = error,
                StatusCode = statusCode,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write notification log");
        }
    }

    // ── Log retrieval ────────────────────────────────────────────

    public async Task<List<NotificationLog>> GetLogsAsync(string? environment = null, int limit = 100)
    {
        var query = _db.NotificationLogs.AsQueryable();
        if (environment is not null)
        {
            var env = NormalizeEnvironment(environment);
            query = query.Where(l => l.Environment == env);
        }
        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> ClearLogsAsync(string? environment = null)
    {
        IQueryable<NotificationLog> query = _db.NotificationLogs;
        if (environment is not null)
        {
            var env = NormalizeEnvironment(environment);
            query = query.Where(l => l.Environment == env);
        }
        var logs = await query.ToListAsync();
        _db.NotificationLogs.RemoveRange(logs);
        await _db.SaveChangesAsync();
        return logs.Count;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
