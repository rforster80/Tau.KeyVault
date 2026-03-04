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
