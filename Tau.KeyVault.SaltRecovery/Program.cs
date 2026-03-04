// ═══════════════════════════════════════════════════════════════════════════
//  Tau Key Vault — Salt Recovery Tool
// ═══════════════════════════════════════════════════════════════════════════
//
//  Usage:
//    dotnet run -- --db <path-to-keyvault.db>
//    dotnet run -- --hash <base64-encrypted-blob>
//
//  This tool recovers the encryption salt used by Tau Key Vault.
//  The salt is stored encrypted in the DB using AES-256-CBC with a
//  PBKDF2-derived key. This tool contains the same hardcoded recovery
//  password as the main application to perform decryption.
//
//  Once recovered, add the salt back to appsettings.json:
//    "Encryption": { "Salt": "<recovered-value>" }
//  Then restart the application.
// ═══════════════════════════════════════════════════════════════════════════

using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

// ── Parse arguments ──────────────────────────────────────────────────────

if (args.Length < 2 || (args[0] != "--db" && args[0] != "--hash"))
{
    PrintUsage();
    return 1;
}

var mode = args[0];
var value = args[1];

try
{
    string encryptedBlob;

    if (mode == "--db")
    {
        encryptedBlob = ExtractFromDatabase(value);
    }
    else
    {
        encryptedBlob = value;
    }

    var recoveredSalt = Decrypt(encryptedBlob);

    Console.WriteLine();
    Console.WriteLine("  Salt recovered successfully!");
    Console.WriteLine();
    Console.WriteLine($"  Salt (Base64): {recoveredSalt}");
    Console.WriteLine();
    Console.WriteLine("  Add the following to your appsettings.json and restart the application:");
    Console.WriteLine();
    Console.WriteLine("  \"Encryption\": {");
    Console.WriteLine($"    \"Salt\": \"{recoveredSalt}\"");
    Console.WriteLine("  }");
    Console.WriteLine();

    return 0;
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"  Error: {ex.Message}");
    return 1;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"  Error: {ex.Message}");
    return 1;
}
catch (CryptographicException ex)
{
    Console.Error.WriteLine($"  Error: Decryption failed — {ex.Message}");
    Console.Error.WriteLine("  The encrypted blob may be corrupted or from a different application version.");
    return 1;
}
catch (FormatException)
{
    Console.Error.WriteLine("  Error: The provided hash is not valid Base64.");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"  Unexpected error: {ex.Message}");
    return 1;
}

// ── Database extraction ──────────────────────────────────────────────────

static string ExtractFromDatabase(string dbPath)
{
    if (!File.Exists(dbPath))
        throw new FileNotFoundException($"Database file not found: {dbPath}");

    var connectionString = $"Data Source={dbPath};Mode=ReadOnly";
    using var connection = new SqliteConnection(connectionString);
    connection.Open();

    using var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = 'Encryption:SaltBackup'";

    var result = cmd.ExecuteScalar();

    if (result is null or DBNull)
        throw new InvalidOperationException(
            "No 'Encryption:SaltBackup' entry found in the AppSettings table. " +
            "The database may not contain a salt backup.");

    return (string)result;
}

// ── AES-256-CBC decryption (mirrors EncryptionService.Decrypt) ───────────
//
//  Blob format: [16-byte PBKDF2 salt][16-byte AES IV][ciphertext...]
//  All Base64-encoded as a single string.

static string Decrypt(string encrypted)
{
    const string recoveryPassword = "TauKV!S@ltR3covery#2026";
    const int pbkdf2Iterations = 100_000;
    const int pbkdf2SaltBytes = 16;
    const int aesKeyBytes = 32;
    const int aesIvBytes = 16;

    var blob = Convert.FromBase64String(encrypted);

    if (blob.Length < pbkdf2SaltBytes + aesIvBytes + 1)
        throw new CryptographicException("Encrypted blob is too short to contain valid data.");

    var pbkdf2Salt = blob[..pbkdf2SaltBytes];
    var iv = blob[pbkdf2SaltBytes..(pbkdf2SaltBytes + aesIvBytes)];
    var ciphertext = blob[(pbkdf2SaltBytes + aesIvBytes)..];

    var key = Rfc2898DeriveBytes.Pbkdf2(
        recoveryPassword, pbkdf2Salt, pbkdf2Iterations, HashAlgorithmName.SHA256, aesKeyBytes);

    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV = iv;
    aes.Mode = CipherMode.CBC;
    aes.Padding = PaddingMode.PKCS7;

    using var decryptor = aes.CreateDecryptor();
    var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

    return Encoding.UTF8.GetString(plaintextBytes);
}

// ── Usage help ───────────────────────────────────────────────────────────

static void PrintUsage()
{
    Console.WriteLine();
    Console.WriteLine("  Tau Key Vault — Salt Recovery Tool");
    Console.WriteLine("  ──────────────────────────────────");
    Console.WriteLine();
    Console.WriteLine("  Usage:");
    Console.WriteLine("    dotnet run -- --db <path-to-keyvault.db>");
    Console.WriteLine("    dotnet run -- --hash <base64-encrypted-blob>");
    Console.WriteLine();
    Console.WriteLine("  Options:");
    Console.WriteLine("    --db <path>    Path to the SQLite database file (keyvault.db).");
    Console.WriteLine("                   Extracts the encrypted salt backup automatically.");
    Console.WriteLine();
    Console.WriteLine("    --hash <blob>  The Base64 encrypted blob copied directly from the");
    Console.WriteLine("                   AppSettings table (Key = 'Encryption:SaltBackup').");
    Console.WriteLine();
    Console.WriteLine("  Examples:");
    Console.WriteLine("    dotnet run -- --db ./keyvault.db");
    Console.WriteLine("    dotnet run -- --hash \"abc123...==\"");
    Console.WriteLine();
}
