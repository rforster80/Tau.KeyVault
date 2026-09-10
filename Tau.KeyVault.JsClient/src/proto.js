/**
 * Protobuf codec — runtime-defined message types that are wire-compatible
 * with the server's [ProtoContract] / [ProtoMember] annotations (protobuf-net).
 *
 * protobuf-net uses field numbers from [ProtoMember(n)] and proto3 semantics.
 * DateTime is serialized by protobuf-net as google.protobuf.Timestamp (seconds + nanos)
 * wrapped inside a bcl.DateTime surrogate. For simplicity we treat DateTime
 * fields as int64 ticks on the wire (protobuf-net's default for DateTime)
 * and convert in the codec.
 *
 * NOTE: The server's protobuf-net serializes DateTime as a nested Timestamp
 * with field 1 = int64 (value) and field 2 = int32 (scale) using
 * the bcl.DateTime format. We replicate that here.
 */
import protobuf from 'protobufjs';

const { Root, Type, Field, Enum: PbEnum } = protobuf;

const root = new Root();

// ── bcl.DateTime (protobuf-net's internal DateTime wire format) ────
const bclDateTime = new Type('bcl_DateTime')
  .add(new Field('value', 1, 'sint64'))   // ticks relative to epoch
  .add(new Field('scale', 2, 'int32'));   // TimeSpanScale enum
root.add(bclDateTime);

// ── Response messages ──────────────────────────────────────────────

const KeyEntryResponse = new Type('KeyEntryResponse')
  .add(new Field('key', 1, 'string'))
  .add(new Field('value', 2, 'string'))
  .add(new Field('environment', 3, 'string'))
  .add(new Field('dataType', 4, 'string'))
  .add(new Field('isSensitive', 5, 'bool'))
  .add(new Field('updatedAt', 6, 'bcl_DateTime'));
root.add(KeyEntryResponse);

const KeyEntryListResponse = new Type('KeyEntryListResponse')
  .add(new Field('items', 1, 'KeyEntryResponse', 'repeated'));
root.add(KeyEntryListResponse);

const EnvironmentListResponse = new Type('EnvironmentListResponse')
  .add(new Field('environments', 1, 'string', 'repeated'));
root.add(EnvironmentListResponse);

const DeleteEnvironmentResponse = new Type('DeleteEnvironmentResponse')
  .add(new Field('message', 1, 'string'))
  .add(new Field('deletedKeys', 2, 'int32'));
root.add(DeleteEnvironmentResponse);

const ApiKeyResponse = new Type('ApiKeyResponse')
  .add(new Field('id', 1, 'int32'))
  .add(new Field('name', 2, 'string'))
  .add(new Field('environment', 3, 'string'))
  .add(new Field('enabled', 4, 'bool'))
  .add(new Field('createdAt', 5, 'bcl_DateTime'))
  .add(new Field('lastRotatedAt', 6, 'bcl_DateTime'));
root.add(ApiKeyResponse);

const ApiKeyListResponse = new Type('ApiKeyListResponse')
  .add(new Field('items', 1, 'ApiKeyResponse', 'repeated'));
root.add(ApiKeyListResponse);

// The only message that ever carries a credential secret, and only on create/rotate.
const ApiKeySecretResponse = new Type('ApiKeySecretResponse')
  .add(new Field('id', 1, 'int32'))
  .add(new Field('name', 2, 'string'))
  .add(new Field('environment', 3, 'string'))
  .add(new Field('key', 4, 'string'))
  .add(new Field('message', 5, 'string'));
root.add(ApiKeySecretResponse);

const RevokeApiKeyResponse = new Type('RevokeApiKeyResponse')
  .add(new Field('message', 1, 'string'))
  .add(new Field('id', 2, 'int32'));
root.add(RevokeApiKeyResponse);

const CreateApiKeyRequest = new Type('CreateApiKeyRequest')
  .add(new Field('name', 1, 'string'))
  .add(new Field('environment', 2, 'string'));
root.add(CreateApiKeyRequest);

const UpdateApiKeyRequest = new Type('UpdateApiKeyRequest')
  .add(new Field('enabled', 1, 'bool'));
root.add(UpdateApiKeyRequest);

// Audit rows carry no value field, by design.
// timestamp is protobuf-net's bcl.DateTime surrogate; like updatedAt elsewhere in this
// codec it decodes to an opaque object, so prefer the Api transport when you need the
// timestamp as a string.
const AuditEntryResponse = new Type('AuditEntryResponse')
  .add(new Field('timestamp', 1, 'bcl_DateTime'))
  .add(new Field('action', 2, 'string'))
  .add(new Field('key', 3, 'string'))
  .add(new Field('environment', 4, 'string'))
  .add(new Field('actorType', 5, 'string'))
  .add(new Field('actorId', 6, 'string'))
  .add(new Field('outcome', 7, 'string'))
  .add(new Field('ipAddress', 8, 'string'))
  .add(new Field('itemCount', 9, 'int32'))
  .add(new Field('id', 10, 'int32'));
root.add(AuditEntryResponse);

const AuditEntryListResponse = new Type('AuditEntryListResponse')
  .add(new Field('items', 1, 'AuditEntryResponse', 'repeated'))
  .add(new Field('totalCount', 2, 'int32'))
  .add(new Field('limit', 3, 'int32'))
  .add(new Field('offset', 4, 'int32'));
root.add(AuditEntryListResponse);

const DeleteKeyResponse = new Type('DeleteKeyResponse')
  .add(new Field('message', 1, 'string'))
  .add(new Field('key', 2, 'string'))
  .add(new Field('environment', 3, 'string'));
root.add(DeleteKeyResponse);

const RenameEnvironmentResponse = new Type('RenameEnvironmentResponse')
  .add(new Field('message', 1, 'string'))
  .add(new Field('updatedKeys', 2, 'int32'));
root.add(RenameEnvironmentResponse);

const ExportKeyItemResponse = new Type('ExportKeyItemResponse')
  .add(new Field('key', 1, 'string'))
  .add(new Field('value', 2, 'string'))
  .add(new Field('dataType', 3, 'string'))
  .add(new Field('isSensitive', 4, 'bool'));
root.add(ExportKeyItemResponse);

const ExportPayloadResponse = new Type('ExportPayloadResponse')
  .add(new Field('version', 1, 'string'))
  .add(new Field('exportDate', 2, 'bcl_DateTime'))
  .add(new Field('environment', 3, 'string'))
  .add(new Field('keyCount', 4, 'int32'))
  .add(new Field('keys', 5, 'ExportKeyItemResponse', 'repeated'));
root.add(ExportPayloadResponse);

const ImportResultResponse = new Type('ImportResultResponse')
  .add(new Field('imported', 1, 'int32'))
  .add(new Field('skipped', 2, 'int32'))
  .add(new Field('message', 3, 'string'));
root.add(ImportResultResponse);

const ErrorResponse = new Type('ErrorResponse')
  .add(new Field('error', 1, 'string'));
root.add(ErrorResponse);

// ── Request messages ───────────────────────────────────────────────

const UpsertRequest = new Type('UpsertRequest')
  .add(new Field('key', 1, 'string'))
  .add(new Field('value', 2, 'string'))
  .add(new Field('environment', 3, 'string'))
  .add(new Field('dataType', 4, 'string'))
  .add(new Field('isSensitive', 5, 'bool'));
root.add(UpsertRequest);

const RenameRequest = new Type('RenameRequest')
  .add(new Field('newName', 1, 'string'));
root.add(RenameRequest);

const ImportKeyItem = new Type('ImportKeyItem')
  .add(new Field('key', 1, 'string'))
  .add(new Field('value', 2, 'string'))
  .add(new Field('dataType', 3, 'string'))
  .add(new Field('isSensitive', 4, 'bool'));
root.add(ImportKeyItem);

const ImportRequest = new Type('ImportRequest')
  .add(new Field('environment', 1, 'string'))
  .add(new Field('mode', 2, 'string'))
  .add(new Field('keys', 3, 'ImportKeyItem', 'repeated'));
root.add(ImportRequest);

// ── Resolve all types ──────────────────────────────────────────────
root.resolveAll();

// ── Public codec ───────────────────────────────────────────────────

export const Proto = {
  KeyEntryResponse,
  KeyEntryListResponse,
  EnvironmentListResponse,
  DeleteEnvironmentResponse,
  DeleteKeyResponse,
  AuditEntryResponse,
  AuditEntryListResponse,
  ApiKeyResponse,
  ApiKeyListResponse,
  ApiKeySecretResponse,
  RevokeApiKeyResponse,
  CreateApiKeyRequest,
  UpdateApiKeyRequest,
  RenameEnvironmentResponse,
  ExportPayloadResponse,
  ExportKeyItemResponse,
  ImportResultResponse,
  ErrorResponse,
  UpsertRequest,
  RenameRequest,
  ImportRequest,
  ImportKeyItem,

  /**
   * Encode a message to a Uint8Array.
   * @param {protobuf.Type} type
   * @param {object} payload
   * @returns {Uint8Array}
   */
  encode(type, payload) {
    const msg = type.create(payload);
    return type.encode(msg).finish();
  },

  /**
   * Decode a Uint8Array into a plain object.
   * @param {protobuf.Type} type
   * @param {Uint8Array} buffer
   * @returns {object}
   */
  decode(type, buffer) {
    const msg = type.decode(buffer);
    return type.toObject(msg, { longs: Number, defaults: true });
  },
};
