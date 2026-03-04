# Tau Key Vault

A self-hosted key-value configuration store built with .NET 10, Blazor Server, and Microsoft Fluent UI. Tau Key Vault provides a REST API and a web-based admin interface for managing application configuration across multiple environments with support for typed values, import/export, NATS and webhook notifications, and Protocol Buffers serialization.

## Features

- **Environment-scoped keys** — organize configuration by environment (e.g. `DEVELOPMENT`, `STAGING`, `PRODUCTION`) with a global fallback. If a key is not found in a specific environment, the global value is returned automatically.
- **9 data types** — `Text`, `Code` (uppercase), `Numeric`, `Boolean`, `Date`, `Time`, `DateTime`, `Json`, `Csv`. Typed API endpoints return values as their native types.
- **Sensitive data masking** — mark keys as sensitive and values are masked in the UI list views.
- **Import/Export** — bulk import and export keys per environment as JSON. Three import modes: Add Missing Only, Overwrite Existing, and Clean Import (Delete All).
- **NATS & Webhook notifications** — configure per-environment NATS servers and webhook URLs. Key changes automatically dispatch notifications with full audit logging.
- **Protocol Buffers support** — all API endpoints support protobuf serialization via content negotiation. A `.proto` schema file is auto-generated at startup.
- **Customizable theming** — Light, Dark, and System theme modes with Microsoft Office accent colors. Theme and application title are configurable in `appsettings.json` and overridable from the Settings page.
- **SQLite persistence** — zero-dependency database with automatic migrations on startup.
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
    "DefaultConnection": "Data Source=keyvault.db"
  },
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

| Setting | Description |
|---------|-------------|
| `ConnectionStrings:DefaultConnection` | SQLite database file path |
| `ApiKeys` | Array of valid API keys for authenticating REST API requests |
| `AppTitle` | Application title displayed in the header and browser tab. Overridable from the Settings page. |
| `Theme:Mode` | Default theme: `Light`, `Dark`, or `System` |
| `Theme:OfficeColor` | Default accent color: `Default`, `Word`, `Excel`, `PowerPoint`, `Outlook`, `OneNote`, `Teams`, `SharePoint`, etc. |

Theme and title changes made from the Settings page are saved to the database and override `appsettings.json` values.

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

#### Get Single Key

```
GET /api/keys/{key}?environment=PRODUCTION
```

Returns a single key by name. If not found in the specified environment, falls back to the global environment.

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

#### Typed Endpoints

```
GET /api/keys/typed/{key}?environment=PRODUCTION
GET /api/keys/typed?environment=PRODUCTION
```

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

## Notifications

Tau Key Vault can dispatch notifications when keys are created or updated. Configure notification endpoints from the **Notifications** page in the web UI.

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

Both NATS and webhook configurations support a "Lowercase Environment" option (enabled by default) and an enable/disable toggle. All dispatch attempts are logged with success/failure status, error messages, and HTTP status codes — viewable in the Dispatch Log section.

## Web Interface

The Blazor-based admin UI provides:

| Page | Description |
|------|-------------|
| **Keys** (`/`) | Browse, search, create, edit, delete keys. Supports pagination, environment filtering, sensitive value masking, and inline type badges. |
| **Notifications** (`/notifications`) | Configure NATS servers and webhooks per environment. View dispatch logs. |
| **Settings** (`/settings`) | Change theme (Light/Dark/System), accent color, application title, and admin password. |

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
