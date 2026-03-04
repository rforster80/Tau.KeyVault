# Tau Key Vault

A self-hosted key-value configuration store built with .NET 10, Blazor Server, and Microsoft Fluent UI. Provides a REST API and web-based admin interface for managing application configuration across multiple environments, with support for 9 typed data values, import/export, NATS and webhook notifications, and Protocol Buffers serialization.

## Repository Structure

```
Tau.KeyVault/
├── Tau.KeyVault/            # Server application (Blazor + REST API)
├── Tau.KeyVault.CSClient/   # C# .NET 10 client library
├── Tau.KeyVault.JsClient/   # JavaScript/TypeScript client library (ESM)
├── Tau.KeyVault.PyClient/   # Python client library
├── Tau.KeyVault.GoClient/   # Go client library
├── .gitignore
└── README.md                # ← you are here
```

## Server Application

The server project at `Tau.KeyVault/` is a .NET 10 Blazor Server application that provides both a web UI and a full REST API. See [`Tau.KeyVault/README.md`](Tau.KeyVault/README.md) for detailed server documentation.

### Key Features

- **Environment-scoped keys** with a Global fallback — if a key is not found in a specific environment, the Global value is returned automatically
- **9 data types** — Text, Code (uppercase), Numeric, Boolean, Date, Time, DateTime, Json, Csv
- **Sensitive data masking** — mark keys as sensitive to hide values in the UI
- **Import/Export** — bulk import and export keys per environment as JSON (Add Missing, Overwrite, Clean Import modes)
- **NATS & Webhook notifications** — per-environment notification channels with audit logging
- **Protocol Buffers** — all API endpoints support protobuf via `Accept: application/x-protobuf` header, with an auto-generated `.proto` schema
- **Customizable theming** — Light, Dark, and System modes with Microsoft Office accent colors
- **SQLite persistence** — zero-dependency database with automatic migrations
- **Swagger/OpenAPI** — interactive API docs at `/swagger`

### Quick Start

```bash
cd Tau.KeyVault
dotnet run
```

Navigate to `http://localhost:5000` and log in with `admin` / `admin`. Change the password immediately from the Settings page.

### API Authentication

All API requests require the `X-Api-Key` header. API keys are configured in `appsettings.json` under the `ApiKeys` array.

### API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/keys?environment=&raw=false` | List all keys with global fallback |
| GET | `/api/keys/{key}?environment=` | Get a single key |
| PUT | `/api/keys` | Create or update a key |
| GET | `/api/keys/environments` | List all environments |
| DELETE | `/api/keys/environments/{env}` | Delete an environment |
| PUT | `/api/keys/environments/{env}/rename` | Rename an environment |
| GET | `/api/keys/export?environment=` | Export keys |
| POST | `/api/keys/import` | Import keys |
| GET | `/api/keys/proto` | Download the .proto schema |
| GET | `/api/keys/typed/{key}?environment=` | Get a typed key value |
| GET | `/api/keys/typed?environment=` | List all typed key values |

### Protocol Buffers

Request protobuf responses by adding the `Accept: application/x-protobuf` header. Send protobuf request bodies with `Content-Type: application/x-protobuf`. The `.proto` schema is auto-generated at startup and downloadable from `/api/keys/proto` or as a static file at `/proto/keyvault.proto`.

## Client Libraries

All four client libraries provide the same feature set, mirroring the full REST API with additional typed convenience methods.

### Common Features

Every client library includes:

- **Full API coverage** — all swagger endpoints (get/set keys, environments, export/import, proto schema)
- **Key exists** — check if a key exists (returns bool, catches 404)
- **Typed getters** — `GetText`, `GetCode`, `GetNumeric`, `GetBoolean`, `GetDate`, `GetTime`, `GetDateTime`, `GetJson`, `GetCsv`
- **Typed updaters** — `UpdateText`, `UpdateCode` (auto-uppercase), `UpdateNumeric`, `UpdateBoolean`, `UpdateDate`, `UpdateTime`, `UpdateDateTime`, `UpdateJson`, `UpdateCsv`
- **GetOrCreate pattern** — get a typed value, creating the key with a default if it doesn't exist. Supports an optional `isSensitive` flag
- **CSV list management** — `CsvAdd`, `CsvRemove`, `CsvContains`, `CsvReplace` for managing comma-separated list values
- **3 transport modes** — API (JSON), Protobuf, and Protobuf-with-API-fallback

### C# Client (`Tau.KeyVault.CSClient`)

.NET 10 client with dependency injection support via `IHttpClientFactory`.

```bash
cd Tau.KeyVault.CSClient
dotnet build
```

```csharp
using Tau.KeyVault.Client;

var client = new KeyVaultClient(new KeyVaultClientOptions
{
    BaseUrl = "https://localhost:5001",
    ApiKey = "your-api-key",
    Transport = KeyVaultTransport.ProtobufWithApiFallback,
});

var port = await client.GetOrCreateNumericAsync("SmtpPort", 25, "Production");
```

Dependencies: `protobuf-net`, `System.Text.Json`, `Microsoft.Extensions.Http`

See [`Tau.KeyVault.CSClient/README.md`](Tau.KeyVault.CSClient/README.md) for full documentation.

### JavaScript Client (`Tau.KeyVault.JsClient`)

ESM module with full TypeScript declarations and `protobufjs` for protobuf encoding.

```bash
npm install tau-keyvault-client
```

```js
import { KeyVaultClient, KeyVaultTransport } from 'tau-keyvault-client';

const client = new KeyVaultClient({
  baseUrl: 'https://localhost:5001',
  apiKey: 'your-api-key',
  transport: KeyVaultTransport.ProtobufWithApiFallback,
});

const port = await client.getOrCreateNumeric('SmtpPort', 25, {
  environment: 'Production',
});
```

Dependencies: `protobufjs`

See [`Tau.KeyVault.JsClient/README.md`](Tau.KeyVault.JsClient/README.md) for full documentation.

### Python Client (`Tau.KeyVault.PyClient`)

Python 3.10+ client using `httpx` for HTTP and `protobuf` for wire-compatible encoding. Fully typed with `py.typed` marker.

```bash
pip install tau-keyvault
```

```python
from tau_keyvault import KeyVaultClient, KeyVaultTransport

client = KeyVaultClient(
    base_url="https://localhost:5001",
    api_key="your-api-key",
    transport=KeyVaultTransport.PROTOBUF_WITH_API_FALLBACK,
)

port = client.get_or_create_numeric("SmtpPort", 25, environment="Production")
```

Dependencies: `httpx`, `protobuf`

See [`Tau.KeyVault.PyClient/README.md`](Tau.KeyVault.PyClient/README.md) for full documentation.

### Go Client (`Tau.KeyVault.GoClient`)

Go 1.22+ client with zero external dependencies — includes a hand-rolled protobuf codec and uses the standard library `net/http`. All methods accept `context.Context`.

```bash
go get github.com/tau-keyvault/keyvault
```

```go
import kv "github.com/tau-keyvault/keyvault"

client, _ := kv.NewClient(kv.Options{
    BaseURL:   "https://localhost:5001",
    APIKey:    "your-api-key",
    Transport: kv.TransportProtobufWithAPIFallback,
})

port, _ := client.GetOrCreateNumeric(ctx, "SmtpPort", 25,
    kv.Env("Production"), false)
```

Dependencies: none (standard library only)

See [`Tau.KeyVault.GoClient/README.md`](Tau.KeyVault.GoClient/README.md) for full documentation.

## Data Types

All client libraries support the same 9 data types:

| Type | Storage | Notes |
|------|---------|-------|
| Text | `string` | General-purpose text |
| Code | `string` | Always stored as uppercase |
| Numeric | `string` → parsed | Stored as string, parsed to decimal/float by clients |
| Boolean | `string` → parsed | Accepts `true`/`false`/`1`/`0`/`yes`/`no` |
| Date | `string` | ISO format `YYYY-MM-DD` |
| Time | `string` | ISO format `HH:mm:ss` |
| DateTime | `string` | ISO format `YYYY-MM-DDTHH:mm:ss` |
| Json | `string` | Serialized JSON, clients handle parse/stringify |
| Csv | `string` | Comma-separated values with list management helpers |

## Environment Model

- An empty environment string (`""`) represents **Global**
- Keys are scoped to an environment
- When getting a key, if it's not found in the specified environment, the Global value is returned as a fallback
- The GetOrCreate pattern creates keys in the specified environment (or Global if empty)

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

