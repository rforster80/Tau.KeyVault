using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

/// <summary>
/// Thrown when a security-relevant event could not be recorded. The caller must abandon the
/// operation rather than perform it unaudited — see <c>AuditFailureMiddleware</c>, which turns
/// this into a 503.
/// </summary>
public class AuditWriteException : Exception
{
    public AuditWriteException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Writes the access audit trail.
/// <para>
/// Two properties matter, both from ADR-045:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Survives a rolled-back transaction.</b> Every row is written through a
/// <see cref="AppDbContext"/> this service constructs itself, on its own connection, and
/// committed immediately. It is never enlisted in the transaction of the operation it
/// describes, so rolling that operation back cannot erase the record of the attempt.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Synchronous and fail-closed.</b> There is no queue and no fire-and-forget. If the row
/// cannot be committed the caller gets <see cref="AuditWriteException"/> and the operation is
/// refused, because an unauditable access must not happen.
/// </description>
/// </item>
/// </list>
/// <para>Registered as a singleton: it holds provider options, not a context.</para>
/// </summary>
public class AuditService
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IConfiguration config, ILogger<AuditService> logger)
    {
        _options = DbProviderConfig.BuildStandaloneOptions(config);
        _logger = logger;
    }

    /// <summary>
    /// Records one access event. Throws <see cref="AuditWriteException"/> if it cannot be
    /// persisted — callers must not swallow that.
    /// </summary>
    public async Task WriteAsync(
        AuditAction action,
        AuditActorType actorType,
        string actorId,
        string key = "",
        string environment = "",
        AuditOutcome outcome = AuditOutcome.Success,
        string? ipAddress = null,
        int itemCount = 0,
        CancellationToken ct = default)
    {
        var entry = new AccessAuditLog
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            Key = key ?? string.Empty,
            Environment = environment ?? string.Empty,
            ActorType = actorType,
            ActorId = actorId ?? string.Empty,
            Outcome = outcome,
            IpAddress = ipAddress,
            ItemCount = itemCount
        };

        try
        {
            await using var db = new AppDbContext(_options);
            db.AccessAuditLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Surface loudly as well as throwing: if the store is gone, the operator needs to
            // know why the vault started refusing requests.
            _logger.LogError(ex,
                "Audit write failed for {Action} on key '{Key}' in '{Environment}' by {ActorType}:{ActorId}. Operation refused.",
                action, entry.Key, entry.Environment, actorType, entry.ActorId);

            throw new AuditWriteException(
                $"Could not record audit event '{action}'. The operation was refused because it cannot be audited.", ex);
        }
    }

    /// <summary>Records one event per key, for bulk erasure so each destroyed key has its own row.</summary>
    public async Task WriteManyAsync(
        AuditAction action,
        AuditActorType actorType,
        string actorId,
        IEnumerable<string> keys,
        string environment,
        AuditOutcome outcome = AuditOutcome.Success,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var rows = keys.Select(k => new AccessAuditLog
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            Key = k,
            Environment = environment ?? string.Empty,
            ActorType = actorType,
            ActorId = actorId ?? string.Empty,
            Outcome = outcome,
            IpAddress = ipAddress,
            ItemCount = 1
        }).ToList();

        if (rows.Count == 0) return;

        try
        {
            await using var db = new AppDbContext(_options);
            db.AccessAuditLogs.AddRange(rows);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Audit write failed for {Count} {Action} rows in '{Environment}'. Operation refused.",
                rows.Count, action, environment);

            throw new AuditWriteException(
                $"Could not record {rows.Count} audit event(s) '{action}'. The operation was refused because it cannot be audited.", ex);
        }
    }

    // ── Query side (backs GET /api/audit) ────────────────────────────

    public async Task<(List<AccessAuditLog> Rows, int TotalCount)> QueryAsync(
        string? key = null,
        string? environment = null,
        string? actorId = null,
        AuditAction? action = null,
        AuditOutcome? outcome = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        int offset = 0,
        CancellationToken ct = default)
    {
        await using var db = new AppDbContext(_options);

        var q = db.AccessAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(key)) q = q.Where(a => a.Key == key);
        if (environment is not null)
        {
            var env = KeyVaultService.NormalizeEnvironment(environment);
            q = q.Where(a => a.Environment == env);
        }
        if (!string.IsNullOrEmpty(actorId)) q = q.Where(a => a.ActorId == actorId);
        if (action is not null) q = q.Where(a => a.Action == action);
        if (outcome is not null) q = q.Where(a => a.Outcome == outcome);
        if (from is not null) q = q.Where(a => a.Timestamp >= from);
        if (to is not null) q = q.Where(a => a.Timestamp <= to);

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(ct);

        return (rows, total);
    }
}
