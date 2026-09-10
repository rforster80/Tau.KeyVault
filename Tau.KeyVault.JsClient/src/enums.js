/**
 * Determines how the client communicates with the Tau Key Vault API.
 * @readonly
 * @enum {string}
 */
export const KeyVaultTransport = Object.freeze({
  /** Use JSON for all requests and responses (default). */
  Api: 'Api',
  /** Use Protocol Buffers for all requests and responses. */
  Protobuf: 'Protobuf',
  /** Attempt Protobuf first; on failure, retry with JSON automatically. */
  ProtobufWithApiFallback: 'ProtobufWithApiFallback',
});

/**
 * Data types supported by Tau Key Vault.
 * @readonly
 * @enum {string}
 */
export const KeyVaultDataType = Object.freeze({
  Text: 'Text',
  Code: 'Code',
  Numeric: 'Numeric',
  Boolean: 'Boolean',
  Date: 'Date',
  Time: 'Time',
  DateTime: 'DateTime',
  Json: 'Json',
  Csv: 'Csv',
});

/** Action recorded in the access audit log. @readonly @enum {string} */
export const KeyVaultAuditAction = Object.freeze({
  ReadKey: 'ReadKey',
  ListKeys: 'ListKeys',
  WriteKey: 'WriteKey',
  DeleteKey: 'DeleteKey',
  ListEnvironments: 'ListEnvironments',
  DeleteEnvironment: 'DeleteEnvironment',
  RenameEnvironment: 'RenameEnvironment',
  Export: 'Export',
  Import: 'Import',
  ReadAudit: 'ReadAudit',
  AuthFailure: 'AuthFailure',
});

/** Result of an audited action. @readonly @enum {string} */
export const KeyVaultAuditOutcome = Object.freeze({
  Success: 'Success',
  NotFound: 'NotFound',
  Denied: 'Denied',
  Error: 'Error',
});
