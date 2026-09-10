# Tau Key Vault

A self-hosted key-value configuration store built with .NET 10, Blazor Server, and Microsoft Fluent UI. Tau Key Vault provides a REST API and a web-based admin interface for managing application configuration across multiple environments with support for typed values, import/export, NATS, Kafka and webhook notifications, and Protocol Buffers serialization.

## Features

- **Environment-scoped keys** — organize configuration by environment (e.g. `DEVELOPMENT`, `STAGING`, `PRODUCTION`) with a global fallback. If a key is not found in a specific environment, the global value is returned — unless `GlobalKeyFailover` is off, or the caller holds an environment-bound API credential.
- **9 data types** — `Text`, `Code` (uppercase), `Numeric`, `Boolean`, `Date`, `Time`, `DateTime`, `Json`, `Csv`. Typed API endpoints return values as their native types.
- **Sensitive data masking** — mark keys as sensitive and values are masked in the UI list views.
- **Import/Export** — bulk import and export keys per environment as JSON. Three import modes: Add Missing Only, Overwrite Existing, and Clean Import (Delete All).
- **NATS, Kafka & Webhook notifications** — configure per-environment NATS servers, Kafka topics and webhook URLs. Key changes automatically dispatch notifications with full audit logging. Kafka supports the full connection surface: TLS, mutual TLS, SASL PLAIN/SCRAM/Kerberos/OAuth, with credentials encrypted at rest.
- **Protocol Buffers support** — all API endpoints support protobuf serialization via content negotiation. A `.proto` schema file is auto-generated at startup.
- **Customizable theming** — Light, Dark, and System theme modes with Microsoft Office accent colors. Theme and application title are configurable in `appsettings.json` and overridable from the Settings page.
- **SQLite or Postgres** — SQLite by default for dependency-free container runs; set `UseSqlite: false` for Postgres. Each provider has its own migration set, applied automatically on startup.
- **Swagger/OpenAPI** — interactive API documentation at `/swagger`.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

### Run

```bash
cd Tau.KeyVault
dotnet run
```

The application starts on `http://localhost:5000` (or the port configured in `launchSettings.json`).

### Default Credentials

On first launch the database is seeded with a default admin user:

| Field    | Value   |
|----------|---------|
| Username | `admin` |
| Password | `admin` |

Change the password immediately from the **Settings** page after first login.

### Configuration

All settings are in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=keyvault.db",
    "PostgresConnection": "Host=localhost;Port=5432;Database=keyvault;Username=keyvault;Password=CHANGE-ME"
  },
  "UseSqlite": true,
  "GlobalKeyFailover": true,
  "EnableAPIKeyPerEnvironment": false,
  "ApiKeys": [
    "YOUR-API-KEY-HERE"
  ],
  "AppTitle": "Tau Key Vault",
  "Theme": {
    "Mode": "Light",
    "OfficeColor": "Default"
  }
}
```

An `Encryption` section is added automatically on first run — see
[Encryption at rest](#encryption-at-rest) below. Do not hand-write it.

| Setting | Description |
|---------|-------------|
| `UseSqlite` | `true` (default) uses SQLite; `false` uses Postgres via `ConnectionStrings:PostgresConnection` |
| `GlobalKeyFailover` | `true` (default) resolves a missing environment key from Global; `false` disables that fallback entirely |
| `EnableAPIKeyPerEnvironment` | `false` (default). `true` accepts per-environment credentials minted via `/api/apikeys` |
| `ConnectionStrings:DefaultConnection` | SQLite database file path |
| `ConnectionStrings:PostgresConnection` | Postgres connection string, used only when `UseSqlite` is `false` |
| `Encryption:Salt` | AES key for value encryption. Generated on first run and written back to this file — never set it by hand; recover a lost one with the Salt Recovery tool |
| `ApiKeys` | Valid API keys. Accepts plain strings, or `{ "Name": "...", "Key": "..." }` objects so the audit log can name the credential |
| `AppTitle` | Application title displayed in the header and browser tab. Overridable from the Settings page. |
| `Theme:Mode` | Default theme: `Light`, `Dark`, or `System` |
| `Theme:OfficeColor` | Default accent color: `Default`, `Word`, `Excel`, `PowerPoint`, `Outlook`, `OneNote`, `Teams`, `SharePoint`, etc. |

Theme and title changes made from the Settings page are saved to the database and override `appsettings.json` values.

## Encryption at Rest

Every key value is encrypted in the database with AES-256-CBC. A read that bypasses the
application — a stolen database file, a `SELECT` against Postgres — yields only ciphertext:

```
Value = "ENC:" + Base64( [16-byte IV][ciphertext] )
```

`KeyVaultService` encrypts on write and decrypts on read, so nothing that goes through the
API or the admin UI needs to know. Values written before encryption was introduced are
migrated in place on startup; anything without the `ENC:` prefix is treated as legacy
plaintext and encrypted on the next pass.

### The salt

The AES key is `Encryption:Salt` in `appsettings.json`, 32 random bytes generated on first
run. Startup keeps it consistent in three cases:

| On startup | Behaviour |
|------------|-----------|
| Salt in config | An encrypted backup is written to the `AppSettings` table if absent |
| No salt, backup in the database | The salt is recovered and **written back into `appsettings.json`** |
| Neither | A fresh salt is generated and stored in both places |

So `appsettings.json` is rewritten at runtime, not just read. Never hand-write the
`Encryption` section, and keep the file writable by the application.

> **Losing the salt means losing every value.** The database holds an encrypted backup of it,
> which the bundled [Salt Recovery tool](../Tau.KeyVault.SaltRecovery/README.md) can decrypt:
>
> ```bash
> cd Tau.KeyVault.SaltRecovery
> dotnet run -- --db ../Tau.KeyVault/keyvault.db
> ```
>
> Back up `appsettings.json` alongside the database. A backup of the database alone is
> recoverable only while you still have this source code.

Kafka broker credentials are protected the same way, with the same salt — see
[Notifications](#kafka).

## Storage Engine

SQLite is the default so the app runs from `docker run` with no external dependencies.
For enterprise deployments, point it at Postgres instead:

```json
{
  "UseSqlite": false,
  "ConnectionStrings": {
    "PostgresConnection": "Host=db.example.com;Port=5432;Database=keyvault;Username=keyvault;Password=..."
  }
}
```

Startup fails with a clear error if `UseSqlite` is `false` and `PostgresConnection` is unset.

Verified against PostgreSQL 18.6: migration applies, all 9 data types round-trip with
at-rest encryption intact, and NATS/webhook dispatch logs as on SQLite.

### Provider-specific migrations

The two providers do **not** share a migration set — the SQLite migrations use `TEXT`/`INTEGER`
column types, the `Sqlite:Autoincrement` annotation, and one issues a raw `PRAGMA foreign_keys`
that Postgres rejects. `ProviderMigrationsAssembly` scopes migration discovery by namespace:

| Location | Applies to |
|----------|------------|
| `Data/Migrations/` | SQLite — the original 6-migration history |
| `Data/Migrations/Postgres/` | Postgres — one consolidated `InitialCreate` |

Postgres is treated as a fresh-install target, so it gets the final schema in a single
migration rather than a replay of the SQLite history.

**A schema change must be written once per provider.** To scaffold a new Postgres migration,
temporarily move `Data/Migrations/AppDbContextModelSnapshot.cs` aside — it is the SQLite
baseline, and `dotnet ef` will otherwise diff SQLite-against-Postgres and emit a migration
full of `AlterColumn` calls instead of the intended change:

```bash
UseSqlite=false ConnectionStrings__PostgresConnection="Host=...;Database=..." \
  dotnet ef migrations add <Name> \
    --output-dir Data/Migrations/Postgres \
    --namespace Tau.KeyVault.Data.Migrations.Postgres
```

Note that `dotnet ef` writes the new snapshot to a path derived from `--namespace`
(`Tau/KeyVault/Data/Migrations/Postgres/`) rather than to `--output-dir` — move it into
`Data/Migrations/Postgres/` by hand. Keep it: each provider needs its own snapshot, because
EF compares the live model against it at `MigrateAsync()` and aborts startup with
`PendingModelChangesWarning` on a mismatch. `ProviderMigrationsAssembly` selects the right
snapshot per provider alongside the right migrations.

Verify which set a provider sees with `dotnet ef migrations list --no-connect`.

## Global Key Failover

By default a key not found in the requested environment resolves to the Global (blank
environment) value. Set `GlobalKeyFailover` to `false` to switch that off, so an
environment only ever sees keys defined in it:

| Request | `true` (default) | `false` |
|---------|------------------|---------|
| `GET /api/keys/Foo?environment=PROD`, `Foo` only in Global | Global value, `200` | `404` |
| `GET /api/keys?environment=PROD` | PROD keys + Global keys PROD does not define | PROD keys only |
| `GET /api/keys/Foo` (blank environment) | Global value | Global value — unchanged |

Blank-environment requests are direct lookups, not fallbacks, so they are unaffected.
`GET /api/keys/all` is a raw cross-environment dump and is also unaffected.

## REST API

All API endpoints are under `/api/keys` and require an API key via the `X-Api-Key` header.

### Authentication

Every request to `/api/*` must include:

```
X-Api-Key: YOUR-API-KEY-HERE
```

API keys are configured in the `ApiKeys` array in `appsettings.json`. Requests without a valid key receive a `401` or `403` response.

### Response Format

By default, responses are JSON. To receive Protocol Buffers, set the `Accept` header:

| Header | Format |
|--------|--------|
| `Accept: application/json` (or omitted) | JSON |
| `Accept: application/x-protobuf` | Protocol Buffers (binary) |

For request bodies (PUT/POST), set `Content-Type` accordingly:

| Header | Format |
|--------|--------|
| `Content-Type: application/json` | JSON request body |
| `Content-Type: application/x-protobuf` | Protocol Buffers request body |

### Endpoints

#### List Keys

```
GET /api/keys?environment=PRODUCTION
```

Returns all keys for the given environment with global fallback resolution. Add `&raw=true` to skip fallback and return only keys stored directly in that environment.

```bash
# JSON (default)
curl -H "X-Api-Key: YOUR-KEY" http://localhost:5000/api/keys?environment=PRODUCTION

# Protobuf
curl -H "X-Api-Key: YOUR-KEY" -H "Accept: application/x-protobuf" \
  http://localhost:5000/api/keys?environment=PRODUCTION --output keys.bin
```

#### List Keys Across All Environments

```
GET /api/keys/all
```

Every key in every environment, unfiltered and with no global-fallback merging — the raw dump
clients use to build a local cache. An environment-bound credential gets only its own
environment's keys here, so it cannot discover that others exist.

#### Get Single Key

```
GET /api/keys/{key}?environment=PRODUCTION
```

Returns a single key by name. If not found in the specified environment, falls back to the global
environment — unless `GlobalKeyFailover` is `false`, or the caller holds an environment-bound
API credential, in which case the response is `404` and no Global value is disclosed.

#### Upsert Key

```
PUT /api/keys
Content-Type: application/json

{
  "key": "ConnectionString",
  "value": "Server=db.example.com;Database=app",
  "environment": "PRODUCTION",
  "dataType": "Text",
  "isSensitive": true
}
```

Creates the key if it doesn't exist, or updates it if it does. Valid `dataType` values: `Text`, `Code`, `Numeric`, `Boolean`, `Date`, `Time`, `DateTime`, `Json`, `Csv`. Defaults to `Text` if omitted.

#### Delete Single Key

```
DELETE /api/keys/{key}?environment=PRODUCTION
```

Deletes one key from one environment. Unlike `GET`, this does **not** fall back to
the global environment: a key that exists only globally is left untouched and the
response is `404`. Omit `environment` (or leave it blank) to delete the global entry
itself. Deleting a key dispatches NATS/webhook notifications the same way an upsert does.

```bash
curl -X DELETE -H "X-Api-Key: YOUR-KEY" \
  "http://localhost:5000/api/keys/ConnectionString?environment=PRODUCTION"
```

#### Typed Endpoints

```
GET /api/keys/typed/{key}?environment=PRODUCTION
GET /api/keys/typed?environment=PRODUCTION
GET /api/keys/typed/all
```

`/api/keys/typed/all` is the typed equivalent of `/api/keys/all`: every environment, unfiltered.

These endpoints return values as their native types in JSON responses: `Numeric` as a number, `Boolean` as `true`/`false`, `Csv` as a string array, `Json` as an object. For protobuf responses, values are always strings with a `ValueType` field indicating how to interpret them.

#### List Environments

```
GET /api/keys/environments
```

#### Delete Environment

```
DELETE /api/keys/environments/STAGING
```

Deletes the environment and all its key-value pairs.

#### Rename Environment

```
PUT /api/keys/environments/STAGING/rename
Content-Type: application/json

{
  "newName": "UAT"
}
```

#### Export Keys

```
GET /api/keys/export?environment=PRODUCTION
```

Returns a portable JSON payload containing all keys for the environment with metadata.

#### Import Keys

```
POST /api/keys/import
Content-Type: application/json

{
  "environment": "STAGING",
  "mode": "Overwrite",
  "keys": [
    {
      "key": "ApiUrl",
      "value": "https://api.staging.example.com",
      "dataType": "Text",
      "isSensitive": false
    }
  ]
}
```

Import modes:

| Mode | Behavior |
|------|----------|
| `AddMissing` | Only add keys that don't exist. Safest option. |
| `Overwrite` | Update existing keys and add new ones. Other keys are preserved. |
| `DeleteAll` | Delete all existing keys in the environment first, then import. |

## Protocol Buffers Support

Tau Key Vault supports [Protocol Buffers](https://protobuf.dev/) as an alternative to JSON for all API endpoints. This provides smaller payloads and faster serialization, ideal for service-to-service communication.

### How to Use

Set the `Accept` header to `application/x-protobuf` on any API request:

```bash
# Get all keys as protobuf
curl -H "X-Api-Key: YOUR-KEY" \
     -H "Accept: application/x-protobuf" \
     http://localhost:5000/api/keys?environment=PRODUCTION \
     --output response.bin

# Send a protobuf request body
curl -X PUT \
     -H "X-Api-Key: YOUR-KEY" \
     -H "Content-Type: application/x-protobuf" \
     -H "Accept: application/x-protobuf" \
     --data-binary @request.bin \
     http://localhost:5000/api/keys
```

### Download the .proto Schema

The `.proto` file is auto-generated at application startup from the annotated C# models and can be downloaded in two ways:

```bash
# Via the API endpoint
curl -H "X-Api-Key: YOUR-KEY" http://localhost:5000/api/keys/proto

# Via static file (no auth required)
curl http://localhost:5000/proto/keyvault.proto
```

Use this schema file with `protoc` to generate client code in any language:

```bash
# Generate C# client
protoc --csharp_out=./generated keyvault.proto

# Generate Python client
protoc --python_out=./generated keyvault.proto

# Generate Go client
protoc --go_out=./generated keyvault.proto
```

### .NET Client Example

For .NET consumers, you can use `protobuf-net` directly with the shared DTO classes, or generate types from the `.proto` file:

```csharp
using ProtoBuf;
using System.Net.Http.Headers;

var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
client.DefaultRequestHeaders.Add("X-Api-Key", "YOUR-KEY");
client.DefaultRequestHeaders.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/x-protobuf"));

// GET keys as protobuf
var response = await client.GetAsync("/api/keys?environment=PRODUCTION");
var stream = await response.Content.ReadAsStreamAsync();
var keys = Serializer.Deserialize<KeyEntryListResponse>(stream);

foreach (var key in keys.Items)
    Console.WriteLine($"{key.Key} = {key.Value} ({key.DataType})");
```

### Message Types

The schema includes these message types:

| Message | Used For |
|---------|----------|
| `KeyEntryResponse` | Single key response |
| `KeyEntryListResponse` | List of keys |
| `TypedKeyEntryResponse` | Single typed key (value is string, interpret via `ValueType`) |
| `TypedKeyEntryListResponse` | List of typed keys |
| `UpsertRequest` | Create/update a key |
| `RenameRequest` | Rename an environment |
| `ImportRequest` / `ImportKeyItem` | Bulk import keys |
| `ExportPayloadResponse` / `ExportKeyItemResponse` | Export payload |
| `EnvironmentListResponse` | List of environment names |
| `DeleteEnvironmentResponse` | Delete environment result |
| `RenameEnvironmentResponse` | Rename environment result |
| `ImportResultResponse` | Import result (imported/skipped counts) |
| `ErrorResponse` | Error detail |

## Per-Environment API Credentials

Off by default. Set `"EnableAPIKeyPerEnvironment": true` to allow credentials that are each
bound to a single environment, alongside the Global keys in `ApiKeys`.

### The two kinds of credential

| | Global | Environment-bound |
|---|---|---|
| Defined in | `appsettings.json` (`ApiKeys`) | the database, via `/api/apikeys` |
| Rotated by | editing config and restarting the service | `POST /api/apikeys/{id}/rotate`, no restart |
| Reaches | every environment | exactly one |
| Global fallback | per `GlobalKeyFailover` | **never**, whatever `GlobalKeyFailover` says |
| Can administer environments and credentials | yes | no |

A credential is bound to **one** environment — binding to Global is rejected, because Global
access is what the configured keys are for.

### What a bound credential can and cannot do

It can read, list, write, delete, export and import keys, and read the audit log — always
confined to its own environment. A request that names no environment is taken to mean its
own, so consumers need not repeat themselves; a request naming a different one is refused
with `403` and recorded as `ScopeViolation`.

It cannot delete or rename environments, manage credentials, or enumerate environments other
than its own. `GET /api/keys/environments` returns just its environment, and
`GET /api/keys/all` returns just its environment's keys, so it cannot discover that other
environments exist. A leaked bound credential therefore cannot escalate into more
credentials, and cannot destroy an environment.

### Managing credentials

All Global-only, and all `409` while the feature is disabled.

| Method | Endpoint | Notes |
|--------|----------|-------|
| GET | `/api/apikeys` | List. Secrets are never returned |
| POST | `/api/apikeys` | `{ "name": "...", "environment": "..." }` → the key, **once** |
| POST | `/api/apikeys/{id}/rotate` | New secret; the previous one stops working immediately |
| PUT | `/api/apikeys/{id}` | `{ "enabled": false }` to suspend without deleting |
| DELETE | `/api/apikeys/{id}` | Permanent |

```bash
curl -X POST -H "X-Api-Key: GLOBAL-KEY" -H "Content-Type: application/json" \
  -d '{"name":"adapter-prod","environment":"PRODUCTION"}' \
  http://localhost:5000/api/apikeys
```

Only a SHA-256 hash is stored, so a credential is shown exactly once, at creation and at
rotation. There is no way to read it back — a lost key is rotated, not recovered. Every
create, rotate, update and revoke is audited by credential name; the secret never reaches
the audit log or any listing.

Disabling is preferable to deleting when you want the audit trail to keep naming its subject.

## Access Audit Log

A durable record of who read, wrote or deleted which key, in which environment, and when.
It never records a value, and there is no column in which one could be stored.

### What is recorded

| Field | Notes |
|-------|-------|
| `timestamp` | UTC |
| `action` | `ReadKey`, `ListKeys`, `WriteKey`, `DeleteKey`, `ListEnvironments`, `DeleteEnvironment`, `RenameEnvironment`, `Export`, `Import`, `ReadAudit`, `AuthFailure` |
| `key` | Blank for collection-level actions |
| `environment` | Blank means Global |
| `actorType` / `actorId` | `ApiKey` + the configured key *name*, or `User` + admin username. Never the API key itself |
| `outcome` | `Success`, `NotFound`, `Denied`, `Error` |
| `ipAddress` | Blank for admin UI actions on an established Blazor circuit |
| `itemCount` | Keys affected by a collection-level action |

Auditing lives in `KeyVaultService`, at the data-access boundary, so the REST API and the
Blazor admin UI are both covered and neither can touch a key without leaving a record.

### Naming your API keys

`ApiKeys` accepts both shapes, so existing configuration keeps working unchanged:

```json
"ApiKeys": [
  "LEGACY-PLAIN-STRING-KEY",
  { "Name": "adapter-prod", "Key": "SECRET-VALUE" }
]
```

A plain string is audited as `#0`, `#1` … by position; a named object is audited as its
`Name`. Naming them is what lets you answer "which credential did this?" — and, when a
credential is compromised, "what else did it touch?".

### Rejected credentials

A missing or invalid API key is recorded as `AuthFailure` / `Denied` with the caller IP.
The presented key is never written anywhere. This is the signal to watch for a leaked
credential:

```
GET /api/audit?action=AuthFailure
```

### Erasure evidence

Every destruction path writes a per-key `DeleteKey` row — single-key delete, environment
delete, and Clean Import purge alike — so evidence that a subject's key was destroyed holds
regardless of how it was destroyed:

```
GET /api/audit?key=SubjectEmail&action=DeleteKey
```

### Querying

```
GET /api/audit?key=&environment=&actorId=&action=&outcome=&from=&to=&limit=&offset=
```

All filters are optional and combine with AND; rows come back newest first with a
`totalCount` for paging. Reading the audit log is itself audited, as `ReadAudit`.

### Durability and fail-closed behaviour

Two guarantees, both from ADR-045:

1. **Audit rows survive a rollback of the operation they describe.** Each row is written on
   its own connection and committed immediately, never enlisted in the operation's
   transaction.
2. **An operation that cannot be audited does not happen.** There is no queue and no
   fire-and-forget. If the row cannot be committed the request is refused with `503` and
   nothing is changed — verified by breaking the audit table and confirming that a refused
   write leaves no key behind and a refused delete destroys nothing.

Mutations are therefore audited **before** they are applied. The ordering is forced: the
audit row cannot share the operation's transaction (that is what makes it survive a
rollback), so auditing afterwards would leave a committed change with no record whenever the
audit store is down. The trade-off is the opposite error — a row may describe an attempt
that was subsequently rolled back, which is exactly the semantics ADR-045 asks for. Where a
mutation fails after its row is written, a follow-up row with `outcome=Error` is appended.

> **SQLite cannot provide guarantee 1 under an explicit transaction.** SQLite allows a single
> writer, so an audit write on a second connection fails with `database is locked` while the
> operation's transaction is open, and the operation is then refused. The vault opens no
> explicit transactions today, so SQLite behaves correctly in normal use — but if you need
> the rollback-survival guarantee to hold in general, run on Postgres, where it is verified.

There is deliberately **no API to delete or prune audit rows**. Plan retention at the
database level; on a busy vault the read paths make this the fastest-growing table.

## Notifications

Tau Key Vault can dispatch notifications when keys are created or updated. Configure notification endpoints from the **Notifications** page in the web UI.

### Kafka

Publish key changes to a Kafka topic. Configure brokers per environment from the
**Notifications** page. The topic supports the same `{environment}` and `{key}` placeholders
as NATS:

```
10.0.0.100:9092  →  keyvault.{environment}.updates  →  keyvault.production.updates
```

The message **key** is the vault key name, so consumers get per-key partitioning and
ordering; the value is the same payload NATS receives:

```json
{ "environment": "PRODUCTION", "key": "ConnectionString", "timestamp": "2026-09-10T10:20:03Z" }
```

**Connection scenarios.** The full librdkafka connection surface is configurable:

| Area | Settings |
|------|----------|
| Protocol | `Plaintext`, `Ssl`, `SaslPlaintext`, `SaslSsl` |
| SASL | `Plain`, `ScramSha256`, `ScramSha512`, `Gssapi` (Kerberos), `OAuthBearer` (OIDC) |
| SASL credentials | username + password; Kerberos service name, principal and keytab; OAuth client id, secret, token endpoint, scope and extensions |
| TLS | CA certificate, client certificate and key for mutual TLS, key passphrase, and certificate verification toggle |
| Producer | acks, message and request timeouts, idempotence, compression, client id |
| Anything else | a free-form `key=value` block applied last, so it overrides the fields above |

**Secrets** — the SASL password, TLS key passphrase and OAuth client secret — are encrypted at
rest with the vault salt, exactly as key values are, and are write-only in the UI: the stored
value is never sent to the browser, and leaving a secret field blank on edit keeps the stored
one. The free-form block is stored in plain text, so put credentials in the dedicated fields.

**Producer lifetime.** Unlike NATS, which opens a connection per dispatch, Kafka producers are
built once per configuration and cached — a librdkafka producer owns background threads and a
metadata cache, so building one per key change would cost far more than the publish. Editing a
configuration changes its fingerprint and the next dispatch transparently builds a replacement;
deleting one disposes it.

> **A broker that is down slows key writes.** Dispatch is inline on the write path, as it is
> for NATS and webhooks, so an unreachable broker makes each write wait up to
> `MessageTimeoutMs` (default 5000) before the failure is logged. The write itself still
> succeeds and is never rolled back — lower the timeout if that latency matters.

### NATS

Configure NATS server URLs and queue names per environment. Queue names support the `{environment}` placeholder:

```
nats://localhost:4222  →  keyvault.{environment}.updates  →  keyvault.production.updates
```

### Webhooks

Configure webhook URLs per environment. URLs support `{environment}` and `{key}` placeholders:

```
https://api.example.com/{environment}/config-changed?key={key}
```

NATS, Kafka and webhook configurations all support a "Lowercase Environment" option (enabled by default) and an enable/disable toggle. All dispatch attempts are logged with success/failure status, error messages, and HTTP status codes — viewable in the Dispatch Log section.

## Web Interface

The Blazor-based admin UI provides:

| Page | Description |
|------|-------------|
| **Keys** (`/`) | Browse, search, create, edit, delete keys. Supports pagination, environment filtering, sensitive value masking, and inline type badges. |
| **Notifications** (`/notifications`) | Configure NATS servers and webhooks per environment. View dispatch logs. |
| **Audit** (`/audit`) | Browse the access audit log with filters for key, environment, actor, action, outcome and date range. Quick views for erasures, rejected credentials and scope violations. Viewing it is itself audited. |
| **API Keys** (`/apikeys`) | Create, rotate, suspend and revoke per-environment credentials. The secret is displayed once, on creation and rotation. Shows an explanation instead when `EnableAPIKeyPerEnvironment` is off. |
| **Settings** (`/settings`) | Change theme (Light/Dark/System), accent color, application title, and admin password. Also shows a read-only Runtime Configuration panel: storage provider, `GlobalKeyFailover`, `EnableAPIKeyPerEnvironment`, Global key count and encryption status. |

Access the UI at `http://localhost:5000` and log in with the admin credentials.

## Project Structure

```
Tau.KeyVault/
├── Controllers/
│   └── KeyVaultApiController.cs    # REST API endpoints
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor        # App shell with sidebar and theme
│   └── Pages/
│       ├── Keys.razor              # Key management UI
│       ├── Notifications.razor     # Notification config UI
│       ├── Settings.razor          # Theme, title, password settings
│       └── Login.razor             # Authentication page
├── Data/
│   ├── AppDbContext.cs             # EF Core database context
│   ├── DbSeeder.cs                # Initial data seeding
│   └── Migrations/                # EF Core migrations
├── Formatters/
│   ├── ProtobufInputFormatter.cs   # Deserializes protobuf requests
│   ├── ProtobufOutputFormatter.cs  # Serializes protobuf responses
│   └── ProtoSchemaGenerator.cs     # Auto-generates .proto file
├── Middleware/
│   └── ApiKeyMiddleware.cs         # API key authentication
├── Models/
│   ├── KeyEntry.cs                 # Key-value entry entity
│   ├── DataType.cs                 # Data type enum
│   ├── ApiRequests.cs              # API request DTOs (protobuf-annotated)
│   ├── ApiResponses.cs             # API response DTOs (protobuf-annotated)
│   ├── AppSetting.cs               # App settings entity
│   ├── AppUser.cs                  # User entity
│   ├── NatsConfig.cs               # NATS config entity
│   ├── WebhookConfig.cs            # Webhook config entity
│   └── NotificationLog.cs          # Notification audit log entity
├── Services/
│   ├── KeyVaultService.cs          # Core key-value operations
│   ├── AuthService.cs              # User authentication
│   ├── AppSettingsService.cs       # App settings (DB > config > default)
│   ├── NotificationConfigService.cs # NATS/webhook config CRUD
│   └── NotificationDispatchService.cs # Dispatch + logging
├── wwwroot/
│   ├── app.css                     # Theme-aware styles (Fluent UI vars)
│   └── proto/
│       └── keyvault.proto          # Auto-generated protobuf schema
├── Program.cs                      # App startup and configuration
├── appsettings.json                # Configuration file
└── Tau.KeyVault.csproj             # Project file
```

## Technology Stack

- **.NET 10** — runtime and web framework
- **Blazor Server** — interactive server-side UI
- **Microsoft Fluent UI v4** — component library with theming
- **Entity Framework Core** — ORM with SQLite provider
- **protobuf-net v3** — Protocol Buffers serialization
- **NATS.Net v2** — NATS messaging client
- **Swashbuckle** — Swagger/OpenAPI documentation

## License

MIT License

Copyright (c) 2026 Tau Inventions (Pty) Ltd.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
