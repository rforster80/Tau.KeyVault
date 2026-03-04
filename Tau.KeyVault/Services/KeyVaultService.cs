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
    private readonly string _encryptionSalt;

    public KeyVaultService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _encryptionSalt = config[EncryptionService.SaltConfigKey]
            ?? throw new InvalidOperationException("Encryption:Salt is not configured in appsettings.json.");
    }

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

    public async Task<List<string>> GetEnvironmentsAsync()
    {
        return await _db.KeyEntries
            .Select(k => k.Environment)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();
    }

    /// <summary>
    /// Normalizes an environment name to uppercase. Global (empty) stays empty.
    /// </summary>
    private static string NormalizeEnvironment(string environment) =>
        string.IsNullOrEmpty(environment) ? "" : environment.Trim().ToUpperInvariant();

    public async Task<List<KeyEntry>> GetKeysAsync(string? environment = null)
    {
        var query = _db.KeyEntries.AsNoTracking().AsQueryable();
        if (environment is not null)
        {
            var env = NormalizeEnvironment(environment);
            query = query.Where(k => k.Environment == env);
        }
        var results = await query.OrderBy(k => k.Key).ToListAsync();
        DecryptEntries(results);
        return results;
    }

    public async Task<KeyEntry?> GetKeyAsync(string key, string environment)
    {
        var env = NormalizeEnvironment(environment);
        var entry = await _db.KeyEntries.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.Environment == env);
        if (entry is not null) DecryptEntry(entry);
        return entry;
    }

    public async Task<KeyEntry> UpsertKeyAsync(string key, string value, string environment,
        DataType dataType = DataType.Text, bool isSensitive = false)
    {
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

        await _db.SaveChangesAsync();

        // Detach so the decrypted value doesn't get flushed back to DB by a later SaveChanges
        _db.Entry(entry).State = EntityState.Detached;
        entry.Value = processedValue;
        return entry;
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
        _db.KeyEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Resolves a key by environment with global fallback.
    /// </summary>
    public async Task<KeyEntry?> ResolveKeyAsync(string key, string environment)
    {
        var env = NormalizeEnvironment(environment);
        var entry = await _db.KeyEntries.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.Environment == env);

        if (entry is null && !string.IsNullOrEmpty(env))
        {
            entry = await _db.KeyEntries.AsNoTracking()
                .FirstOrDefaultAsync(k => k.Key == key && k.Environment == "");
        }

        if (entry is not null) DecryptEntry(entry);
        return entry;
    }

    /// <summary>
    /// Resolves all keys for an environment with global fallback.
    /// </summary>
    public async Task<List<KeyEntry>> ResolveAllKeysAsync(string environment)
    {
        var env = NormalizeEnvironment(environment);
        if (string.IsNullOrEmpty(env))
            return await GetKeysAsync("");

        var envKeys = await _db.KeyEntries.AsNoTracking()
            .Where(k => k.Environment == env)
            .ToListAsync();

        var envKeyNames = envKeys.Select(k => k.Key).ToHashSet();

        var globalFallbacks = await _db.KeyEntries.AsNoTracking()
            .Where(k => k.Environment == "" && !envKeyNames.Contains(k.Key))
            .ToListAsync();

        var results = envKeys.Concat(globalFallbacks).OrderBy(k => k.Key).ToList();
        DecryptEntries(results);
        return results;
    }

    /// <summary>
    /// Deletes an environment and ALL its key-value pairs.
    /// </summary>
    public async Task<int> DeleteEnvironmentAsync(string environment)
    {
        var env = NormalizeEnvironment(environment);
        if (string.IsNullOrEmpty(env))
            throw new InvalidOperationException("Cannot delete the global environment.");

        var entries = await _db.KeyEntries
            .Where(k => k.Environment == env)
            .ToListAsync();

        _db.KeyEntries.RemoveRange(entries);
        await _db.SaveChangesAsync();
        return entries.Count;
    }

    /// <summary>
    /// Renames an environment, updating all associated key-value pairs.
    /// </summary>
    public async Task<int> RenameEnvironmentAsync(string oldName, string newName)
    {
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

        foreach (var entry in entries)
        {
            entry.Environment = newEnv;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return entries.Count;
    }

    // ───────────────────────────────────────────────
    //  Export / Import
    // ───────────────────────────────────────────────

    /// <summary>
    /// Exports all keys for an environment as a portable payload.
    /// </summary>
    public async Task<ExportPayload> ExportEnvironmentAsync(string environment)
    {
        var env = NormalizeEnvironment(environment);
        var keys = await _db.KeyEntries.AsNoTracking()
            .Where(k => k.Environment == env)
            .OrderBy(k => k.Key)
            .ToListAsync();

        DecryptEntries(keys);

        var items = keys.Select(k => new ExportKeyItem(
            k.Key, k.Value, k.DataType.ToString(), k.IsSensitive
        )).ToList();

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
        var env = NormalizeEnvironment(environment);
        int imported = 0, skipped = 0;

        // Delete All mode: remove existing keys first
        if (mode == ImportMode.DeleteAll)
        {
            var existing = await _db.KeyEntries
                .Where(k => k.Environment == env)
                .ToListAsync();
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

            await UpsertKeyAsync(item.Key, item.Value, env, dataType, item.IsSensitive);
            imported++;
        }

        return (imported, skipped);
    }
}
