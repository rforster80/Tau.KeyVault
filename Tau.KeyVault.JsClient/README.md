# Tau Key Vault — JavaScript Client (Untested Library)

JavaScript/TypeScript client library for the [Tau Key Vault](../Tau.KeyVault/) REST API. Supports JSON and Protocol Buffers transport with typed helpers for all nine key-value data types.

## Installation

```bash
npm install tau-keyvault-client
```

The package ships as ESM (`"type": "module"`) with full TypeScript declarations (`.d.ts`).

## Quick Start

```js
import { KeyVaultClient } from 'tau-keyvault-client';

const client = new KeyVaultClient({
  baseUrl: 'https://localhost:5001',
  apiKey: 'your-api-key',
});

// Get a key
const entry = await client.getKey('SmtpHost');
console.log(entry.value); // "smtp.example.com"

// Set a key
await client.upsertKey('SmtpHost', 'mail.example.com', {
  environment: 'Production',
  dataType: 'Text',
});
```

## Constructor Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `baseUrl` | `string` | *required* | Base URL of the Tau Key Vault server |
| `apiKey` | `string` | *required* | API key (sent as `X-Api-Key` header) |
| `defaultEnvironment` | `string` | `''` (Global) | Default environment for all requests |
| `transport` | `string` | `'Api'` | Transport mode (see below) |
| `timeout` | `number` | `30000` | Request timeout in milliseconds |
| `fetch` | `function` | `globalThis.fetch` | Custom fetch implementation |

## Transport Modes

```js
import { KeyVaultTransport } from 'tau-keyvault-client';
```

| Mode | Description |
|------|-------------|
| `KeyVaultTransport.Api` | JSON for all requests (default) |
| `KeyVaultTransport.Protobuf` | Protocol Buffers for all requests |
| `KeyVaultTransport.ProtobufWithApiFallback` | Try Protobuf first; on failure, retry with JSON |

```js
const client = new KeyVaultClient({
  baseUrl: 'https://localhost:5001',
  apiKey: 'your-api-key',
  transport: KeyVaultTransport.ProtobufWithApiFallback,
});
```

## Environments

An empty string (`''`) represents the **Global** environment. Keys in Global act as fallback defaults when a key is not found in a specific environment.

```js
// Global (default)
const client = new KeyVaultClient({ baseUrl, apiKey });

// Environment-specific
const client = new KeyVaultClient({
  baseUrl,
  apiKey,
  defaultEnvironment: 'Production',
});

// Override per-call
await client.getText('SmtpHost', { environment: 'Staging' });
```

## Core API Methods

These methods map directly to the Tau Key Vault REST API endpoints.

### Keys

```js
// List all keys (with global fallback)
const { items } = await client.getAllKeys({ environment: 'Production' });

// Every key in every environment — the raw dump clients use to build a local cache.
const everything = await client.getAllKeysAllEnvironments();

// Get a single key
const entry = await client.getKey('SmtpHost', { environment: 'Production' });

// Create or update a key
await client.upsertKey('SmtpHost', 'mail.example.com', {
  environment: 'Production',
  dataType: 'Text',
  isSensitive: false,
});
```

### Deleting a Key

```js
// Delete one key from one environment.
const deleted = await client.deleteKey('ConnectionString', { environment: 'PRODUCTION' });
console.log(`${deleted.key} removed from ${deleted.environment}`);

// Unlike a get, a delete never falls back to Global: if the key exists only globally,
// this rejects with a 404 rather than deleting it. Pass an empty environment to
// delete the global entry itself.
await client.deleteKey('ConnectionString', { environment: '' });
```

### Environments

```js
const { environments } = await client.getEnvironments();

await client.deleteEnvironment('OldEnv');

await client.renameEnvironment('Staging', 'QA');
```

### Export / Import

```js
// Export
const payload = await client.export({ environment: 'Production' });

// Import
await client.import({
  environment: 'Staging',
  mode: 'merge',
  keys: [
    { key: 'SmtpHost', value: 'smtp.test.com', dataType: 'Text', isSensitive: false },
  ],
});
```

### Proto Schema

```js
const protoFile = await client.getProtoSchema();
```

## Key Exists

```js
const exists = await client.keyExists('SmtpHost', { environment: 'Production' });
```

Returns `true` if the key is found; `false` on 404. Other errors are re-thrown.

## Typed Get Helpers

Each method returns the value parsed to the appropriate JavaScript type.

| Method | Returns |
|--------|---------|
| `getText(key, opts?)` | `string` |
| `getCode(key, opts?)` | `string` (uppercase) |
| `getNumeric(key, opts?)` | `number` |
| `getBoolean(key, opts?)` | `boolean` |
| `getDate(key, opts?)` | `string` (YYYY-MM-DD) |
| `getTime(key, opts?)` | `string` (HH:mm:ss) |
| `getDateTime(key, opts?)` | `string` (ISO 8601) |
| `getJson(key, opts?)` | `T` (parsed JSON) |
| `getCsv(key, opts?)` | `string[]` |

```js
const port = await client.getNumeric('SmtpPort');
const debug = await client.getBoolean('DebugMode');
const tags = await client.getCsv('AllowedTags');
const config = await client.getJson('AppConfig');
```

## Typed Update Helpers

| Method | Value Type |
|--------|-----------|
| `updateText(key, value, opts?)` | `string` |
| `updateCode(key, value, opts?)` | `string` (auto-uppercased) |
| `updateNumeric(key, value, opts?)` | `number` |
| `updateBoolean(key, value, opts?)` | `boolean` |
| `updateDate(key, value, opts?)` | `string` (YYYY-MM-DD) |
| `updateTime(key, value, opts?)` | `string` (HH:mm:ss) |
| `updateDateTime(key, value, opts?)` | `string` (ISO 8601) |
| `updateJson(key, value, opts?)` | `object` (serialized) |
| `updateCsv(key, values, opts?)` | `string[]` |

```js
await client.updateNumeric('SmtpPort', 587, { environment: 'Production' });
await client.updateBoolean('DebugMode', false);
await client.updateCode('CountryCode', 'za'); // stored as "ZA"
```

## GetOrCreate Pattern

These methods get a key's typed value, creating it with the provided default if the key doesn't exist. The optional `isSensitive` flag is only used when creating.

| Method | Default Type | Returns |
|--------|-------------|---------|
| `getOrCreateText(key, default, opts?)` | `string` | `string` |
| `getOrCreateCode(key, default, opts?)` | `string` | `string` |
| `getOrCreateNumeric(key, default, opts?)` | `number` | `number` |
| `getOrCreateBoolean(key, default, opts?)` | `boolean` | `boolean` |
| `getOrCreateDate(key, default, opts?)` | `string` | `string` |
| `getOrCreateTime(key, default, opts?)` | `string` | `string` |
| `getOrCreateDateTime(key, default, opts?)` | `string` | `string` |
| `getOrCreateJson(key, default, opts?)` | `T` | `T` |
| `getOrCreateCsv(key, default, opts?)` | `string[]` | `string[]` |

```js
// Returns existing value or creates with default
const port = await client.getOrCreateNumeric('SmtpPort', 25, {
  environment: 'Production',
});

const apiKey = await client.getOrCreateText('ExternalApiKey', 'change-me', {
  isSensitive: true,
});

const config = await client.getOrCreateJson('Defaults', { retries: 3, timeout: 5000 });
```

## CSV List Management

Convenience methods for managing comma-separated list values.

```js
// Add an item (creates key if missing)
const list = await client.csvAdd('AllowedOrigins', 'https://app.example.com');

// Remove an item
await client.csvRemove('AllowedOrigins', 'https://old.example.com');

// Check membership
const has = await client.csvContains('AllowedOrigins', 'https://app.example.com');

// Replace an item
await client.csvReplace('AllowedOrigins', 'https://old.example.com', 'https://new.example.com');
```

## Access Audit Log

Every read, write and delete is recorded server-side. Values are never recorded, so
nothing here can leak a secret.

```js
import { KeyVaultAuditAction, KeyVaultAuditOutcome } from 'tau-keyvault-client';

// Everything that happened to one key — the erasure-evidence question.
const trail = await client.getKeyAuditTrail('SubjectEmail');
for (const row of trail.items) {
  console.log(`${row.timestamp} ${row.action} ${row.actorType}:${row.actorId} ${row.outcome}`);
}

// Was a specific key actually destroyed?
const erased = await client.getAuditLog({
  key: 'SubjectEmail',
  action: KeyVaultAuditAction.DeleteKey,
});

// What has one credential been doing this week?
const byCredential = await client.getAuditLog({
  actorId: 'adapter-prod',
  from: new Date(Date.now() - 7 * 24 * 3600 * 1000),
  limit: 500,
});

// Rejected credentials — the signal for a leaked key.
const denied = await client.getAuditLog({
  action: KeyVaultAuditAction.AuthFailure,
  outcome: KeyVaultAuditOutcome.Denied,
});
console.log(`${denied.totalCount} rejected attempts`);
```

## Per-Environment API Credentials

Requires a Global API key, and `EnableAPIKeyPerEnvironment` on the server. A credential
bound to an environment sees only that environment, with no Global fallback.

```js
// Mint a credential. The secret is returned once and is never retrievable again.
const minted = await client.createApiKey('adapter-prod', 'PRODUCTION');
console.log(`Store this now: ${minted.key}`);

// List them — metadata only, never the secret.
const { items } = await client.listApiKeys();
items.forEach(k => console.log(`${k.id} ${k.name} -> ${k.environment} (enabled: ${k.enabled})`));

// Rotate: the previous secret stops working immediately.
const rotated = await client.rotateApiKey(minted.id);
console.log(`New secret: ${rotated.key}`);

// Suspend without deleting, so the audit trail keeps naming its subject.
await client.setApiKeyEnabled(minted.id, false);

// Or remove it permanently.
await client.revokeApiKey(minted.id);
```

## Publishing

`sample_publish-npm.sh` builds and publishes the package. Copy it to the un-prefixed names —
`publish-npm.sh`, `npm_version.txt` — which are gitignored, then set your registry and token:

```bash
cp sample_publish-npm.sh publish-npm.sh
cp sample_npm_version.txt npm_version.txt

# inspect the tarball without publishing
NPM_TOKEN=xxxx ./publish-npm.sh -n

# publish
NPM_TOKEN=xxxx ./publish-npm.sh -r "https://registry.npmjs.org" -a public
```

It increments the patch in the version file, syncs `package.json` to match, then publishes.
Auth is written to a temporary `.npmrc` that is removed on exit, so the token never has to
live in a committed file.

## Error Handling

All API errors throw a `KeyVaultApiError` with `statusCode` and `apiError` properties.

```js
import { KeyVaultApiError } from 'tau-keyvault-client';

try {
  await client.getKey('MissingKey');
} catch (err) {
  if (err instanceof KeyVaultApiError) {
    console.log(err.statusCode); // 404
    console.log(err.apiError);   // "Key 'MissingKey' not found ..."
  }
}
```

## AbortController Support

All methods accept an `AbortSignal` via the `signal` option for cancellation.

```js
const controller = new AbortController();
setTimeout(() => controller.abort(), 5000);

const entry = await client.getKey('SmtpHost', { signal: controller.signal });
```

## Custom Fetch

You can inject a custom `fetch` implementation (useful for Node.js < 18 or testing).

```js
import nodeFetch from 'node-fetch';

const client = new KeyVaultClient({
  baseUrl: 'https://localhost:5001',
  apiKey: 'your-api-key',
  fetch: nodeFetch,
});
```

## TypeScript

The package includes full TypeScript declarations. Import types directly:

```ts
import {
  KeyVaultClient,
  KeyVaultTransport,
  KeyVaultDataType,
  KeyVaultApiError,
  type KeyEntryResponse,
  type KeyVaultClientOptions,
} from 'tau-keyvault-client';
```

## API Reference

### Core Methods

| Method | Description |
|--------|-------------|
| `getAllKeys(opts?)` | List all keys for an environment |
| `getAllKeysAllEnvironments(opts?)` | List all keys across every environment (no filtering) |
| `getKey(key, opts?)` | Get a single key by name |
| `upsertKey(key, value, opts?)` | Create or update a key |
| `getEnvironments(opts?)` | List all environments |
| `deleteKey(key, opts?)` | Delete a single key from one environment (no global fallback) |
| `getAuditLog(opts?)` | Query the access audit log (filters: key, environment, actorId, action, outcome, from, to) |
| `getKeyAuditTrail(key, opts?)` | Every audit row for one key, including its erasure evidence |
| `listApiKeys(opts?)` | List per-environment credentials (Global only; secrets never returned) |
| `createApiKey(name, env, opts?)` | Mint a credential bound to one environment; the key is returned once |
| `rotateApiKey(id, opts?)` | Replace a credential's secret; the previous key stops working immediately |
| `setApiKeyEnabled(id, enabled, opts?)` | Suspend or resume a credential without deleting it |
| `revokeApiKey(id, opts?)` | Permanently remove a credential |
| `deleteEnvironment(env, opts?)` | Delete an environment and its keys |
| `renameEnvironment(env, newName, opts?)` | Rename an environment |
| `export(opts?)` | Export all keys for an environment |
| `import(request, opts?)` | Import keys into an environment |
| `getProtoSchema(opts?)` | Download the .proto schema |
| `keyExists(key, opts?)` | Check if a key exists |

### Typed Helpers

Nine data types, each with `get*`, `update*`, and `getOrCreate*` variants.

### CSV Helpers

| Method | Description |
|--------|-------------|
| `csvAdd(key, item, opts?)` | Add an item to a CSV list |
| `csvRemove(key, item, opts?)` | Remove first occurrence from a CSV list |
| `csvContains(key, item, opts?)` | Check if a CSV list contains an item |
| `csvReplace(key, old, new, opts?)` | Replace all occurrences in a CSV list |

## License

UNLICENSED
