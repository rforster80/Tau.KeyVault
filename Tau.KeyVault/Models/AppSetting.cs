namespace Tau.KeyVault.Models;

/// <summary>
/// Generic key-value settings persisted to the database.
/// Used for theme preferences, app title, and other configurable options.
/// DB values override appsettings.json defaults.
/// </summary>
public class AppSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
