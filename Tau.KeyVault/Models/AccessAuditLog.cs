namespace Tau.KeyVault.Models;

/// <summary>What was attempted against the vault.</summary>
public enum AuditAction
{
    ReadKey = 0,
    ListKeys = 1,
    WriteKey = 2,
    DeleteKey = 3,
    ListEnvironments = 4,
    DeleteEnvironment = 5,
    RenameEnvironment = 6,
    Export = 7,
    Import = 8,
    /// <summary>Someone queried the audit log itself.</summary>
    ReadAudit = 9,
    /// <summary>A request presented a missing or invalid API key.</summary>
    AuthFailure = 10,
    /// <summary>A valid credential was used outside the environment it is bound to.</summary>
    ScopeViolation = 11,
    ListApiKeys = 12,
    CreateApiKey = 13,
    RotateApiKey = 14,
    RevokeApiKey = 15,
    UpdateApiKey = 16
}

public enum AuditActorType
{
    /// <summary>No identifiable actor (rejected before authentication).</summary>
    Anonymous = 0,
    /// <summary>REST API caller, identified by its configured API key name.</summary>
    ApiKey = 1,
    /// <summary>Signed-in admin UI user.</summary>
    User = 2,
    /// <summary>Vault itself (startup migration, seeding).</summary>
    System = 3
}

public enum AuditOutcome
{
    Success = 0,
    NotFound = 1,
    Denied = 2,
    Error = 3
}

/// <summary>
/// A durable record of who read, wrote or deleted which key, in which environment, and when.
/// <para>
/// This entity deliberately has no value column and never will: the audit trail records
/// <em>access to</em> a key, never its contents. Do not add one.
/// </para>
/// <para>
/// Rows are written by <c>AuditService</c> on a connection of their own so they survive a
/// rollback of the operation they describe (ADR-045), and a failed audit write refuses the
/// operation rather than letting it proceed unrecorded.
/// </para>
/// </summary>
public class AccessAuditLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public AuditAction Action { get; set; }

    /// <summary>Key name. Blank for collection-level actions (list, export, import, environment ops).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Environment the action targeted; blank means Global.</summary>
    public string Environment { get; set; } = string.Empty;

    public AuditActorType ActorType { get; set; }

    /// <summary>
    /// Configured API key <em>name</em>, admin username, or <c>#n</c> for an unnamed API key.
    /// Never the API key itself.
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    public AuditOutcome Outcome { get; set; }

    /// <summary>Caller IP where known. Null for admin UI actions on an established Blazor circuit.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Number of keys affected by a collection-level action; 1 (or 0) for single-key actions.</summary>
    public int ItemCount { get; set; }
}
