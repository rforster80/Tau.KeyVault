using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Data;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

/// <summary>A newly minted credential. The plaintext exists only in this object, once.</summary>
public readonly record struct MintedApiKey(EnvironmentApiKey Record, string PlaintextKey);

/// <summary>
/// Manages per-environment API credentials: minting, rotation, revocation and lookup.
/// <para>
/// The whole feature is gated on <see cref="EnableConfigKey"/>, default false. While it is
/// off, environment credentials do not authenticate and cannot be administered, so the vault
/// behaves exactly as it did with a single shared key.
/// </para>
/// </summary>
public class ApiKeyService
{
    /// <summary>appsettings.json flag enabling per-environment credentials. Default false.</summary>
    public const string EnableConfigKey = "EnableAPIKeyPerEnvironment";

    /// <summary>Prefix on generated keys, so they are recognisable to secret scanners.</summary>
    private const string KeyPrefix = "kv_";

    private readonly AppDbContext _db;
    private readonly bool _enabled;

    public ApiKeyService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _enabled = config.GetValue<bool?>(EnableConfigKey) ?? false;
    }

    public bool Enabled => _enabled;

    // ── Hashing ──────────────────────────────────────────────────────
    //  API keys are high-entropy random values, not user-chosen passwords, so a plain
    //  SHA-256 is appropriate: it is deterministic (allowing an indexed lookup on the
    //  request hot path) and there is no dictionary to attack.

    public static string HashKey(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    private static string GenerateKey() =>
        KeyPrefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

    // ── Lookup (authentication path) ─────────────────────────────────

    /// <summary>
    /// Resolves a presented key to its environment binding, or null when it matches no
    /// enabled credential. Always returns null while the feature is disabled.
    /// </summary>
    public async Task<EnvironmentApiKey?> ResolveAsync(string presentedKey, CancellationToken ct = default)
    {
        if (!_enabled || string.IsNullOrEmpty(presentedKey))
            return null;

        var hash = HashKey(presentedKey);
        return await _db.EnvironmentApiKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.Enabled, ct);
    }

    // ── Administration (Global callers only; enforced at the controller) ──

    public async Task<List<EnvironmentApiKey>> ListAsync(CancellationToken ct = default) =>
        await _db.EnvironmentApiKeys.AsNoTracking()
            .OrderBy(k => k.Environment).ThenBy(k => k.Name)
            .ToListAsync(ct);

    public async Task<EnvironmentApiKey?> GetAsync(int id, CancellationToken ct = default) =>
        await _db.EnvironmentApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct);

    /// <summary>
    /// Mints a credential bound to one environment. The environment must be a real, named
    /// one: binding to Global is meaningless because Global credentials are configured.
    /// </summary>
    public async Task<MintedApiKey> CreateAsync(string name, string environment, CancellationToken ct = default)
    {
        var env = KeyVaultService.NormalizeEnvironment(environment);

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("A credential name is required.");
        if (string.IsNullOrEmpty(env))
            throw new InvalidOperationException(
                "An API key must be bound to a named environment. Global access uses the keys configured in appsettings.json.");
        if (await _db.EnvironmentApiKeys.AnyAsync(k => k.Name == name, ct))
            throw new InvalidOperationException($"An API key named '{name}' already exists.");

        var plaintext = GenerateKey();
        var record = new EnvironmentApiKey
        {
            Name = name.Trim(),
            Environment = env,
            KeyHash = HashKey(plaintext),
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.EnvironmentApiKeys.Add(record);
        await _db.SaveChangesAsync(ct);

        return new MintedApiKey(record, plaintext);
    }

    /// <summary>Replaces the secret, keeping the name and environment binding. Old key stops working immediately.</summary>
    public async Task<MintedApiKey?> RotateAsync(int id, CancellationToken ct = default)
    {
        var record = await _db.EnvironmentApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (record is null) return null;

        var plaintext = GenerateKey();
        record.KeyHash = HashKey(plaintext);
        record.LastRotatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new MintedApiKey(record, plaintext);
    }

    public async Task<bool> RevokeAsync(int id, CancellationToken ct = default)
    {
        var record = await _db.EnvironmentApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (record is null) return false;

        _db.EnvironmentApiKeys.Remove(record);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EnvironmentApiKey?> SetEnabledAsync(int id, bool enabled, CancellationToken ct = default)
    {
        var record = await _db.EnvironmentApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (record is null) return null;

        record.Enabled = enabled;
        await _db.SaveChangesAsync(ct);
        return record;
    }
}
