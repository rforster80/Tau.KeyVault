# Tau.KeyVault.Client - C# Client (Untested Library)

A .NET 10 client library for the [Tau Key Vault](../Tau.KeyVault/README.md) API. Provides full coverage of all API endpoints plus typed helper methods for every supported data type, a GetOrCreate pattern for auto-provisioning keys, CSV list management, and configurable transport (JSON, Protocol Buffers, or Protobuf-with-JSON-fallback).

## Installation

Add a project reference or NuGet package reference:

```xml
<PackageReference Include="Tau.KeyVault.Client" Version="1.0.0" />
```

Or as a project reference within the solution:

```xml
<ProjectReference Include="..\Tau.KeyVault.Client\Tau.KeyVault.Client.csproj" />
```

## Quick Start

### With Dependency Injection (recommended)

Register the client in `Program.cs` or `Startup.cs`:

```csharp
builder.Services.AddKeyVaultClient(options =>
{
    options.BaseUrl = "https://vault.example.com";
    options.ApiKey = "YOUR-API-KEY";
    options.DefaultEnvironment = "PRODUCTION";
    options.Transport = KeyVaultTransport.Api; // JSON (default)
});
```

Then inject `KeyVaultClient` into your services:

```csharp
public class MyService
{
    private readonly KeyVaultClient _vault;

    public MyService(KeyVaultClient vault)
    {
        _vault = vault;
    }

    public async Task DoWork()
    {
        var connStr = await _vault.GetTextAsync("ConnectionString");
        var maxRetries = await _vault.GetNumericAsync("MaxRetries");
        var featureEnabled = await _vault.GetBooleanAsync("EnableNewFeature");
    }
}
```

### Without Dependency Injection

```csharp
var vault = new KeyVaultClient(new KeyVaultClientOptions
{
    BaseUrl = "https://vault.example.com",
    ApiKey = "YOUR-API-KEY",
    DefaultEnvironment = "PRODUCTION",
    Transport = KeyVaultTransport.Api
});

var value = await vault.GetTextAsync("MyKey");
```

## Transport Modes

The client supports three transport modes, configured via `KeyVaultClientOptions.Transport`:

| Mode | Description |
|------|-------------|
| `KeyVaultTransport.Api` | JSON serialization for all requests and responses. This is the default. |
| `KeyVaultTransport.Protobuf` | Protocol Buffers serialization for all requests and responses. Smaller payloads, faster serialization. |
| `KeyVaultTransport.ProtobufWithApiFallback` | Attempts Protobuf first; if the request fails for any reason, automatically retries with JSON. Useful during migration or when protobuf support is uncertain. |

```csharp
// Use protobuf with automatic JSON fallback
builder.Services.AddKeyVaultClient(options =>
{
    options.BaseUrl = "https://vault.example.com";
    options.ApiKey = "YOUR-API-KEY";
    options.Transport = KeyVaultTransport.ProtobufWithApiFallback;
});
```

## Environments

Tau Key Vault organises keys by environment. An empty string (`""`) represents the **Global** environment, which acts as a fallback — if a key is not found in a specific environment, the global value is returned automatically.

Set the default environment in options so you don't have to pass it on every call:

```csharp
options.DefaultEnvironment = "PRODUCTION";
```

You can always override per-call:

```csharp
// Uses the default environment from options
var value = await vault.GetTextAsync("ApiUrl");

// Override for a specific environment
var stagingValue = await vault.GetTextAsync("ApiUrl", environment: "STAGING");

// Explicitly use the Global environment
var globalValue = await vault.GetTextAsync("ApiUrl", environment: "");
```

## Core API Methods

These methods map directly to the Tau Key Vault REST API endpoints.

### Keys

```csharp
// List all keys for an environment
var keys = await vault.GetAllKeysAsync(environment: "PRODUCTION");

// Get a single key
var entry = await vault.GetKeyAsync("ConnectionString", environment: "PRODUCTION");

// Create or update a key
var result = await vault.UpsertKeyAsync(
    key: "MaxRetries",
    value: "5",
    environment: "PRODUCTION",
    dataType: KeyVaultDataType.Numeric,
    isSensitive: false);

// Check if a key exists
bool exists = await vault.KeyExistsAsync("MyKey", environment: "PRODUCTION");
```

### Environments

```csharp
// List all environments
var envs = await vault.GetEnvironmentsAsync();

// Delete an environment and all its keys
var deleted = await vault.DeleteEnvironmentAsync("STAGING");

// Rename an environment
var renamed = await vault.RenameEnvironmentAsync("STAGING", "UAT");
```

### Export / Import

```csharp
// Export all keys from an environment
var payload = await vault.ExportAsync(environment: "PRODUCTION");

// Import keys into an environment
var result = await vault.ImportAsync(new ImportRequest
{
    Environment = "STAGING",
    Mode = "Overwrite",
    Keys = new List<ImportKeyItem>
    {
        new() { Key = "ApiUrl", Value = "https://staging.example.com", DataType = "Text" }
    }
});
```

### Proto Schema

```csharp
// Download the .proto schema file
string protoSchema = await vault.GetProtoSchemaAsync();
File.WriteAllText("keyvault.proto", protoSchema);
```

## Typed Get and Update Helpers

Every supported data type has dedicated Get and Update methods that handle serialization and parsing automatically.

### Text

```csharp
string value = await vault.GetTextAsync("AppName");
await vault.UpdateTextAsync("AppName", "My Application");
```

### Code (Uppercase Text)

Values are always stored and returned in uppercase.

```csharp
string code = await vault.GetCodeAsync("CountryCode");    // Returns "US"
await vault.UpdateCodeAsync("CountryCode", "us");          // Stored as "US"
```

### Numeric

Returned as `decimal` for full precision.

```csharp
decimal rate = await vault.GetNumericAsync("TaxRate");     // Returns 0.085m
await vault.UpdateNumericAsync("TaxRate", 0.09m);
```

### Boolean

Recognises `true`, `1`, `yes` as truthy values.

```csharp
bool enabled = await vault.GetBooleanAsync("FeatureFlag");
await vault.UpdateBooleanAsync("FeatureFlag", true);
```

### Date

Uses `DateOnly` and `yyyy-MM-dd` format.

```csharp
DateOnly date = await vault.GetDateAsync("LaunchDate");
await vault.UpdateDateAsync("LaunchDate", new DateOnly(2026, 6, 15));
```

### Time

Uses `TimeOnly` and `HH:mm:ss` format.

```csharp
TimeOnly time = await vault.GetTimeAsync("CutoffTime");
await vault.UpdateTimeAsync("CutoffTime", new TimeOnly(17, 30, 0));
```

### DateTime

Uses `DateTime` and `yyyy-MM-ddTHH:mm:ss` format.

```csharp
DateTime dt = await vault.GetDateTimeAsync("LastSync");
await vault.UpdateDateTimeAsync("LastSync", DateTime.UtcNow);
```

### JSON

Automatically serializes and deserializes to your types.

```csharp
// Define your model
public class SmtpSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public bool UseSsl { get; set; }
}

// Read
var smtp = await vault.GetJsonAsync<SmtpSettings>("SmtpConfig");

// Write
await vault.UpdateJsonAsync("SmtpConfig", new SmtpSettings
{
    Host = "smtp.example.com",
    Port = 587,
    UseSsl = true
});
```

### CSV (Comma-Separated List)

Stored as comma-separated text, returned as `List<string>`.

```csharp
// Read
List<string> tags = await vault.GetCsvAsync("AllowedOrigins");

// Write
await vault.UpdateCsvAsync("AllowedOrigins", new[] { "https://app.example.com", "https://admin.example.com" });
```

## GetOrCreate Pattern

Every data type has a `GetOrCreate` method that returns the existing value or creates the key with a default value if it doesn't exist. This is the recommended way to read configuration — it ensures keys are always provisioned.

All `GetOrCreate` methods accept an optional `isSensitive` parameter to flag the value for masking in the UI.

```csharp
// String types
string appName = await vault.GetOrCreateTextAsync("AppName", "My App");
string region = await vault.GetOrCreateCodeAsync("Region", "us-east-1");

// Numeric
decimal taxRate = await vault.GetOrCreateNumericAsync("TaxRate", 0.085m);

// Boolean
bool maintenance = await vault.GetOrCreateBooleanAsync("MaintenanceMode", false);

// Date / Time / DateTime
DateOnly launch = await vault.GetOrCreateDateAsync("LaunchDate", new DateOnly(2026, 1, 1));
TimeOnly cutoff = await vault.GetOrCreateTimeAsync("CutoffTime", new TimeOnly(17, 0, 0));
DateTime sync = await vault.GetOrCreateDateTimeAsync("LastSync", DateTime.UtcNow);

// JSON (with complex default)
var smtp = await vault.GetOrCreateJsonAsync("SmtpConfig", new SmtpSettings
{
    Host = "smtp.example.com",
    Port = 587,
    UseSsl = true
});

// CSV
var origins = await vault.GetOrCreateCsvAsync("AllowedOrigins",
    new[] { "https://localhost:5000" });

// Sensitive data
string secret = await vault.GetOrCreateTextAsync("ApiSecret", "default-secret",
    isSensitive: true);
```

## CSV List Management

For CSV-type keys, the client provides list manipulation helpers that read the current list, modify it, and save it back in a single call.

```csharp
// Add an item to the list
List<string> updated = await vault.CsvAddAsync("AllowedOrigins", "https://new.example.com");

// Remove an item
updated = await vault.CsvRemoveAsync("AllowedOrigins", "https://old.example.com");

// Check if an item exists in the list
bool contains = await vault.CsvContainsAsync("AllowedOrigins", "https://app.example.com");

// Replace an item
updated = await vault.CsvReplaceAsync("AllowedOrigins",
    "https://old.example.com",
    "https://new.example.com");
```

## Error Handling

API errors are thrown as `KeyVaultApiException` with the HTTP status code and server error message:

```csharp
try
{
    var value = await vault.GetTextAsync("NonExistentKey");
}
catch (KeyVaultApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
{
    Console.WriteLine($"Key not found: {ex.ApiError}");
}
catch (KeyVaultApiException ex)
{
    Console.WriteLine($"API error {ex.StatusCode}: {ex.ApiError}");
}
```

## Configuration via appsettings.json

You can bind options from configuration:

```json
{
  "KeyVault": {
    "BaseUrl": "https://vault.example.com",
    "ApiKey": "YOUR-API-KEY",
    "DefaultEnvironment": "PRODUCTION",
    "Transport": "ProtobufWithApiFallback",
    "Timeout": "00:00:30"
  }
}
```

```csharp
builder.Services.AddKeyVaultClient(options =>
    builder.Configuration.GetSection("KeyVault").Bind(options));
```

## API Reference

### Core Methods

| Method | Description |
|--------|-------------|
| `GetAllKeysAsync` | List all keys for an environment |
| `GetKeyAsync` | Get a single key by name |
| `UpsertKeyAsync` | Create or update a key |
| `KeyExistsAsync` | Check if a key exists |
| `GetEnvironmentsAsync` | List all environments |
| `DeleteEnvironmentAsync` | Delete an environment and all its keys |
| `RenameEnvironmentAsync` | Rename an environment |
| `ExportAsync` | Export keys from an environment |
| `ImportAsync` | Import keys into an environment |
| `GetProtoSchemaAsync` | Download the .proto schema |

### Typed Getters

| Method | Return Type |
|--------|------------|
| `GetTextAsync` | `string` |
| `GetCodeAsync` | `string` (uppercase) |
| `GetNumericAsync` | `decimal` |
| `GetBooleanAsync` | `bool` |
| `GetDateAsync` | `DateOnly` |
| `GetTimeAsync` | `TimeOnly` |
| `GetDateTimeAsync` | `DateTime` |
| `GetJsonAsync<T>` | `T` |
| `GetCsvAsync` | `List<string>` |

### Typed Updaters

| Method | Input Type |
|--------|-----------|
| `UpdateTextAsync` | `string` |
| `UpdateCodeAsync` | `string` → stored uppercase |
| `UpdateNumericAsync` | `decimal` |
| `UpdateBooleanAsync` | `bool` |
| `UpdateDateAsync` | `DateOnly` |
| `UpdateTimeAsync` | `TimeOnly` |
| `UpdateDateTimeAsync` | `DateTime` |
| `UpdateJsonAsync<T>` | `T` → serialized to JSON |
| `UpdateCsvAsync` | `IEnumerable<string>` → joined with commas |

### GetOrCreate Methods

| Method | Return Type | Default Type |
|--------|------------|--------------|
| `GetOrCreateTextAsync` | `string` | `string` |
| `GetOrCreateCodeAsync` | `string` | `string` → uppercased |
| `GetOrCreateNumericAsync` | `decimal` | `decimal` |
| `GetOrCreateBooleanAsync` | `bool` | `bool` |
| `GetOrCreateDateAsync` | `DateOnly` | `DateOnly` |
| `GetOrCreateTimeAsync` | `TimeOnly` | `TimeOnly` |
| `GetOrCreateDateTimeAsync` | `DateTime` | `DateTime` |
| `GetOrCreateJsonAsync<T>` | `T` | `T` → serialized |
| `GetOrCreateCsvAsync` | `List<string>` | `IEnumerable<string>` |

### CSV Helpers

| Method | Description |
|--------|-------------|
| `CsvAddAsync` | Add an item to a CSV list |
| `CsvRemoveAsync` | Remove an item from a CSV list |
| `CsvContainsAsync` | Check if a CSV list contains an item |
| `CsvReplaceAsync` | Replace an item in a CSV list |
