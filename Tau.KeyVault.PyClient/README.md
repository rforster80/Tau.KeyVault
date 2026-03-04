# Tau Key Vault — Python Client (Untested Library)

Python client library for the [Tau Key Vault](../Tau.KeyVault/) REST API. Supports JSON and Protocol Buffers transport with typed helpers for all nine key-value data types.

## Installation

```bash
pip install tau-keyvault
```

Or install from source:

```bash
cd Tau.KeyVault.PyClient
pip install .
```

**Requirements:** Python 3.10+, httpx, protobuf

## Quick Start

```python
from tau_keyvault import KeyVaultClient

client = KeyVaultClient(
    base_url="https://localhost:5001",
    api_key="your-api-key",
)

# Get a key
entry = client.get_key("SmtpHost")
print(entry.value)  # "smtp.example.com"

# Set a key
from tau_keyvault import KeyVaultDataType

client.upsert_key(
    "SmtpHost", "mail.example.com",
    environment="Production",
    data_type=KeyVaultDataType.TEXT,
)
```

The client also works as a context manager:

```python
with KeyVaultClient(base_url="...", api_key="...") as client:
    value = client.get_text("SmtpHost")
```

## Constructor Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `base_url` | `str` | *required* | Base URL of the Tau Key Vault server |
| `api_key` | `str` | *required* | API key (sent as `X-Api-Key` header) |
| `default_environment` | `str` | `""` (Global) | Default environment for all requests |
| `transport` | `KeyVaultTransport` | `API` | Transport mode (see below) |
| `timeout` | `float` | `30.0` | Request timeout in seconds |
| `http_client` | `httpx.Client` | `None` | Optional pre-configured httpx client |

## Transport Modes

```python
from tau_keyvault import KeyVaultTransport
```

| Mode | Description |
|------|-------------|
| `KeyVaultTransport.API` | JSON for all requests (default) |
| `KeyVaultTransport.PROTOBUF` | Protocol Buffers for all requests |
| `KeyVaultTransport.PROTOBUF_WITH_API_FALLBACK` | Try Protobuf first; on failure, retry JSON |

```python
client = KeyVaultClient(
    base_url="https://localhost:5001",
    api_key="your-api-key",
    transport=KeyVaultTransport.PROTOBUF_WITH_API_FALLBACK,
)
```

## Environments

An empty string (`""`) represents the **Global** environment. Keys in Global act as fallback defaults when a key is not found in a specific environment.

```python
# Global (default)
client = KeyVaultClient(base_url=url, api_key=key)

# Environment-specific
client = KeyVaultClient(
    base_url=url, api_key=key,
    default_environment="Production",
)

# Override per-call
client.get_text("SmtpHost", environment="Staging")
```

## Core API Methods

These map directly to the Tau Key Vault REST API endpoints.

### Keys

```python
# List all keys (with global fallback)
result = client.get_all_keys(environment="Production")
for entry in result.items:
    print(entry.key, entry.value)

# Get a single key
entry = client.get_key("SmtpHost", environment="Production")

# Create or update a key
client.upsert_key(
    "SmtpHost", "mail.example.com",
    environment="Production",
    data_type=KeyVaultDataType.TEXT,
    is_sensitive=False,
)
```

### Environments

```python
result = client.get_environments()
print(result.environments)  # ["Production", "Staging", ...]

client.delete_environment("OldEnv")

client.rename_environment("Staging", "QA")
```

### Export / Import

```python
from tau_keyvault import ImportRequest, ImportKeyItem

# Export
payload = client.export(environment="Production")

# Import
client.import_keys(ImportRequest(
    environment="Staging",
    mode="merge",
    keys=[
        ImportKeyItem(key="SmtpHost", value="smtp.test.com", data_type="Text", is_sensitive=False),
    ],
))
```

### Proto Schema

```python
schema = client.get_proto_schema()
```

## Key Exists

```python
exists = client.key_exists("SmtpHost", environment="Production")
```

Returns `True` if the key is found; `False` on 404. Other errors are re-raised.

## Typed Get Helpers

Each method returns the value parsed to the appropriate Python type.

| Method | Returns |
|--------|---------|
| `get_text(key, env?)` | `str` |
| `get_code(key, env?)` | `str` (uppercase) |
| `get_numeric(key, env?)` | `Decimal` |
| `get_boolean(key, env?)` | `bool` |
| `get_date(key, env?)` | `datetime.date` |
| `get_time(key, env?)` | `datetime.time` |
| `get_datetime(key, env?)` | `datetime.datetime` |
| `get_json(key, env?)` | `Any` (parsed JSON) |
| `get_csv(key, env?)` | `list[str]` |

```python
from decimal import Decimal

port = client.get_numeric("SmtpPort")       # Decimal('587')
debug = client.get_boolean("DebugMode")      # False
tags = client.get_csv("AllowedTags")         # ["tag1", "tag2"]
config = client.get_json("AppConfig")        # {"retries": 3}
```

## Typed Update Helpers

| Method | Value Type |
|--------|-----------|
| `update_text(key, value, env?, is_sensitive?)` | `str` |
| `update_code(key, value, env?, is_sensitive?)` | `str` (auto-uppercased) |
| `update_numeric(key, value, env?, is_sensitive?)` | `Decimal \| float \| int` |
| `update_boolean(key, value, env?, is_sensitive?)` | `bool` |
| `update_date(key, value, env?, is_sensitive?)` | `datetime.date` |
| `update_time(key, value, env?, is_sensitive?)` | `datetime.time` |
| `update_datetime(key, value, env?, is_sensitive?)` | `datetime.datetime` |
| `update_json(key, value, env?, is_sensitive?)` | `Any` (serialized) |
| `update_csv(key, values, env?, is_sensitive?)` | `list[str]` |

```python
from datetime import date, time, datetime

client.update_numeric("SmtpPort", 587, environment="Production")
client.update_boolean("DebugMode", False)
client.update_code("CountryCode", "za")  # stored as "ZA"
client.update_date("LaunchDate", date(2026, 6, 1))
client.update_time("CutoffTime", time(17, 0, 0))
client.update_datetime("LastSync", datetime(2026, 3, 4, 12, 0, 0))
client.update_json("AppConfig", {"retries": 5, "timeout": 10000})
client.update_csv("AllowedOrigins", ["https://app.example.com", "https://admin.example.com"])
```

## GetOrCreate Pattern

These methods get a key's typed value, creating it with the provided default if the key doesn't exist. The optional `is_sensitive` flag is only used when creating.

| Method | Default Type | Returns |
|--------|-------------|---------|
| `get_or_create_text(key, default, env?, is_sensitive?)` | `str` | `str` |
| `get_or_create_code(key, default, env?, is_sensitive?)` | `str` | `str` |
| `get_or_create_numeric(key, default, env?, is_sensitive?)` | `Decimal\|float\|int` | `Decimal` |
| `get_or_create_boolean(key, default, env?, is_sensitive?)` | `bool` | `bool` |
| `get_or_create_date(key, default, env?, is_sensitive?)` | `date` | `date` |
| `get_or_create_time(key, default, env?, is_sensitive?)` | `time` | `time` |
| `get_or_create_datetime(key, default, env?, is_sensitive?)` | `datetime` | `datetime` |
| `get_or_create_json(key, default, env?, is_sensitive?)` | `Any` | `Any` |
| `get_or_create_csv(key, default, env?, is_sensitive?)` | `list[str]` | `list[str]` |

```python
# Returns existing value or creates with default
port = client.get_or_create_numeric("SmtpPort", 25, environment="Production")

api_key = client.get_or_create_text("ExternalApiKey", "change-me", is_sensitive=True)

config = client.get_or_create_json("Defaults", {"retries": 3, "timeout": 5000})
```

## CSV List Management

Convenience methods for managing comma-separated list values.

```python
# Add an item (creates key if missing)
items = client.csv_add("AllowedOrigins", "https://app.example.com")

# Remove an item
items = client.csv_remove("AllowedOrigins", "https://old.example.com")

# Check membership
has = client.csv_contains("AllowedOrigins", "https://app.example.com")

# Replace an item
items = client.csv_replace(
    "AllowedOrigins",
    "https://old.example.com",
    "https://new.example.com",
)
```

## Error Handling

All API errors raise `KeyVaultApiError` with `status_code` and `api_error` attributes.

```python
from tau_keyvault import KeyVaultApiError

try:
    client.get_key("MissingKey")
except KeyVaultApiError as e:
    print(e.status_code)  # 404
    print(e.api_error)    # "Key 'MissingKey' not found ..."
```

## Custom httpx Client

You can inject a pre-configured httpx client for advanced scenarios (proxies, TLS, retries).

```python
import httpx

http = httpx.Client(
    verify=False,  # disable TLS verification for dev
    follow_redirects=True,
)

client = KeyVaultClient(
    base_url="https://localhost:5001",
    api_key="your-api-key",
    http_client=http,
)
```

## Type Hints

The package is fully typed and ships with a `py.typed` marker for PEP 561. All models are dataclasses with proper type annotations, giving you autocomplete and type checking out of the box.

## API Reference

### Core Methods

| Method | Description |
|--------|-------------|
| `get_all_keys(env?)` | List all keys for an environment |
| `get_key(key, env?)` | Get a single key by name |
| `upsert_key(key, value, env?, data_type?, is_sensitive?)` | Create or update a key |
| `get_environments()` | List all environments |
| `delete_environment(env)` | Delete an environment and its keys |
| `rename_environment(env, new_name)` | Rename an environment |
| `export(env?)` | Export all keys for an environment |
| `import_keys(request)` | Import keys into an environment |
| `get_proto_schema()` | Download the .proto schema |
| `key_exists(key, env?)` | Check if a key exists |

### Typed Helpers

Nine data types, each with `get_*`, `update_*`, and `get_or_create_*` variants.

### CSV Helpers

| Method | Description |
|--------|-------------|
| `csv_add(key, item, env?, is_sensitive?)` | Add an item to a CSV list |
| `csv_remove(key, item, env?, is_sensitive?)` | Remove first occurrence from a CSV list |
| `csv_contains(key, item, env?)` | Check if a CSV list contains an item |
| `csv_replace(key, old, new, env?, is_sensitive?)` | Replace all occurrences in a CSV list |

## License

UNLICENSED
