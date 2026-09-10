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

# Every key in every environment — the raw dump clients use to build a local cache.
everything = client.get_all_keys_all_environments()
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

### Deleting a Key

```python
# Delete one key from one environment.
deleted = client.delete_key("ConnectionString", environment="PRODUCTION")
print(f"{deleted.key} removed from {deleted.environment}")

# Unlike a get, a delete never falls back to Global: if the key exists only globally,
# this raises KeyVaultApiError with a 404 rather than deleting it. Pass an empty
# environment to delete the global entry itself.
client.delete_key("ConnectionString", environment="")
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

## Access Audit Log

Every read, write and delete is recorded server-side. Values are never recorded, so
nothing here can leak a secret.

```python
from datetime import datetime, timedelta, timezone
from tau_keyvault import KeyVaultAuditAction, KeyVaultAuditOutcome

# Everything that happened to one key — the erasure-evidence question.
trail = client.get_key_audit_trail("SubjectEmail")
for row in trail.items:
    print(f"{row.timestamp} {row.action:10} {row.actor_type}:{row.actor_id} {row.outcome}")

# Was a specific key actually destroyed?
erased = client.get_audit_log(key="SubjectEmail", action=KeyVaultAuditAction.DELETE_KEY)

# What has one credential been doing this week?
by_credential = client.get_audit_log(
    actor_id="adapter-prod",
    from_=datetime.now(timezone.utc) - timedelta(days=7),
    limit=500,
)

# Rejected credentials — the signal for a leaked key.
denied = client.get_audit_log(
    action=KeyVaultAuditAction.AUTH_FAILURE,
    outcome=KeyVaultAuditOutcome.DENIED,
)
print(f"{denied.total_count} rejected attempts")
```

## Per-Environment API Credentials

Requires a Global API key, and `EnableAPIKeyPerEnvironment` on the server. A credential
bound to an environment sees only that environment, with no Global fallback.

```python
# Mint a credential. The secret is returned once and is never retrievable again.
minted = client.create_api_key("adapter-prod", "PRODUCTION")
print(f"Store this now: {minted.key}")

# List them — metadata only, never the secret.
for k in client.list_api_keys().items:
    print(f"{k.id} {k.name} -> {k.environment} (enabled: {k.enabled})")

# Rotate: the previous secret stops working immediately.
rotated = client.rotate_api_key(minted.id)
print(f"New secret: {rotated.key}")

# Suspend without deleting, so the audit trail keeps naming its subject.
client.set_api_key_enabled(minted.id, False)

# Or remove it permanently.
client.revoke_api_key(minted.id)
```

## Publishing

`sample_publish-pypi.sh` builds and uploads the distribution. Copy it to the un-prefixed
names — `publish-pypi.sh`, `pypi_version.txt` — which are gitignored:

```bash
cp sample_publish-pypi.sh publish-pypi.sh
cp sample_pypi_version.txt pypi_version.txt

# build and twine check without uploading
PYPI_TOKEN=pypi-xxxx ./publish-pypi.sh -n

# upload
PYPI_TOKEN=pypi-xxxx ./publish-pypi.sh -r "https://upload.pypi.org/legacy/"
```

It increments the patch in the version file, syncs the `[project] version` in
`pyproject.toml`, builds an sdist and wheel, runs `twine check`, then uploads with token
auth (`__token__`).

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
| `get_all_keys_all_environments()` | List all keys across every environment (no filtering) |
| `get_key(key, env?)` | Get a single key by name |
| `upsert_key(key, value, env?, data_type?, is_sensitive?)` | Create or update a key |
| `get_environments()` | List all environments |
| `delete_key(key, environment=None)` | Delete a single key from one environment (no global fallback) |
| `get_audit_log(...)` | Query the access audit log (filters: key, environment, actor_id, action, outcome, from_, to) |
| `get_key_audit_trail(key, ...)` | Every audit row for one key, including its erasure evidence |
| `list_api_keys()` | List per-environment credentials (Global only; secrets never returned) |
| `create_api_key(name, environment)` | Mint a credential bound to one environment; the key is returned once |
| `rotate_api_key(id)` | Replace a credential's secret; the previous key stops working immediately |
| `set_api_key_enabled(id, enabled)` | Suspend or resume a credential without deleting it |
| `revoke_api_key(id)` | Permanently remove a credential |
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
