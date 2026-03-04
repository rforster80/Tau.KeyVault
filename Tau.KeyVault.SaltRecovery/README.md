# Tau Key Vault — Salt Recovery Tool

A standalone .NET 10 console application for recovering the encryption salt used by Tau Key Vault. If `appsettings.json` is lost, corrupted, or reset, this tool decrypts the salt backup stored in the SQLite database and provides the value needed to restore normal operation.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Access to the `keyvault.db` SQLite database file **or** the encrypted backup hash extracted from it

## Build

```bash
cd Tau.KeyVault.SaltRecovery
dotnet build
```

## Usage

The tool supports two modes — point it at the database file directly, or provide a pre-extracted encrypted blob.

### Option 1: From a database file

```bash
dotnet run -- --db <path-to-keyvault.db>
```

The tool opens the database in **read-only** mode, queries the `AppSettings` table for the `Encryption:SaltBackup` entry, and decrypts it.

```
dotnet run -- --db ../Tau.KeyVault/keyvault.db
```

### Option 2: From an extracted hash

If you have already extracted the encrypted blob from the database (e.g. using a SQLite browser), pass it directly:

```bash
dotnet run -- --hash "<base64-encrypted-blob>"
```

To extract the blob manually:

```sql
SELECT Value FROM AppSettings WHERE Key = 'Encryption:SaltBackup';
```

Then pass the result:

```
dotnet run -- --hash "abc123...=="
```

## Output

On success the tool prints the recovered Base64 salt and the exact JSON to add back to `appsettings.json`:

```
  Salt recovered successfully!

  Salt (Base64): /wrrUIGnqoQ4s4VfCz51mzCf5aprkIvTAXd7RWzbr7Y=

  Add the following to your appsettings.json and restart the application:

  "Encryption": {
    "Salt": "/wrrUIGnqoQ4s4VfCz51mzCf5aprkIvTAXd7RWzbr7Y="
  }
```

## Recovery Steps

1. Run this tool using either `--db` or `--hash` as shown above.
2. Copy the recovered salt value.
3. Open (or recreate) `appsettings.json` in the main Tau Key Vault project directory.
4. Add or replace the `Encryption` section:
   ```json
   {
     "Encryption": {
       "Salt": "<recovered-salt-value>"
     }
   }
   ```
5. Restart the Tau Key Vault application. On startup it will detect the salt in configuration and continue normally.

## How It Works

The main Tau Key Vault application stores an AES-256-CBC encrypted copy of the encryption salt in the `AppSettings` database table under the key `Encryption:SaltBackup`. The AES key is derived from a password embedded in the application source code using PBKDF2 (SHA-256, 100,000 iterations). This recovery tool contains the same password and decryption logic, allowing it to reverse the process without any dependency on the main project.

The encrypted blob format is: `[16-byte PBKDF2 salt][16-byte AES IV][ciphertext]`, all concatenated and Base64-encoded.

## License

MIT License — Tau Inventions (Pty) Ltd.
