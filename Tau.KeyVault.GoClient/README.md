# Tau Key Vault — Go Client (Untested Library)

Go client library for the [Tau Key Vault](../Tau.KeyVault/) REST API. Supports JSON and Protocol Buffers transport with typed helpers for all nine key-value data types.

## Installation

```bash
go get github.com/tau-keyvault/keyvault
```

**Requirements:** Go 1.22+

## Quick Start

```go
package main

import (
    "context"
    "fmt"
    "log"

    kv "github.com/tau-keyvault/keyvault"
)

func main() {
    client, err := kv.NewClient(kv.Options{
        BaseURL: "https://localhost:5001",
        APIKey:  "your-api-key",
    })
    if err != nil {
        log.Fatal(err)
    }

    ctx := context.Background()

    // Get a key
    entry, err := client.GetKey(ctx, "SmtpHost", nil)
    if err != nil {
        log.Fatal(err)
    }
    fmt.Println(entry.Value) // "smtp.example.com"

    // Set a key
    _, err = client.UpsertKey(ctx, "SmtpHost", "mail.example.com",
        kv.Env("Production"), kv.DataTypeText, false)
    if err != nil {
        log.Fatal(err)
    }
}
```

## Options

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `BaseURL` | `string` | *required* | Base URL of the Tau Key Vault server |
| `APIKey` | `string` | *required* | API key (sent as `X-Api-Key` header) |
| `DefaultEnvironment` | `string` | `""` (Global) | Default environment for all requests |
| `Transport` | `Transport` | `TransportAPI` | Transport mode (see below) |
| `Timeout` | `time.Duration` | `30s` | HTTP request timeout |
| `HTTPClient` | `*http.Client` | `nil` | Optional pre-configured HTTP client |

## Transport Modes

```go
kv.TransportAPI                      // JSON for all requests (default)
kv.TransportProtobuf                 // Protocol Buffers for all requests
kv.TransportProtobufWithAPIFallback  // Try Protobuf, fall back to JSON
```

```go
client, _ := kv.NewClient(kv.Options{
    BaseURL:   "https://localhost:5001",
    APIKey:    "your-api-key",
    Transport: kv.TransportProtobufWithAPIFallback,
})
```

## Environments

An empty string (`""`) represents the **Global** environment. Keys in Global act as fallback defaults when a key is not found in a specific environment.

Use `nil` for the default environment, or `kv.Env("Production")` to specify one:

```go
// Use default environment (Global)
client.GetText(ctx, "SmtpHost", nil)

// Specify environment
client.GetText(ctx, "SmtpHost", kv.Env("Production"))
```

## Core API Methods

These map directly to the Tau Key Vault REST API endpoints.

### Keys

```go
// List all keys (with global fallback)
result, err := client.GetAllKeys(ctx, kv.Env("Production"), false)

// Get a single key
entry, err := client.GetKey(ctx, "SmtpHost", kv.Env("Production"))

// Create or update a key
entry, err := client.UpsertKey(ctx, "SmtpHost", "mail.example.com",
    kv.Env("Production"), kv.DataTypeText, false)
```

### Environments

```go
result, err := client.GetEnvironments(ctx)

resp, err := client.DeleteEnvironment(ctx, "OldEnv")

resp, err := client.RenameEnvironment(ctx, "Staging", "QA")
```

### Export / Import

```go
// Export
payload, err := client.Export(ctx, kv.Env("Production"))

// Import
result, err := client.Import(ctx, &kv.ImportRequest{
    Environment: "Staging",
    Mode:        "merge",
    Keys: []kv.ImportKeyItem{
        {Key: "SmtpHost", Value: "smtp.test.com", DataType: "Text"},
    },
})
```

### Proto Schema

```go
schema, err := client.GetProtoSchema(ctx)
```

## Key Exists

```go
exists, err := client.KeyExists(ctx, "SmtpHost", kv.Env("Production"))
```

Returns `true` if the key is found; `false` on 404. Other errors are returned.

## Typed Get Helpers

| Method | Returns |
|--------|---------|
| `GetText(ctx, key, env)` | `string` |
| `GetCode(ctx, key, env)` | `string` (uppercase) |
| `GetNumeric(ctx, key, env)` | `float64` |
| `GetBoolean(ctx, key, env)` | `bool` |
| `GetDate(ctx, key, env)` | `time.Time` |
| `GetTime(ctx, key, env)` | `time.Time` |
| `GetDateTime(ctx, key, env)` | `time.Time` |
| `GetJSON(ctx, key, &target, env)` | unmarshals into target |
| `GetCSV(ctx, key, env)` | `[]string` |

```go
port, err := client.GetNumeric(ctx, "SmtpPort", nil)
debug, err := client.GetBoolean(ctx, "DebugMode", nil)
tags, err := client.GetCSV(ctx, "AllowedTags", nil)

var config AppConfig
err := client.GetJSON(ctx, "AppConfig", &config, nil)
```

## Typed Update Helpers

| Method | Value Type |
|--------|-----------|
| `UpdateText(ctx, key, value, env, sensitive)` | `string` |
| `UpdateCode(ctx, key, value, env, sensitive)` | `string` (auto-uppercased) |
| `UpdateNumeric(ctx, key, value, env, sensitive)` | `float64` |
| `UpdateBoolean(ctx, key, value, env, sensitive)` | `bool` |
| `UpdateDate(ctx, key, value, env, sensitive)` | `time.Time` |
| `UpdateTime(ctx, key, value, env, sensitive)` | `time.Time` |
| `UpdateDateTime(ctx, key, value, env, sensitive)` | `time.Time` |
| `UpdateJSON(ctx, key, value, env, sensitive)` | `any` (marshaled) |
| `UpdateCSV(ctx, key, values, env, sensitive)` | `[]string` |

```go
client.UpdateNumeric(ctx, "SmtpPort", 587, kv.Env("Production"), false)
client.UpdateBoolean(ctx, "DebugMode", false, nil, false)
client.UpdateCode(ctx, "CountryCode", "za", nil, false) // stored as "ZA"
client.UpdateDate(ctx, "LaunchDate", time.Date(2026, 6, 1, 0, 0, 0, 0, time.UTC), nil, false)
```

## GetOrCreate Pattern

These methods get a key's typed value, creating it with the provided default if the key doesn't exist. The `isSensitive` flag is only used when creating.

| Method | Default Type | Returns |
|--------|-------------|---------|
| `GetOrCreateText(ctx, key, default, env, sensitive)` | `string` | `string` |
| `GetOrCreateCode(ctx, key, default, env, sensitive)` | `string` | `string` |
| `GetOrCreateNumeric(ctx, key, default, env, sensitive)` | `float64` | `float64` |
| `GetOrCreateBoolean(ctx, key, default, env, sensitive)` | `bool` | `bool` |
| `GetOrCreateDate(ctx, key, default, env, sensitive)` | `time.Time` | `time.Time` |
| `GetOrCreateTime(ctx, key, default, env, sensitive)` | `time.Time` | `time.Time` |
| `GetOrCreateDateTime(ctx, key, default, env, sensitive)` | `time.Time` | `time.Time` |
| `GetOrCreateJSON(ctx, key, default, &target, env, sensitive)` | `any` | unmarshals into target |
| `GetOrCreateCSV(ctx, key, default, env, sensitive)` | `[]string` | `[]string` |

```go
// Returns existing value or creates with default
port, err := client.GetOrCreateNumeric(ctx, "SmtpPort", 25,
    kv.Env("Production"), false)

apiKey, err := client.GetOrCreateText(ctx, "ExternalApiKey", "change-me",
    nil, true) // isSensitive = true

var config AppConfig
err := client.GetOrCreateJSON(ctx, "Defaults",
    AppConfig{Retries: 3, Timeout: 5000}, &config, nil, false)
```

## CSV List Management

Convenience methods for managing comma-separated list values.

```go
// Add an item (creates key if missing)
list, err := client.CSVAdd(ctx, "AllowedOrigins", "https://app.example.com", nil, false)

// Remove an item
list, err := client.CSVRemove(ctx, "AllowedOrigins", "https://old.example.com", nil, false)

// Check membership
has, err := client.CSVContains(ctx, "AllowedOrigins", "https://app.example.com", nil)

// Replace an item
list, err := client.CSVReplace(ctx, "AllowedOrigins",
    "https://old.example.com", "https://new.example.com", nil, false)
```

## Error Handling

All API errors return `*keyvault.APIError` with `StatusCode` and `APIMessage` fields.

```go
import kv "github.com/tau-keyvault/keyvault"

entry, err := client.GetKey(ctx, "MissingKey", nil)
if err != nil {
    if kv.IsNotFound(err) {
        fmt.Println("Key not found")
    } else if apiErr, ok := err.(*kv.APIError); ok {
        fmt.Println(apiErr.StatusCode, apiErr.APIMessage)
    }
}
```

## Environment Helper

The `Env()` function creates a `*string` for passing environments:

```go
kv.Env("Production")  // → *string pointing to "Production"
nil                    // → uses default environment
```

## Custom HTTP Client

You can inject a pre-configured `*http.Client` for advanced scenarios (proxies, TLS, retries):

```go
import "crypto/tls"

httpClient := &http.Client{
    Transport: &http.Transport{
        TLSClientConfig: &tls.Config{InsecureSkipVerify: true},
    },
}

client, _ := kv.NewClient(kv.Options{
    BaseURL:    "https://localhost:5001",
    APIKey:     "your-api-key",
    HTTPClient: httpClient,
})
```

## API Reference

### Core Methods

| Method | Description |
|--------|-------------|
| `GetAllKeys(ctx, env, raw)` | List all keys for an environment |
| `GetKey(ctx, key, env)` | Get a single key by name |
| `UpsertKey(ctx, key, value, env, dataType, sensitive)` | Create or update a key |
| `GetEnvironments(ctx)` | List all environments |
| `DeleteEnvironment(ctx, env)` | Delete an environment and its keys |
| `RenameEnvironment(ctx, env, newName)` | Rename an environment |
| `Export(ctx, env)` | Export all keys for an environment |
| `Import(ctx, request)` | Import keys into an environment |
| `GetProtoSchema(ctx)` | Download the .proto schema |
| `KeyExists(ctx, key, env)` | Check if a key exists |

### Typed Helpers

Nine data types, each with `Get*`, `Update*`, and `GetOrCreate*` variants.

### CSV Helpers

| Method | Description |
|--------|-------------|
| `CSVAdd(ctx, key, item, env, sensitive)` | Add an item to a CSV list |
| `CSVRemove(ctx, key, item, env, sensitive)` | Remove first occurrence |
| `CSVContains(ctx, key, item, env)` | Check if list contains item |
| `CSVReplace(ctx, key, old, new, env, sensitive)` | Replace all occurrences |

## License

UNLICENSED
