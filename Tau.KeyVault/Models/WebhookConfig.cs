namespace Tau.KeyVault.Models;

public class WebhookConfig
{
    public int Id { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool LowercaseEnvironment { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
