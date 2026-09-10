namespace Tau.KeyVault.Models;

public enum NotificationType
{
    Nats = 0,
    Webhook = 1,
    Kafka = 2
}

public class NotificationLog
{
    public int Id { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public NotificationType Type { get; set; }

    /// <summary>
    /// The resolved target (NATS server+queue, Kafka brokers+topic, or webhook URL
    /// after placeholder resolution).
    /// </summary>
    public string Target { get; set; } = string.Empty;

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// HTTP status code for webhooks, or 0 for NATS and Kafka.
    /// </summary>
    public int StatusCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
