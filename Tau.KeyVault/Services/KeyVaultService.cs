using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

// ── Export/Import DTOs ──────────────────────────────────────
public record ExportPayload(string Version, DateTime ExportDate, string Environment, int KeyCount, List<ExportKeyItem> Keys);
public record ExportKeyItem(string Key, string Value, string DataType, bool IsSensitive);

public enum ImportMode
{
    DeleteAll = 0,
    Overwrite = 1,
    AddMissing = 2
}

public class KeyVaultService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly AuditActorAccessor _actor;
    private readonly CallerScopeAccessor _scopes;
    private readonly string _encryptionSalt;
    private readonly bool _globalKeyFailover;

    /// <summary>appsettings.json key controlling global fallback on key resolution.</summary>
    public const string GlobalKeyFailoverConfigKey = "GlobalKeyFailover";

    public KeyVaultService(AppDbContext db, IConfiguration config, AuditService audit,
        AuditActorAccessor actor, CallerScopeAccessor scopes)
    {
        _db = db;
        _audit = audit;
        _actor = actor;
        _scopes = scopes;
        _encryptionSalt = config[EncryptionService.SaltConfigKey]
            ?? throw new InvalidOperationException("Encryption:Salt is not configured in appsettings.json.");

        // Defaults to true (historic behaviour) when the key is absent or unparseable.
        _globalKeyFailover = config.GetValue<bool?>(GlobalKeyFailoverConfigKey) ?? true;
    }

    /// <summary>
    /// Whether an environment-scoped lookup that misses falls back to the Global
    /// (blank environment) value. Configured by <see cref="GlobalKeyFailoverConfigKey"/>;
    /// true unless explicitly disabled.
    /// </summary>
    public bool GlobalKeyFailover => _globalKeyFailover;

    // ── Transparent encryption helpers ───────────────────────────────
    private void DecryptEntry(KeyEntry entry)
    {
        if (entry?.Value is not null)
            entry.Value = EncryptionService.DecryptValue(entry.Value, _encryptionSalt);
    }

    private void DecryptEntries(List<KeyEntry> entries)
    {
        foreach (var entry in entries)
            DecryptEntry(entry);
    }

    private string EncryptValue(string processedValue) =>
        EncryptionService.EncryptValue(processedValue, _encryptionSalt);

    // ── Audit helpers ────────────────────────────────────────────────
    //  Auditing lives here, at the data-access boundary, so that neither the REST API nor the
    //  Blazor admin UI can touch a key without leaving a record. Every one of these awaits the
    //  write and lets AuditWriteException escape: fail-closed by design (ADR-045).

    private async Task AuditAsync(AuditAction action, string key, string environment,
        AuditOutcome outcome = AuditOutcome.Success, int itemCount = 0)
    {
        var actor = await _actor.GetAsync();
        await _audit.WriteAsync(action, actor.Type, actor.Id, key, environment,
            outcome, actor.IpAddress, itemCount);
    }

    private async Task AuditManyAsync(AuditAction action, IEnumerable<string> keys, string environment)
    {
        var actor = await _actor.GetAsync();
        await _audit.WriteManyAsync(action, actor.Type, actor.Id, keys, environment,
            AuditOutcome.Success, actor.IpAddress);
    }

    // ── Scope enforcement ────────────────────────────────────────────
    //  Enforced here rather than in the controllers so that no caller path — REST or the
    //  Blazor admin UI — can reach an environment its credential is not bound to. The admin
    //  UI resolves as Global, so its behaviour is unchanged.

    /// <summary>Refuses the operation unless the caller's credential can reach this environment.</summary>
    private void RequireReach(string environment)
    {
        var scope = _scopes.Current;
        if (scope.CanReach(environment))
            return;

        var attempted = NormalizeEnvironment(environment);
        throw new ScopeViolationException(
            $"API key '{scope.Name}' is bound to environment '{scope.Environment}' and cannot access " +
            (string.IsNullOrEmpty(attempted) ? "the Global environment." : $"environment '{attempted}'."),
            attempted);
    }

    /// <summary>Refuses an administrative action reserved for Global callers.</summary>
    private void RequireGlobal(string action)
    {
        var scope = _scopes.Current;
        if (scope.IsGlobal)
            return;

        throw new ScopeViolationException(
            $"API key '{scope.Name}' is bound to environment '{scope.Environment}' and may not {action}. " +
            "Use a Global API key configured in appsettings.json.",
            scope.Environment);
    }

    /// <summary>
    /// Global fallback is never available to an environment-bound caller, whatever
    /// GlobalKeyFailover says: such a caller must not be able to observe Global values.
    /// </summary>
    public bool FailoverAllowed => _globalKeyFailover && _scopes.Current.IsGlobal;

    /// <summary>
    /// Records a mutation <em>before</em> applying it, so a failed audit write refuses the change
    /// instead of leaving it committed and unrecorded.
    /// <para>
    /// Ordering matters and cannot be avoided. The audit row is committed on its own connection
    /// (that is what makes it survive a rollback of this operation, per ADR-045), so it cannot
    /// share this operation's transaction. Auditing afterwards would mean a committed change with
    /// no record whenever the audit store is unavailable — the precise gap fail-closed exists to
    /// shut. Auditing first instead means a recorded attempt that may not have landed, which the
    /// follow-up Error row below makes visible.
    /// </para>
    /// </summary>
    private async Task<T> AuditedMutationAsync<T>(
        AuditAction action, string key, string environment, int itemCount, Func<Task<T>> mutate)
    {
        await AuditAsync(action, key, environment, AuditOutcome.Success, itemCount);

        try
        {
            return await mutate();
        }
        catch
        {
            // Best-effort correction so the trail does not claim a change that never landed.
            try { await AuditAsync(action, key, environment, AuditOutcome.Error, itemCount); }
            catch { /* keep the original failure, not the bookkeeping one */ }
            throw;
        }
    }

    public async Task<List<string>> GetEnvironmentsAsync()
    {
        var scope = _scopes.Current;

        var envs = await _db.KeyEntries
            .Select(k => k.Environment)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        // A bound caller must not learn which other environments exist.
        if (!scope.IsGlobal)
            envs = envs.Where(e => e == scope.Environment).ToList();

        await AuditAsync(AuditAction.ListEnvironments, "", scope.DefaultEnvironment, itemCount: envs.Count);
        return envs;
    }

    /// <summary>
    /// Normalizes an environment name to uppercase. Global (empty) stays empty.
    /// </summary>
    public static string NormalizeEnvironment(string environment) =>
        string.IsNullOrEmpty(environment) ? "" : environment.Trim().ToUpperInvariant();

    public async Task<List<KeyEntry>> GetKeysAsync(string? environment = null)
    {
        // environment == null means "every environment", which only Global may ask for.
        if (environment is null)
            RequireGlobal("list keys across all environments");
        else
            RequireReach(environment);

        var query = _db.KeyEntries.AsNoTracking().AsQueryable();
        if (environment is not null)
        {
            var env = NormalizeEnvironment(environment);
            query = query.Where(k => k.Environment == env);
        }
        var results = await query.OrderBy(k => k.Key).ToListAsync();
        DecryptEntries(results);

        await AuditAsync(AuditAction.ListKeys, "", environment ?? "", itemCount: results.Count);
        return results;
    }

    public async Task<KeyEntry?> GetKeyAsync(string key, string environment)
    {
        RequireReach(environment);
        var env = NormalizeEnvironment(environment);
        var entry = await _db.KeyEntries.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.Environment == env);

        await AuditAsync(AuditAction.ReadKey, key, env,
            entry is null ? AuditOutcome.NotFound : AuditOutcome.Success,
            entry is null ? 0 : 1);

        if (entry is not null) DecryptEntry(entry);
        return entry;
    }

    public async Task<KeyEntry> UpsertKeyAsync(string key, string value, string environment,
        DataType dataType = DataType.Text, bool isSensitive = false)
    {
        RequireReach(environment);
        var env = NormalizeEnvironment(environment);
        var entry = await _db.KeyEntries
            .FirstOrDefaultAsync(k => k.Key == key && k.Environment == env);

        // Apply type-specific transformations, then encrypt before storing
        var processedValue = ProcessValueByType(value, dataType);
        var encryptedValue = EncryptValue(processedValue);

        if (entry is null)
        {
            entry = new KeyEntry
            {
                Key = key,
                Value = encryptedValue,
                Environment = env,
                DataType = dataType,
                IsSensitive = isSensitive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.KeyEntries.Add(entry);
        }
        else
        {
            entry.Value = encryptedValue;
            entry.DataType = dataType;
            entry.IsSensitive = isSensitive;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        return await AuditedMutationAsync(AuditAction.WriteKey, key, env, 1, async () =>
        {
            await _db.SaveChangesAsync();

            // Detach so the decrypted value doesn't get flushed back to DB by a later SaveChanges
            _db.Entry(entry).State = EntityState.Detached;
            entry.Value = processedValue;
            return entry;
        });
    }

    /// <summary>
    /// Applies type-specific transformations to the value before storage.
    /// </summary>
    private static string ProcessValueByType(string value, DataType dataType) => dataType switch
    {
        DataType.Code => value.ToUpperInvariant(),
        DataType.Numeric => ValidateNumeric(value),
        DataType.Boolean => NormalizeBoolean(value),
        DataType.Csv => NormalizeCsv(value),
        _ => value // Text, Date, Time, DateTime, Json stored as-is
    };

    private static string ValidateNumeric(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            throw new InvalidOperationException($"Invalid numeric value: '{value}'");
        return value;
    }

    private static string NormalizeBoolean(string value)
    {
        var lower = value?.Trim().ToLowerInvariant() ?? "";
        return lower is "true" or "1" or "yes" ? "true" : "false";
    }

    /// <summary>
    /// Normalizes CSV: trims items, removes empties, removes duplicates (case-insensitive).
    /// </summary>
    private static string NormalizeCsv(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = value.Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s) && seen.Add(s))
            .ToList();

        return string.Join(",", items);
    }

    public async Task<bool> DeleteKeyAsync(int id)
    {
        var entry = await _db.KeyEntries.FindAsync(id);
        if (entry is null) return false;

        // Capture identity before the row goes away, so the audit row can name it.
        var (key, env) = (entry.Key, entry.Environment);
        RequireReach(env);

        return await AuditedMutationAsync(AuditAction.DeleteKey, key, env, 1, async () =>
        {
            _db.KeyEntries.Remove(entry);
            await _db.SaveChangesAsync();
            return true;
        });
    }

    /// <summary>
    /// Deletes a single key from one specific environment.
    /// Deliberately does NOT apply global fallback: a key that exists only in the
    /// global environment is left alone when a named environment is requested, so
    /// deleting from one environment can never remove another environment's value.
    /// Returns false when no row matches.
    /// </summary>
    public async Task<bool> DeleteKeyAsync(string key, string environment)
    {
        RequireReach(environment);
        var env = NormalizeEnvironment(environment);
        var entry = await _db.KeyEntries
            .FirstOrDefaultAsync(k => k.Key == key && k.Environment == env);

        if (entry is null)
        {
            await AuditAsync(AuditAction.DeleteKey, key, env, AuditOutcome.NotFound);
            return false;
        }

        return await AuditedMutationAsync(AuditAction.DeleteKey, key, env, 1, async () =>
        {
            _db.KeyEntries.Remove(entry);
            await _db.SaveChangesAsync();
            return true;
        });
    }

    /// <summary>
    /// Resolves a key by environment, falling back to Global only when
    /// GlobalKeyFailover is enabled (the default).
    /// </summary>
    public async Task<KeyEntry?> ResolveKeyAsync(string key, string environment)
    {
        RequireReach(environment);
        var env = NormalizeEnvironment(environment);
        var entry = await _db.KeyEntries.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.Environment == env);

        if (entry is null && FailoverAllowed && !string.IsNullOrEmpty(env))
        {
            entry = await _db.KeyEntries.AsNoTracking()
                .FirstOrDefaultAsync(k => k.Key == key && k.Environment == "");
        }

        await AuditAsync(AuditAction.ReadKey, key, env,
            entry is null ? AuditOutcome.NotFound : AuditOutcome.Success,
            entry is null ? 0 : 1);

        if (entry is not null) DecryptEntry(entry);
        return entry;
    }

    /// <summary>
    /// Resolves all keys for an environment, merging in Global values for keys the
    /// environment does not define — only when GlobalKeyFailover is enabled (the default).
    /// </summary>
    public async Task<List<KeyEntry>> ResolveAllKeysAsync(string environment)
    {
        RequireReach(environment);
        var env = NormalizeEnvironment(environment);
        if (string.IsNullOrEmpty(env))
            return await GetKeysAsync("");

        if (!FailoverAllowed)
            return await GetKeysAsync(env);

        var envKeys = await _db.KeyEntries.AsNoTracking()
            .Where(k => k.Environment == env)
            .ToListAsync();

        var envKeyNames = envKeys.Select(k => k.Key).ToHashSet();

        var globalFallbacks = await _db.KeyEntries.AsNoTracking()
            .Where(k => k.Environment == "" && !envKeyNames.Contains(k.Key))
            .ToListAsync();

        var results = envKeys.Concat(globalFallbacks).OrderBy(k => k.Key).ToList();
        DecryptEntries(results);

        await AuditAsync(AuditAction.ListKeys, "", env, itemCount: results.Count);
        return results;
    }

    /// <summary>
    /// Deletes an environment and ALL its key-value pairs.
    /// </summary>
    public async Task<int> DeleteEnvironmentAsync(string environment)
    {
        RequireGlobal("delete an environment");
        var env = NormalizeEnvironment(environment);
        if (string.IsNullOrEmpty(env))
            throw new InvalidOperationException("Cannot delete the global environment.");

        var entries = await _db.KeyEntries
            .Where(k => k.Environment == env)
            .ToListAsync();

        var destroyedKeys = entries.Select(e => e.Key).ToList();

        // One row per key: erasure evidence has to hold even when a subject's key was
        // destroyed by dropping the whole environment.
        await AuditManyAsync(AuditAction.DeleteKey, destroyedKeys, env);

        return await AuditedMutationAsync(AuditAction.DeleteEnvironment, "", env, entries.Count, async () =>
        {
            _db.KeyEntries.RemoveRange(entries);
            await _db.SaveChangesAsync();
            return entries.Count;
        });
    }

    /// <summary>
    /// Renames an environment, updating all associated key-value pairs.
    /// </summary>
    public async Task<int> RenameEnvironmentAsync(string oldName, string newName)
    {
        RequireGlobal("rename an environment");
        var oldEnv = NormalizeEnvironment(oldName);
        var newEnv = NormalizeEnvironment(newName);

        if (string.IsNullOrEmpty(oldEnv))
            throw new InvalidOperationException("Cannot rename the global environment.");
        if (string.IsNullOrEmpty(newEnv))
            throw new InvalidOperationException("Cannot rename to an empty environment name (that is the global environment).");
        if (oldEnv == newEnv)
            return 0;

        var oldKeys = await _db.KeyEntries
            .Where(k => k.Environment == oldEnv)
            .Select(k => k.Key)
            .ToListAsync();

        var conflicting = await _db.KeyEntries
            .Where(k => k.Environment == newEnv && oldKeys.Contains(k.Key))
            .Select(k => k.Key)
            .ToListAsync();

        if (conflicting.Count > 0)
            throw new InvalidOperationException(
                $"Cannot rename: the following keys already exist in '{newEnv}': {string.Join(", ", conflicting)}");

        var entries = await _db.KeyEntries
            .Where(k => k.Environment == oldEnv)
            .ToListAsync();

        return await AuditedMutationAsync(AuditAction.RenameEnvironment, "", newEnv, entries.Count, async () =>
        {
            foreach (var entry in entries)
            {
                entry.Environment = newEnv;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return entries.Count;
        });
    }

    // ───────────────────────────────────────────────
    //  Export / Import
    // ───────────────────────────────────────────────

    /// <summary>
    /// Exports all keys for an environment as a portable payload.
    /// </summary>
    public async Task<ExportPayload> ExportEnvironmentAsync(string environment)
    {
        RequireReach(environment);
        var env = NormalizeEnvironment(environment);
        var keys = await _db.KeyEntries.AsNoTracking()
            .Where(k => k.Environment == env)
            .OrderBy(k => k.Key)
            .ToListAsync();

        DecryptEntries(keys);

        var items = keys.Select(k => new ExportKeyItem(
            k.Key, k.Value, k.DataType.ToString(), k.IsSensitive
        )).ToList();

        // An export discloses every value in the environment, so record a read per key
        // as well as the export itself.
        await AuditManyAsync(AuditAction.ReadKey, items.Select(i => i.Key), env);
        await AuditAsync(AuditAction.Export, "", env, itemCount: items.Count);

        return new ExportPayload(
            Version: "1.0",
            ExportDate: DateTime.UtcNow,
            Environment: env,
            KeyCount: items.Count,
            Keys: items
        );
    }

    /// <summary>
    /// Imports keys into an environment using the specified mode.
    /// Returns (imported, skipped) counts.
    /// </summary>
    public async Task<(int Imported, int Skipped)> ImportKeysAsync(
        string environment, List<ExportKeyItem> keys, ImportMode mode)
    {
        RequireReach(environment);
        var env = NormalizeEnvironment(environment);
        int imported = 0, skipped = 0;

        // Delete All mode: remove existing keys first
        if (mode == ImportMode.DeleteAll)
        {
            var existing = await _db.KeyEntries
                .Where(k => k.Environment == env)
                .ToListAsync();
            var purgedKeys = existing.Select(e => e.Key).ToList();

            // Clean Import destroys keys; each one needs its own erasure record, written
            // before the purge so an unrecordable purge does not happen.
            await AuditManyAsync(AuditAction.DeleteKey, purgedKeys, env);

            _db.KeyEntries.RemoveRange(existing);
            await _db.SaveChangesAsync();
        }

        // Load existing key names for AddMissing check
        HashSet<string>? existingKeys = null;
        if (mode == ImportMode.AddMissing)
        {
            existingKeys = (await _db.KeyEntries
                .Where(k => k.Environment == env)
                .Select(k => k.Key)
                .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var item in keys)
        {
            // Parse DataType
            if (!Enum.TryParse<DataType>(item.DataType, ignoreCase: true, out var dataType))
                dataType = DataType.Text;

            // AddMissing: skip if key already exists
            if (mode == ImportMode.AddMissing && existingKeys!.Contains(item.Key))
            {
                skipped++;
                continue;
            }

            // UpsertKeyAsync writes its own WriteKey row per key.
            await UpsertKeyAsync(item.Key, item.Value, env, dataType, item.IsSensitive);
            imported++;
        }

        await AuditAsync(AuditAction.Import, "", env, itemCount: imported);
        return (imported, skipped);
    }
}
