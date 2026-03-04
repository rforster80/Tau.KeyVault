using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tau.KeyVault.Models;
using Tau.KeyVault.Services;

namespace Tau.KeyVault.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new AppUser
            {
                Username = "admin",
                PasswordHash = HashPassword("admin")
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Ensures the encryption salt exists in appsettings.json and is backed up (encrypted) in the DB.
    /// Handles three scenarios:
    ///   1. Salt in config → ensure DB backup exists
    ///   2. No salt in config, but DB backup → recover salt from DB and write to config
    ///   3. Neither → generate new salt, store in both locations
    /// Idempotent — safe to call on every startup.
    /// Returns the salt value (needed by callers before IConfiguration reloads from disk).
    /// </summary>
    public static async Task<string> InitializeSaltAsync(AppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        var existingSalt = config[EncryptionService.SaltConfigKey];

        if (!string.IsNullOrEmpty(existingSalt))
        {
            // Salt already in appsettings.json — ensure encrypted backup exists in DB
            var dbBackup = await db.AppSettings
                .FirstOrDefaultAsync(s => s.Key == EncryptionService.SaltBackupKey);

            if (dbBackup == null)
            {
                db.AppSettings.Add(new AppSetting
                {
                    Key = EncryptionService.SaltBackupKey,
                    Value = EncryptionService.Encrypt(existingSalt),
                    UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            return existingSalt;
        }

        // No salt in appsettings.json — check DB for encrypted backup
        var dbEntry = await db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == EncryptionService.SaltBackupKey);

        string salt;

        if (dbEntry != null)
        {
            // Recover from encrypted DB backup
            salt = EncryptionService.Decrypt(dbEntry.Value);
        }
        else
        {
            // Brand new install — generate fresh salt and persist encrypted backup
            salt = EncryptionService.GenerateSalt();

            db.AppSettings.Add(new AppSetting
            {
                Key = EncryptionService.SaltBackupKey,
                Value = EncryptionService.Encrypt(salt),
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Write salt to appsettings.json on disk
        await WriteSaltToAppSettingsAsync(env.ContentRootPath, salt);

        return salt;
    }

    // ── Write Encryption:Salt into appsettings.json ──────────────────
    private static async Task WriteSaltToAppSettingsAsync(string contentRootPath, string salt)
    {
        var path = Path.Combine(contentRootPath, "appsettings.json");
        var json = await File.ReadAllTextAsync(path);

        using var doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "Encryption")
                    continue; // will be re-written below
                prop.WriteTo(writer);
            }

            writer.WriteStartObject("Encryption");
            writer.WriteString("Salt", salt);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        await File.WriteAllTextAsync(path, System.Text.Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>
    /// One-time migration: encrypts any existing plaintext values in the KeyEntries table.
    /// Values already encrypted (prefixed with "ENC:") are skipped.
    /// Idempotent — safe to call on every startup.
    /// </summary>
    public static async Task EncryptExistingKeysAsync(AppDbContext db, string saltBase64)
    {
        if (string.IsNullOrEmpty(saltBase64))
            return;

        // Load all entries and filter in memory to avoid LINQ translation issues
        var allEntries = await db.KeyEntries.ToListAsync();
        var unencrypted = allEntries
            .Where(k => !string.IsNullOrEmpty(k.Value) && !k.Value.StartsWith("ENC:", StringComparison.Ordinal))
            .ToList();

        if (unencrypted.Count == 0)
            return;

        foreach (var entry in unencrypted)
        {
            entry.Value = EncryptionService.EncryptValue(entry.Value, saltBase64);
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
