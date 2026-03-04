/**
 * JavaScript client for the Tau Key Vault REST API.
 * Supports JSON and Protocol Buffers transport with typed helper methods
 * for all key-value data types.
 *
 * @module KeyVaultClient
 */
import { KeyVaultTransport, KeyVaultDataType } from './enums.js';
import { KeyVaultApiError } from './KeyVaultApiError.js';
import { Proto } from './proto.js';

// ─────────────────────────────────────────────────────────
//  Helpers
// ─────────────────────────────────────────────────────────

/**
 * Parse a CSV string into a trimmed, non-empty list.
 * @param {string} value
 * @returns {string[]}
 */
function parseCsv(value) {
  if (!value || !value.trim()) return [];
  return value.split(',').map(s => s.trim()).filter(Boolean);
}

/**
 * @typedef {Object} KeyVaultClientOptions
 * @property {string} baseUrl         Base URL of the Tau Key Vault server (e.g. "https://localhost:5001")
 * @property {string} apiKey          API key for authentication (X-Api-Key header)
 * @property {string} [defaultEnvironment='']  Default environment (blank = Global)
 * @property {string} [transport='Api']  Transport mode: 'Api' | 'Protobuf' | 'ProtobufWithApiFallback'
 * @property {number} [timeout=30000]  Request timeout in milliseconds
 * @property {typeof fetch} [fetch]    Custom fetch implementation (defaults to globalThis.fetch)
 */

/**
 * @typedef {Object} KeyEntryResponse
 * @property {string} key
 * @property {string} value
 * @property {string} environment
 * @property {string} dataType
 * @property {boolean} isSensitive
 * @property {string|null} updatedAt
 */

/**
 * Client for the Tau Key Vault REST API.
 */
export class KeyVaultClient {
  /**
   * @param {KeyVaultClientOptions} options
   */
  constructor(options) {
    if (!options) throw new Error('KeyVaultClient options are required.');
    if (!options.baseUrl) throw new Error('options.baseUrl is required.');
    if (!options.apiKey) throw new Error('options.apiKey is required.');

    /** @type {string} */
    this._baseUrl = options.baseUrl.replace(/\/+$/, '');
    /** @type {string} */
    this._apiKey = options.apiKey;
    /** @type {string} */
    this._defaultEnvironment = options.defaultEnvironment ?? '';
    /** @type {string} */
    this._transport = options.transport ?? KeyVaultTransport.Api;
    /** @type {number} */
    this._timeout = options.timeout ?? 30_000;
    /** @type {typeof fetch} */
    this._fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
  }

  // ═══════════════════════════════════════════════════════════
  //  CORE API METHODS (Swagger endpoints)
  // ═══════════════════════════════════════════════════════════

  // ── Keys ───────────────────────────────────────────────

  /**
   * List all keys for an environment with global fallback.
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.raw=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<{items: KeyEntryResponse[]}>}
   */
  async getAllKeys({ environment, raw = false, signal } = {}) {
    const env = environment ?? this._defaultEnvironment;
    const url = `api/keys?environment=${enc(env)}&raw=${raw}`;
    return this._sendGet(url, Proto.KeyEntryListResponse, signal);
  }

  /**
   * Get a single key by name with global fallback.
   * @param {string} key
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<KeyEntryResponse>}
   */
  async getKey(key, { environment, signal } = {}) {
    const env = environment ?? this._defaultEnvironment;
    const url = `api/keys/${enc(key)}?environment=${enc(env)}`;
    return this._sendGet(url, Proto.KeyEntryResponse, signal);
  }

  /**
   * Create or update a key-value pair.
   * @param {string} key
   * @param {string} value
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {string} [opts.dataType='Text']
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<KeyEntryResponse>}
   */
  async upsertKey(key, value, { environment, dataType = 'Text', isSensitive = false, signal } = {}) {
    const env = environment ?? this._defaultEnvironment;
    const body = { key, value, environment: env, dataType, isSensitive };
    return this._sendPut('api/keys', body, Proto.UpsertRequest, Proto.KeyEntryResponse, signal);
  }

  // ── Environments ───────────────────────────────────────

  /**
   * List all known environments.
   * @param {object} [opts]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<{environments: string[]}>}
   */
  async getEnvironments({ signal } = {}) {
    return this._sendGet('api/keys/environments', Proto.EnvironmentListResponse, signal);
  }

  /**
   * Delete an environment and all its keys.
   * @param {string} environment
   * @param {object} [opts]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<{message: string, deletedKeys: number}>}
   */
  async deleteEnvironment(environment, { signal } = {}) {
    const url = `api/keys/environments/${enc(environment)}`;
    return this._sendDelete(url, Proto.DeleteEnvironmentResponse, signal);
  }

  /**
   * Rename an environment.
   * @param {string} environment
   * @param {string} newName
   * @param {object} [opts]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<{message: string, updatedKeys: number}>}
   */
  async renameEnvironment(environment, newName, { signal } = {}) {
    const url = `api/keys/environments/${enc(environment)}/rename`;
    const body = { newName };
    return this._sendPut(url, body, Proto.RenameRequest, Proto.RenameEnvironmentResponse, signal);
  }

  // ── Export / Import ────────────────────────────────────

  /**
   * Export all keys for an environment.
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<Object>}
   */
  async export({ environment, signal } = {}) {
    const env = environment ?? this._defaultEnvironment;
    return this._sendGet(`api/keys/export?environment=${enc(env)}`, Proto.ExportPayloadResponse, signal);
  }

  /**
   * Import keys into an environment.
   * @param {object} request
   * @param {string} [request.environment]
   * @param {string} request.mode  'merge' | 'overwrite'
   * @param {Array<{key:string, value:string, dataType:string, isSensitive:boolean}>} request.keys
   * @param {object} [opts]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<{imported: number, skipped: number, message: string}>}
   */
  async import(request, { signal } = {}) {
    return this._sendPost('api/keys/import', request, Proto.ImportRequest, Proto.ImportResultResponse, signal);
  }

  /**
   * Download the auto-generated .proto schema file as a string.
   * @param {object} [opts]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<string>}
   */
  async getProtoSchema({ signal } = {}) {
    const res = await this._rawFetch('api/keys/proto', {
      method: 'GET',
      headers: { 'Accept': 'text/plain' },
      signal,
    });
    if (!res.ok) await this._throwApiError(res);
    return res.text();
  }

  // ═══════════════════════════════════════════════════════════
  //  KEY EXISTS
  // ═══════════════════════════════════════════════════════════

  /**
   * Checks if a key exists in the specified environment (with global fallback).
   * @param {string} key
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<boolean>}
   */
  async keyExists(key, opts = {}) {
    try {
      await this.getKey(key, opts);
      return true;
    } catch (err) {
      if (err instanceof KeyVaultApiError && err.statusCode === 404) return false;
      throw err;
    }
  }

  // ═══════════════════════════════════════════════════════════
  //  GET OR CREATE (typed, creates with default if missing)
  // ═══════════════════════════════════════════════════════════

  /**
   * Gets a Text value, creating the key with the default if it does not exist.
   * @param {string} key
   * @param {string} defaultValue
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<string>}
   */
  async getOrCreateText(key, defaultValue, opts = {}) {
    return this._getOrCreateRaw(key, defaultValue, KeyVaultDataType.Text, opts);
  }

  /**
   * Gets a Code value (always uppercase), creating the key with the default if it does not exist.
   * @param {string} key
   * @param {string} defaultValue
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<string>}
   */
  async getOrCreateCode(key, defaultValue, opts = {}) {
    return this._getOrCreateRaw(key, defaultValue.toUpperCase(), KeyVaultDataType.Code, opts);
  }

  /**
   * Gets a Numeric value, creating the key with the default if it does not exist.
   * @param {string} key
   * @param {number} defaultValue
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<number>}
   */
  async getOrCreateNumeric(key, defaultValue, opts = {}) {
    const raw = await this._getOrCreateRaw(key, String(defaultValue), KeyVaultDataType.Numeric, opts);
    return Number(raw);
  }

  /**
   * Gets a Boolean value, creating the key with the default if it does not exist.
   * @param {string} key
   * @param {boolean} defaultValue
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<boolean>}
   */
  async getOrCreateBoolean(key, defaultValue, opts = {}) {
    const raw = await this._getOrCreateRaw(key, String(defaultValue), KeyVaultDataType.Boolean, opts);
    return ['true', '1', 'yes'].includes(raw.trim().toLowerCase());
  }

  /**
   * Gets a Date value (YYYY-MM-DD string), creating the key with the default if it does not exist.
   * @param {string} key
   * @param {string} defaultValue  ISO date string (YYYY-MM-DD)
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<string>}
   */
  async getOrCreateDate(key, defaultValue, opts = {}) {
    return this._getOrCreateRaw(key, defaultValue, KeyVaultDataType.Date, opts);
  }

  /**
   * Gets a Time value (HH:mm:ss string), creating the key with the default if it does not exist.
   * @param {string} key
   * @param {string} defaultValue  Time string (HH:mm:ss)
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<string>}
   */
  async getOrCreateTime(key, defaultValue, opts = {}) {
    return this._getOrCreateRaw(key, defaultValue, KeyVaultDataType.Time, opts);
  }

  /**
   * Gets a DateTime value (ISO string), creating the key with the default if it does not exist.
   * @param {string} key
   * @param {string} defaultValue  ISO datetime string (YYYY-MM-DDTHH:mm:ss)
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<string>}
   */
  async getOrCreateDateTime(key, defaultValue, opts = {}) {
    return this._getOrCreateRaw(key, defaultValue, KeyVaultDataType.DateTime, opts);
  }

  /**
   * Gets a JSON value as a parsed object, creating the key with the
   * serialized default if it does not exist.
   * @param {string} key
   * @param {*} defaultValue  Will be JSON.stringify'd
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<*>}
   */
  async getOrCreateJson(key, defaultValue, opts = {}) {
    const raw = await this._getOrCreateRaw(key, JSON.stringify(defaultValue), KeyVaultDataType.Json, opts);
    return JSON.parse(raw);
  }

  /**
   * Gets a CSV value as an array of strings, creating the key with the
   * default list if it does not exist.
   * @param {string} key
   * @param {string[]} defaultValues
   * @param {object} [opts]
   * @param {string} [opts.environment]
   * @param {boolean} [opts.isSensitive=false]
   * @param {AbortSignal} [opts.signal]
   * @returns {Promise<string[]>}
   */
  async getOrCreateCsv(key, defaultValues, opts = {}) {
    const raw = await this._getOrCreateRaw(key, defaultValues.join(','), KeyVaultDataType.Csv, opts);
    return parseCsv(raw);
  }

  // ═══════════════════════════════════════════════════════════
  //  TYPED GET HELPERS
  // ═══════════════════════════════════════════════════════════

  /** Gets a Text value. @returns {Promise<string>} */
  async getText(key, opts = {}) {
    return (await this.getKey(key, opts)).value;
  }

  /** Gets a Code value (uppercase text). @returns {Promise<string>} */
  async getCode(key, opts = {}) {
    return (await this.getKey(key, opts)).value;
  }

  /** Gets a Numeric value. @returns {Promise<number>} */
  async getNumeric(key, opts = {}) {
    return Number((await this.getKey(key, opts)).value);
  }

  /** Gets a Boolean value. @returns {Promise<boolean>} */
  async getBoolean(key, opts = {}) {
    const val = (await this.getKey(key, opts)).value.trim().toLowerCase();
    return ['true', '1', 'yes'].includes(val);
  }

  /** Gets a Date value (YYYY-MM-DD string). @returns {Promise<string>} */
  async getDate(key, opts = {}) {
    return (await this.getKey(key, opts)).value;
  }

  /** Gets a Time value (HH:mm:ss string). @returns {Promise<string>} */
  async getTime(key, opts = {}) {
    return (await this.getKey(key, opts)).value;
  }

  /** Gets a DateTime value (ISO string). @returns {Promise<string>} */
  async getDateTime(key, opts = {}) {
    return (await this.getKey(key, opts)).value;
  }

  /** Gets a JSON value deserialized. @returns {Promise<*>} */
  async getJson(key, opts = {}) {
    return JSON.parse((await this.getKey(key, opts)).value);
  }

  /** Gets a CSV value as an array of strings. @returns {Promise<string[]>} */
  async getCsv(key, opts = {}) {
    return parseCsv((await this.getKey(key, opts)).value);
  }

  // ═══════════════════════════════════════════════════════════
  //  TYPED UPDATE HELPERS
  // ═══════════════════════════════════════════════════════════

  /** Updates a Text value. @returns {Promise<KeyEntryResponse>} */
  async updateText(key, value, opts = {}) {
    return this.upsertKey(key, value, { ...opts, dataType: KeyVaultDataType.Text });
  }

  /** Updates a Code value (stored uppercase). @returns {Promise<KeyEntryResponse>} */
  async updateCode(key, value, opts = {}) {
    return this.upsertKey(key, value.toUpperCase(), { ...opts, dataType: KeyVaultDataType.Code });
  }

  /** Updates a Numeric value. @returns {Promise<KeyEntryResponse>} */
  async updateNumeric(key, value, opts = {}) {
    return this.upsertKey(key, String(value), { ...opts, dataType: KeyVaultDataType.Numeric });
  }

  /** Updates a Boolean value. @returns {Promise<KeyEntryResponse>} */
  async updateBoolean(key, value, opts = {}) {
    return this.upsertKey(key, String(value), { ...opts, dataType: KeyVaultDataType.Boolean });
  }

  /** Updates a Date value. @returns {Promise<KeyEntryResponse>} */
  async updateDate(key, value, opts = {}) {
    return this.upsertKey(key, value, { ...opts, dataType: KeyVaultDataType.Date });
  }

  /** Updates a Time value. @returns {Promise<KeyEntryResponse>} */
  async updateTime(key, value, opts = {}) {
    return this.upsertKey(key, value, { ...opts, dataType: KeyVaultDataType.Time });
  }

  /** Updates a DateTime value. @returns {Promise<KeyEntryResponse>} */
  async updateDateTime(key, value, opts = {}) {
    return this.upsertKey(key, value, { ...opts, dataType: KeyVaultDataType.DateTime });
  }

  /** Updates a JSON value by serializing the object. @returns {Promise<KeyEntryResponse>} */
  async updateJson(key, value, opts = {}) {
    return this.upsertKey(key, JSON.stringify(value), { ...opts, dataType: KeyVaultDataType.Json });
  }

  /** Updates a CSV value from an array of strings. @returns {Promise<KeyEntryResponse>} */
  async updateCsv(key, values, opts = {}) {
    return this.upsertKey(key, values.join(','), { ...opts, dataType: KeyVaultDataType.Csv });
  }

  // ═══════════════════════════════════════════════════════════
  //  CSV LIST MANAGEMENT HELPERS
  // ═══════════════════════════════════════════════════════════

  /**
   * Adds an item to a CSV list key. If the key does not exist, it is created.
   * @returns {Promise<string[]>} the updated list
   */
  async csvAdd(key, item, opts = {}) {
    const list = await this.getOrCreateCsv(key, [], opts);
    list.push(item);
    await this.updateCsv(key, list, opts);
    return list;
  }

  /**
   * Removes the first occurrence of an item from a CSV list key.
   * @returns {Promise<string[]>} the updated list
   */
  async csvRemove(key, item, opts = {}) {
    const list = await this.getCsv(key, opts);
    const idx = list.indexOf(item);
    if (idx !== -1) list.splice(idx, 1);
    await this.updateCsv(key, list, opts);
    return list;
  }

  /**
   * Checks if a CSV list key contains an item.
   * @returns {Promise<boolean>}
   */
  async csvContains(key, item, opts = {}) {
    const list = await this.getCsv(key, opts);
    return list.includes(item);
  }

  /**
   * Replaces all occurrences of oldItem with newItem in a CSV list key.
   * @returns {Promise<string[]>} the updated list
   */
  async csvReplace(key, oldItem, newItem, opts = {}) {
    const list = await this.getCsv(key, opts);
    for (let i = 0; i < list.length; i++) {
      if (list[i] === oldItem) list[i] = newItem;
    }
    await this.updateCsv(key, list, opts);
    return list;
  }

  // ═══════════════════════════════════════════════════════════
  //  INTERNAL: Transport (JSON / Protobuf / Failover)
  // ═══════════════════════════════════════════════════════════

  /**
   * @private
   * GetOrCreate base: tries GET, on 404 creates with PUT.
   */
  async _getOrCreateRaw(key, defaultValue, dataType, opts) {
    try {
      const entry = await this.getKey(key, opts);
      return entry.value;
    } catch (err) {
      if (err instanceof KeyVaultApiError && err.statusCode === 404) {
        const created = await this.upsertKey(key, defaultValue, {
          environment: opts.environment,
          dataType,
          isSensitive: opts.isSensitive ?? false,
          signal: opts.signal,
        });
        return created.value;
      }
      throw err;
    }
  }

  // ── Transport dispatch ─────────────────────────────────

  /** @private */
  async _sendGet(url, protoType, signal) {
    switch (this._transport) {
      case KeyVaultTransport.Protobuf:
        return this._execGet(url, protoType, true, signal);
      case KeyVaultTransport.ProtobufWithApiFallback:
        try { return await this._execGet(url, protoType, true, signal); }
        catch { return this._execGet(url, protoType, false, signal); }
      default:
        return this._execGet(url, protoType, false, signal);
    }
  }

  /** @private */
  async _sendPut(url, body, protoReqType, protoResType, signal) {
    switch (this._transport) {
      case KeyVaultTransport.Protobuf:
        return this._execPut(url, body, protoReqType, protoResType, true, signal);
      case KeyVaultTransport.ProtobufWithApiFallback:
        try { return await this._execPut(url, body, protoReqType, protoResType, true, signal); }
        catch { return this._execPut(url, body, protoReqType, protoResType, false, signal); }
      default:
        return this._execPut(url, body, protoReqType, protoResType, false, signal);
    }
  }

  /** @private */
  async _sendPost(url, body, protoReqType, protoResType, signal) {
    switch (this._transport) {
      case KeyVaultTransport.Protobuf:
        return this._execPost(url, body, protoReqType, protoResType, true, signal);
      case KeyVaultTransport.ProtobufWithApiFallback:
        try { return await this._execPost(url, body, protoReqType, protoResType, true, signal); }
        catch { return this._execPost(url, body, protoReqType, protoResType, false, signal); }
      default:
        return this._execPost(url, body, protoReqType, protoResType, false, signal);
    }
  }

  /** @private */
  async _sendDelete(url, protoType, signal) {
    switch (this._transport) {
      case KeyVaultTransport.Protobuf:
        return this._execDelete(url, protoType, true, signal);
      case KeyVaultTransport.ProtobufWithApiFallback:
        try { return await this._execDelete(url, protoType, true, signal); }
        catch { return this._execDelete(url, protoType, false, signal); }
      default:
        return this._execDelete(url, protoType, false, signal);
    }
  }

  // ── Core HTTP executors ────────────────────────────────

  /** @private */
  async _execGet(url, protoType, useProtobuf, signal) {
    const res = await this._rawFetch(url, {
      method: 'GET',
      headers: { 'Accept': useProtobuf ? 'application/x-protobuf' : 'application/json' },
      signal,
    });
    return this._handleResponse(res, protoType, useProtobuf);
  }

  /** @private */
  async _execPut(url, body, protoReqType, protoResType, useProtobuf, signal) {
    const res = await this._rawFetch(url, {
      method: 'PUT',
      headers: {
        'Accept': useProtobuf ? 'application/x-protobuf' : 'application/json',
        'Content-Type': useProtobuf ? 'application/x-protobuf' : 'application/json',
      },
      body: useProtobuf ? Proto.encode(protoReqType, body) : JSON.stringify(body),
      signal,
    });
    return this._handleResponse(res, protoResType, useProtobuf);
  }

  /** @private */
  async _execPost(url, body, protoReqType, protoResType, useProtobuf, signal) {
    const res = await this._rawFetch(url, {
      method: 'POST',
      headers: {
        'Accept': useProtobuf ? 'application/x-protobuf' : 'application/json',
        'Content-Type': useProtobuf ? 'application/x-protobuf' : 'application/json',
      },
      body: useProtobuf ? Proto.encode(protoReqType, body) : JSON.stringify(body),
      signal,
    });
    return this._handleResponse(res, protoResType, useProtobuf);
  }

  /** @private */
  async _execDelete(url, protoType, useProtobuf, signal) {
    const res = await this._rawFetch(url, {
      method: 'DELETE',
      headers: { 'Accept': useProtobuf ? 'application/x-protobuf' : 'application/json' },
      signal,
    });
    return this._handleResponse(res, protoType, useProtobuf);
  }

  // ── Fetch wrapper ──────────────────────────────────────

  /** @private */
  async _rawFetch(relativeUrl, init) {
    const url = `${this._baseUrl}/${relativeUrl}`;
    const headers = { ...init.headers, 'X-Api-Key': this._apiKey };

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this._timeout);

    // Combine user-provided signal with timeout
    if (init.signal) {
      init.signal.addEventListener('abort', () => controller.abort());
    }

    try {
      return await this._fetch(url, { ...init, headers, signal: controller.signal });
    } finally {
      clearTimeout(timeoutId);
    }
  }

  // ── Response handling ──────────────────────────────────

  /** @private */
  async _handleResponse(res, protoType, useProtobuf) {
    if (!res.ok) await this._throwApiError(res);

    if (useProtobuf) {
      const buf = new Uint8Array(await res.arrayBuffer());
      return Proto.decode(protoType, buf);
    } else {
      return res.json();
    }
  }

  /** @private */
  async _throwApiError(res) {
    let apiError = null;
    try {
      const body = await res.json();
      apiError = body.error || null;
    } catch { /* ignore parse errors */ }

    throw new KeyVaultApiError(
      res.status,
      apiError,
      apiError ?? `HTTP ${res.status} ${res.statusText}`,
    );
  }
}

// ── Utility ──────────────────────────────────────────────
function enc(s) { return encodeURIComponent(s); }
