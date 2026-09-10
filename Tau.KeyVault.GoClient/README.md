# Tau Key Vault — Go Client (Untested Library)

Go client library for the [Tau Key Vault](../Tau.KeyVault/) REST API. Supports JSON and Protocol Buffers transport with typed helpers for all nine key-value data types.

## Installation

```bash
go get github.com/rforster80/Tau.KeyVault/Tau.KeyVault.GoClient
```

**Requirements:** Go 1.22+

## Quick Start

```go
package main

import (
    "context"
    "fmt"
    "log"

    kv "github.com/rforster80/Tau.KeyVault/Tau.KeyVault.GoClient"
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

// Every key in every environment — the raw dump clients use to build a local cache.
everything, err := client.GetAllKeysAllEnvironments(ctx)

// Get a single key
entry, err := client.GetKey(ctx, "SmtpHost", kv.Env("Production"))

// Create or update a key (entry and err already exist, so plain assignment)
entry, err = client.UpsertKey(ctx, "SmtpHost", "mail.example.com",
    kv.Env("Production"), kv.DataTypeText, false)
```

### Deleting a Key

```go
// Delete one key from one environment.
deleted, err := client.DeleteKey(ctx, "ConnectionString", kv.Env("PRODUCTION"))
if err != nil {
    log.Fatal(err)
}
fmt.Printf("%s removed from %s\n", deleted.Key, deleted.Environment)

// Unlike a get, a delete never falls back to Global: if the key exists only globally,
// this returns a 404 APIError rather than deleting it. Pass an empty environment to
// delete the global entry itself.
_, err = client.DeleteKey(ctx, "ConnectionString", kv.Env(""))
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

## Access Audit Log

Every read, write and delete is recorded server-side. Values are never recorded, so
nothing here can leak a secret.

```go
// Everything that happened to one key — the erasure-evidence question.
trail, err := client.GetKeyAuditTrail(ctx, "SubjectEmail", nil)
if err != nil {
    log.Fatal(err)
}
for _, row := range trail.Items {
    fmt.Printf("%s %-10s %s:%s %s\n",
        row.Timestamp, row.Action, row.ActorType, row.ActorID, row.Outcome)
}

// Was a specific key actually destroyed?
action := kv.AuditActionDeleteKey
key := "SubjectEmail"
erased, _ := client.GetAuditLog(ctx, &kv.AuditQuery{Key: &key, Action: &action})

// What has one credential been doing this week?
actor := "adapter-prod"
since := time.Now().Add(-7 * 24 * time.Hour)
limit := 500
byCredential, _ := client.GetAuditLog(ctx, &kv.AuditQuery{
    ActorID: &actor,
    From:    &since,
    Limit:   &limit,
})

// Rejected credentials — the signal for a leaked key.
authFailure := kv.AuditActionAuthFailure
denied := kv.AuditOutcomeDenied
rejected, _ := client.GetAuditLog(ctx, &kv.AuditQuery{
    Action:  &authFailure,
    Outcome: &denied,
})
fmt.Printf("%d rejected attempts\n", rejected.TotalCount)
```

## Per-Environment API Credentials

Requires a Global API key, and `EnableAPIKeyPerEnvironment` on the server. A credential
bound to an environment sees only that environment, with no Global fallback.

```go
// Mint a credential. The secret is returned once and is never retrievable again.
minted, err := client.CreateApiKey(ctx, "adapter-prod", "PRODUCTION")
if err != nil {
    log.Fatal(err)
}
fmt.Printf("Store this now: %s\n", minted.Key)

// List them — metadata only, never the secret.
keys, _ := client.ListApiKeys(ctx)
for _, k := range keys.Items {
    fmt.Printf("%d %s -> %s (enabled: %v)\n", k.ID, k.Name, k.Environment, k.Enabled)
}

// Rotate: the previous secret stops working immediately.
rotated, _ := client.RotateApiKey(ctx, minted.ID)
fmt.Printf("New secret: %s\n", rotated.Key)

// Suspend without deleting, so the audit trail keeps naming its subject.
_, _ = client.SetApiKeyEnabled(ctx, minted.ID, false)

// Or remove it permanently.
_, _ = client.RevokeApiKey(ctx, minted.ID)
```

## Publishing

Go has no registry upload — a module is published by pushing a semver git tag that the module
proxy can resolve. `sample_publish-gomodule.sh` does the validation and tagging. Copy it to
the un-prefixed names — `publish-gomodule.sh`, `go_version.txt` — which are gitignored:

```bash
cp sample_publish-gomodule.sh publish-gomodule.sh
cp sample_go_version.txt go_version.txt

# validate and show the tag it would create, changing nothing
./publish-gomodule.sh -n

# bump, gofmt/vet/build/test, tag locally
./publish-gomodule.sh

# ...and push the tag
./publish-gomodule.sh -p
```

It refuses to tag unless `gofmt`, `go vet`, `go build` and `go test` all pass, because a
pushed tag is immutable once the proxy has seen it. Nothing leaves your machine without `-p`.

> **Module path and tags.** This client is a module inside a larger repository, so its module
> path carries the subdirectory and its tags are prefixed with it —
> `Tau.KeyVault.GoClient/v1.1.0`, not `v1.1.0`. The script derives both from `go.mod` and the
> repository layout, and warns if they ever drift apart.

## Error Handling

All API errors return `*keyvault.APIError` with `StatusCode` and `APIMessage` fields.

```go
import kv "github.com/rforster80/Tau.KeyVault/Tau.KeyVault.GoClient"

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
| `GetAllKeysAllEnvironments(ctx)` | List all keys across every environment (no filtering) |
| `GetKey(ctx, key, env)` | Get a single key by name |
| `UpsertKey(ctx, key, value, env, dataType, sensitive)` | Create or update a key |
| `GetEnvironments(ctx)` | List all environments |
| `DeleteKey(ctx, key, env)` | Delete a single key from one environment (no global fallback) |
| `GetAuditLog(ctx, *AuditQuery)` | Query the access audit log (filters: key, environment, actorID, action, outcome, from, to) |
| `GetKeyAuditTrail(ctx, key, env)` | Every audit row for one key, including its erasure evidence |
| `ListApiKeys(ctx)` | List per-environment credentials (Global only; secrets never returned) |
| `CreateApiKey(ctx, name, env)` | Mint a credential bound to one environment; the key is returned once |
| `RotateApiKey(ctx, id)` | Replace a credential's secret; the previous key stops working immediately |
| `SetApiKeyEnabled(ctx, id, enabled)` | Suspend or resume a credential without deleting it |
| `RevokeApiKey(ctx, id)` | Permanently remove a credential |
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
