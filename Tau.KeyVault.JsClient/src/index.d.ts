// ─────────────────────────────────────────────────────────
//  Tau Key Vault — JavaScript Client  (TypeScript declarations)
// ─────────────────────────────────────────────────────────

// ── Enums ────────────────────────────────────────────────

export declare const KeyVaultTransport: {
  readonly Api: 'Api';
  readonly Protobuf: 'Protobuf';
  readonly ProtobufWithApiFallback: 'ProtobufWithApiFallback';
};
export type KeyVaultTransportValue =
  (typeof KeyVaultTransport)[keyof typeof KeyVaultTransport];

export declare const KeyVaultDataType: {
  readonly Text: 'Text';
  readonly Code: 'Code';
  readonly Numeric: 'Numeric';
  readonly Boolean: 'Boolean';
  readonly Date: 'Date';
  readonly Time: 'Time';
  readonly DateTime: 'DateTime';
  readonly Json: 'Json';
  readonly Csv: 'Csv';
};
export type KeyVaultDataTypeValue =
  (typeof KeyVaultDataType)[keyof typeof KeyVaultDataType];

// ── Error ────────────────────────────────────────────────

export declare class KeyVaultApiError extends Error {
  readonly statusCode: number;
  readonly apiError: string | null;
  constructor(statusCode: number, apiError: string | null, message: string);
}

// ── Options ──────────────────────────────────────────────

export interface KeyVaultClientOptions {
  /** Base URL of the Tau Key Vault server (e.g. "https://localhost:5001"). */
  baseUrl: string;
  /** API key for authentication (sent as X-Api-Key header). */
  apiKey: string;
  /** Default environment (blank string = Global). */
  defaultEnvironment?: string;
  /** Transport mode. @default 'Api' */
  transport?: KeyVaultTransportValue;
  /** Request timeout in milliseconds. @default 30000 */
  timeout?: number;
  /** Custom fetch implementation (defaults to globalThis.fetch). */
  fetch?: typeof globalThis.fetch;
}

// ── Response types ───────────────────────────────────────

export interface KeyEntryResponse {
  key: string;
  value: string;
  environment: string;
  dataType: string;
  isSensitive: boolean;
  updatedAt: string | null;
}

export interface KeyEntryListResponse {
  items: KeyEntryResponse[];
}

export interface EnvironmentListResponse {
  environments: string[];
}

export interface DeleteEnvironmentResponse {
  message: string;
  deletedKeys: number;
}

export interface RenameEnvironmentResponse {
  message: string;
  updatedKeys: number;
}

export interface ExportKeyItemResponse {
  key: string;
  value: string;
  dataType: string;
  isSensitive: boolean;
}

export interface ExportPayloadResponse {
  version: string;
  exportDate: string | null;
  environment: string;
  keyCount: number;
  keys: ExportKeyItemResponse[];
}

export interface ImportResultResponse {
  imported: number;
  skipped: number;
  message: string;
}

// ── Request types ────────────────────────────────────────

export interface ImportKeyItem {
  key: string;
  value: string;
  dataType: string;
  isSensitive: boolean;
}

export interface ImportRequest {
  environment?: string;
  mode: string;
  keys: ImportKeyItem[];
}

// ── Common option bags ───────────────────────────────────

export interface EnvOpts {
  environment?: string;
  signal?: AbortSignal;
}

export interface UpsertOpts extends EnvOpts {
  dataType?: KeyVaultDataTypeValue;
  isSensitive?: boolean;
}

export interface SensitiveOpts extends EnvOpts {
  isSensitive?: boolean;
}

export interface SignalOpts {
  signal?: AbortSignal;
}

// ── Client ───────────────────────────────────────────────

export declare class KeyVaultClient {
  constructor(options: KeyVaultClientOptions);

  // ── Core API ─────────────────────────────────────────
  getAllKeys(opts?: EnvOpts & { raw?: boolean }): Promise<KeyEntryListResponse>;
  getKey(key: string, opts?: EnvOpts): Promise<KeyEntryResponse>;
  upsertKey(key: string, value: string, opts?: UpsertOpts): Promise<KeyEntryResponse>;

  getEnvironments(opts?: SignalOpts): Promise<EnvironmentListResponse>;
  deleteEnvironment(environment: string, opts?: SignalOpts): Promise<DeleteEnvironmentResponse>;
  renameEnvironment(environment: string, newName: string, opts?: SignalOpts): Promise<RenameEnvironmentResponse>;

  export(opts?: EnvOpts): Promise<ExportPayloadResponse>;
  import(request: ImportRequest, opts?: SignalOpts): Promise<ImportResultResponse>;
  getProtoSchema(opts?: SignalOpts): Promise<string>;

  // ── Key Exists ───────────────────────────────────────
  keyExists(key: string, opts?: EnvOpts): Promise<boolean>;

  // ── Get Or Create ────────────────────────────────────
  getOrCreateText(key: string, defaultValue: string, opts?: SensitiveOpts): Promise<string>;
  getOrCreateCode(key: string, defaultValue: string, opts?: SensitiveOpts): Promise<string>;
  getOrCreateNumeric(key: string, defaultValue: number, opts?: SensitiveOpts): Promise<number>;
  getOrCreateBoolean(key: string, defaultValue: boolean, opts?: SensitiveOpts): Promise<boolean>;
  getOrCreateDate(key: string, defaultValue: string, opts?: SensitiveOpts): Promise<string>;
  getOrCreateTime(key: string, defaultValue: string, opts?: SensitiveOpts): Promise<string>;
  getOrCreateDateTime(key: string, defaultValue: string, opts?: SensitiveOpts): Promise<string>;
  getOrCreateJson<T = unknown>(key: string, defaultValue: T, opts?: SensitiveOpts): Promise<T>;
  getOrCreateCsv(key: string, defaultValues: string[], opts?: SensitiveOpts): Promise<string[]>;

  // ── Typed Getters ────────────────────────────────────
  getText(key: string, opts?: EnvOpts): Promise<string>;
  getCode(key: string, opts?: EnvOpts): Promise<string>;
  getNumeric(key: string, opts?: EnvOpts): Promise<number>;
  getBoolean(key: string, opts?: EnvOpts): Promise<boolean>;
  getDate(key: string, opts?: EnvOpts): Promise<string>;
  getTime(key: string, opts?: EnvOpts): Promise<string>;
  getDateTime(key: string, opts?: EnvOpts): Promise<string>;
  getJson<T = unknown>(key: string, opts?: EnvOpts): Promise<T>;
  getCsv(key: string, opts?: EnvOpts): Promise<string[]>;

  // ── Typed Updaters ───────────────────────────────────
  updateText(key: string, value: string, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateCode(key: string, value: string, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateNumeric(key: string, value: number, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateBoolean(key: string, value: boolean, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateDate(key: string, value: string, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateTime(key: string, value: string, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateDateTime(key: string, value: string, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateJson<T = unknown>(key: string, value: T, opts?: SensitiveOpts): Promise<KeyEntryResponse>;
  updateCsv(key: string, values: string[], opts?: SensitiveOpts): Promise<KeyEntryResponse>;

  // ── CSV Helpers ──────────────────────────────────────
  csvAdd(key: string, item: string, opts?: SensitiveOpts): Promise<string[]>;
  csvRemove(key: string, item: string, opts?: SensitiveOpts): Promise<string[]>;
  csvContains(key: string, item: string, opts?: EnvOpts): Promise<boolean>;
  csvReplace(key: string, oldItem: string, newItem: string, opts?: SensitiveOpts): Promise<string[]>;
}
