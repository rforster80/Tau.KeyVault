using System.Security.Cryptography;
using System.Text;

namespace Tau.KeyVault.Services;

// ═══════════════════════════════════════════════════════════════════════════
//  EncryptionService — Salt generation & AES-256 recovery encryption
// ═══════════════════════════════════════════════════════════════════════════
//
//  SALT RECOVERY PROCEDURE (if appsettings.json is lost or reset):
//  ─────────────────────────────────────────────────────────────────
//  1. Open the SQLite database file (keyvault.db) with any SQLite tool.
//  2. Run:  SELECT Value FROM AppSettings WHERE Key = 'Encryption:SaltBackup';
//  3. The returned value is an AES-256-CBC encrypted blob (Base64).
//     It is encrypted using the hardcoded RecoveryPassword in this class.
//  4. To decrypt, call:
//         EncryptionService.Decrypt(valueFromDb)
//     or build a small console app that references this project and calls
//     the same method. The decrypted result is the original Base64 salt.
//  5. Add the recovered salt back to appsettings.json:
//         "Encryption": { "Salt": "<recovered-base64-salt>" }
//  6. Restart the application — it will detect the salt in config and
//     continue normally.
//
//  Without the application source code (which contains RecoveryPassword),
//  the encrypted blob in the database is unrecoverable. Anyone who only
//  has the .db file will see an opaque Base64 string — no plaintext salt,
//  no key names, no recognisable structure.
// ═══════════════════════════════════════════════════════════════════════════

public static class EncryptionService
{
    // ── Hardcoded recovery password ──────────────────────────────────────
    // Used ONLY for encrypting/decrypting the salt backup stored in the DB.
    // This is intentionally embedded in source code so that anyone with the
    // codebase can recover a lost salt, but someone with only the DB cannot.
    private const string RecoveryPassword = "TauKV!S@ltR3covery#2026";

    // ── Well-known keys ──────────────────────────────────────────────────
    /// <summary>AppSettings DB key for the encrypted salt backup blob.</summary>
    public const string SaltBackupKey = "Encryption:SaltBackup";

    /// <summary>appsettings.json configuration path for the plaintext salt.</summary>
    public const string SaltConfigKey = "Encryption:Salt";

    // ── PBKDF2 parameters (matching DbSeeder conventions) ────────────────
    private const int Pbkdf2Iterations = 100_000;
    private const int Pbkdf2SaltBytes = 16;
    private const int AesKeyBytes = 32;   // 256-bit
    private const int AesIvBytes = 16;    // 128-bit CBC IV

    // ─────────────────────────────────────────────────────────────────────
    //  Salt generation
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a cryptographically random 32-byte salt and returns it as a Base64 string.
    /// </summary>
    public static string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(saltBytes);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  AES-256-CBC Encrypt / Decrypt (with PBKDF2 key derivation)
    // ─────────────────────────────────────────────────────────────────────
    //
    //  Encrypted format (all concatenated, then Base64-encoded):
    //    [16-byte PBKDF2 salt] [16-byte AES IV] [ciphertext...]
    //

    /// <summary>
    /// Encrypts a plaintext string using AES-256-CBC.
    /// The AES key is derived from <see cref="RecoveryPassword"/> via PBKDF2.
    /// Returns a Base64 string containing [pbkdf2Salt][iv][ciphertext].
    /// </summary>
    public static string Encrypt(string plaintext)
    {
        var pbkdf2Salt = RandomNumberGenerator.GetBytes(Pbkdf2SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            RecoveryPassword, pbkdf2Salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, AesKeyBytes);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Concatenate: [pbkdf2Salt][iv][ciphertext]
        var result = new byte[Pbkdf2SaltBytes + AesIvBytes + ciphertext.Length];
        Buffer.BlockCopy(pbkdf2Salt, 0, result, 0, Pbkdf2SaltBytes);
        Buffer.BlockCopy(aes.IV, 0, result, Pbkdf2SaltBytes, AesIvBytes);
        Buffer.BlockCopy(ciphertext, 0, result, Pbkdf2SaltBytes + AesIvBytes, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a value previously encrypted by <see cref="Encrypt"/>.
    /// Extracts the PBKDF2 salt and IV from the blob, re-derives the AES key,
    /// and decrypts the ciphertext.
    /// </summary>
    public static string Decrypt(string encrypted)
    {
        var blob = Convert.FromBase64String(encrypted);

        if (blob.Length < Pbkdf2SaltBytes + AesIvBytes + 1)
            throw new CryptographicException("Encrypted blob is too short to contain valid data.");

        var pbkdf2Salt = blob[..Pbkdf2SaltBytes];
        var iv = blob[Pbkdf2SaltBytes..(Pbkdf2SaltBytes + AesIvBytes)];
        var ciphertext = blob[(Pbkdf2SaltBytes + AesIvBytes)..];

        var key = Rfc2898DeriveBytes.Pbkdf2(
            RecoveryPassword, pbkdf2Salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, AesKeyBytes);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Value-level encryption (uses config salt as AES-256 key directly)
    // ─────────────────────────────────────────────────────────────────────
    //
    //  These methods encrypt/decrypt individual key-vault values at rest.
    //  The config salt (Encryption:Salt) is already 32 random bytes, so it
    //  is used directly as the AES key — no PBKDF2 derivation needed.
    //
    //  Stored format:  "ENC:" + Base64([16-byte IV][ciphertext])
    //  The "ENC:" prefix distinguishes encrypted values from legacy plaintext.
    //

    private const string EncPrefix = "ENC:";

    /// <summary>
    /// Encrypts a plaintext value using AES-256-CBC with the config salt as the key.
    /// Returns a string prefixed with "ENC:" followed by Base64([IV][ciphertext]).
    /// </summary>
    public static string EncryptValue(string plaintext, string saltBase64)
    {
        var key = Convert.FromBase64String(saltBase64);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Concatenate: [iv][ciphertext]
        var blob = new byte[AesIvBytes + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, blob, 0, AesIvBytes);
        Buffer.BlockCopy(ciphertext, 0, blob, AesIvBytes, ciphertext.Length);

        return EncPrefix + Convert.ToBase64String(blob);
    }

    /// <summary>
    /// Decrypts a stored value. If the value does not start with "ENC:" it is
    /// returned as-is (legacy plaintext that has not yet been encrypted).
    /// </summary>
    public static string DecryptValue(string stored, string saltBase64)
    {
        if (!stored.StartsWith(EncPrefix, StringComparison.Ordinal))
            return stored; // legacy plaintext — return unchanged

        var blob = Convert.FromBase64String(stored[EncPrefix.Length..]);

        if (blob.Length < AesIvBytes + 1)
            throw new CryptographicException("Encrypted value blob is too short.");

        var iv = blob[..AesIvBytes];
        var ciphertext = blob[AesIvBytes..];
        var key = Convert.FromBase64String(saltBase64);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
