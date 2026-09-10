namespace Tau.KeyVault.Models;

/// <summary>
/// An API credential bound to exactly one environment.
/// <para>
/// A caller presenting one of these sees only that environment: no other environment's keys,
/// no Global fallback, and no environment or credential administration. Global credentials
/// remain the ones configured in <c>appsettings.json</c>.
/// </para>
/// <para>
/// The secret itself is never stored. <see cref="KeyHash"/> holds SHA-256 of the key, so a
/// leaked database yields no usable credential and a lost key must be rotated, not recovered.
/// </para>
/// </summary>
public class EnvironmentApiKey
{
    public int Id { get; set; }

    /// <summary>Unique, human-meaningful name. This is what the audit log records as the actor.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The single environment this credential is bound to, normalized to uppercase.
    /// Never blank: blank would mean Global, and Global credentials come from configuration.
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of the key. The key itself is shown once, at creation and rotation.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Disabled credentials are rejected without being deleted, so the audit trail keeps its subject.</summary>
    public bool Enabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the secret was last replaced. Null if never rotated.</summary>
    public DateTime? LastRotatedAt { get; set; }
}
