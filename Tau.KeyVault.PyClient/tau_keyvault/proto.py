"""
Protobuf codec — runtime-defined message descriptors that are wire-compatible
with the server's [ProtoContract] / [ProtoMember] annotations (protobuf-net).

protobuf-net uses field numbers from [ProtoMember(n)] and proto3 semantics.
DateTime is serialized as protobuf-net's bcl.DateTime surrogate (sint64 value +
int32 scale). We replicate that here for full wire compatibility.

This module uses the pure-Python protobuf library (google.protobuf) to build
descriptors at runtime — no generated _pb2 files required.
"""

from __future__ import annotations

from google.protobuf import descriptor as _descriptor
from google.protobuf import descriptor_pool as _pool
from google.protobuf import symbol_database as _sym_db
from google.protobuf.internal import decoder as _decoder
from google.protobuf.internal import encoder as _encoder
from google.protobuf import message as _message
from google.protobuf import reflection as _reflection
from google.protobuf import descriptor_pb2

from . import models


# ─────────────────────────────────────────────────────────
#  Build the proto file descriptor at runtime
# ─────────────────────────────────────────────────────────

def _build_file_descriptor() -> descriptor_pb2.FileDescriptorProto:
    """Build a FileDescriptorProto matching the server's wire format."""
    f = descriptor_pb2.FileDescriptorProto()
    f.name = "keyvault.proto"
    f.package = "tau.keyvault"
    f.syntax = "proto3"

    # ── bcl_DateTime (protobuf-net internal) ──
    _add_message(f, "bcl_DateTime", [
        (1, "value", descriptor_pb2.FieldDescriptorProto.TYPE_SINT64),
        (2, "scale", descriptor_pb2.FieldDescriptorProto.TYPE_INT32),
    ])

    # ── Response messages ──

    _add_message(f, "KeyEntryResponse", [
        (1, "key", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "value", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (3, "environment", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (4, "data_type", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (5, "is_sensitive", descriptor_pb2.FieldDescriptorProto.TYPE_BOOL),
        (6, "updated_at", descriptor_pb2.FieldDescriptorProto.TYPE_MESSAGE, "bcl_DateTime"),
    ])

    _add_message(f, "KeyEntryListResponse", [
        (1, "items", descriptor_pb2.FieldDescriptorProto.TYPE_MESSAGE, "KeyEntryResponse",
         descriptor_pb2.FieldDescriptorProto.LABEL_REPEATED),
    ])

    _add_message(f, "EnvironmentListResponse", [
        (1, "environments", descriptor_pb2.FieldDescriptorProto.TYPE_STRING, None,
         descriptor_pb2.FieldDescriptorProto.LABEL_REPEATED),
    ])

    _add_message(f, "DeleteEnvironmentResponse", [
        (1, "message", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "deleted_keys", descriptor_pb2.FieldDescriptorProto.TYPE_INT32),
    ])

    _add_message(f, "RenameEnvironmentResponse", [
        (1, "message", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "updated_keys", descriptor_pb2.FieldDescriptorProto.TYPE_INT32),
    ])

    _add_message(f, "ExportKeyItemResponse", [
        (1, "key", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "value", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (3, "data_type", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (4, "is_sensitive", descriptor_pb2.FieldDescriptorProto.TYPE_BOOL),
    ])

    _add_message(f, "ExportPayloadResponse", [
        (1, "version", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "export_date", descriptor_pb2.FieldDescriptorProto.TYPE_MESSAGE, "bcl_DateTime"),
        (3, "environment", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (4, "key_count", descriptor_pb2.FieldDescriptorProto.TYPE_INT32),
        (5, "keys", descriptor_pb2.FieldDescriptorProto.TYPE_MESSAGE, "ExportKeyItemResponse",
         descriptor_pb2.FieldDescriptorProto.LABEL_REPEATED),
    ])

    _add_message(f, "ImportResultResponse", [
        (1, "imported", descriptor_pb2.FieldDescriptorProto.TYPE_INT32),
        (2, "skipped", descriptor_pb2.FieldDescriptorProto.TYPE_INT32),
        (3, "message", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
    ])

    _add_message(f, "ErrorResponse", [
        (1, "error", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
    ])

    # ── Request messages ──

    _add_message(f, "UpsertRequest", [
        (1, "key", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "value", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (3, "environment", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (4, "data_type", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (5, "is_sensitive", descriptor_pb2.FieldDescriptorProto.TYPE_BOOL),
    ])

    _add_message(f, "RenameRequest", [
        (1, "new_name", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
    ])

    _add_message(f, "ImportKeyItem", [
        (1, "key", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "value", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (3, "data_type", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (4, "is_sensitive", descriptor_pb2.FieldDescriptorProto.TYPE_BOOL),
    ])

    _add_message(f, "ImportRequest", [
        (1, "environment", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (2, "mode", descriptor_pb2.FieldDescriptorProto.TYPE_STRING),
        (3, "keys", descriptor_pb2.FieldDescriptorProto.TYPE_MESSAGE, "ImportKeyItem",
         descriptor_pb2.FieldDescriptorProto.LABEL_REPEATED),
    ])

    return f


def _add_message(file_proto, name, fields):
    """Helper to add a message type to a FileDescriptorProto."""
    msg = file_proto.message_type.add()
    msg.name = name
    for field_def in fields:
        fld = msg.field.add()
        fld.number = field_def[0]
        fld.name = field_def[1]
        fld.type = field_def[2]
        fld.label = descriptor_pb2.FieldDescriptorProto.LABEL_OPTIONAL
        if len(field_def) > 3 and field_def[3] is not None:
            fld.type_name = f".tau.keyvault.{field_def[3]}"
        if len(field_def) > 4:
            fld.label = field_def[4]


# ─────────────────────────────────────────────────────────
#  Register with the pool and build message classes
# ─────────────────────────────────────────────────────────

_file_proto = _build_file_descriptor()
_pool_inst = _pool.Default()
_pool_inst.Add(_file_proto)

_sym = _sym_db.Default()


def _make_class(full_name: str):
    """Create a protobuf message class from a registered descriptor."""
    desc = _pool_inst.FindMessageTypeByName(full_name)
    cls = _reflection.GeneratedProtocolMessageType(
        desc.name,
        (_message.Message,),
        {"DESCRIPTOR": desc, "__module__": __name__},
    )
    _sym.RegisterMessage(cls)
    return cls


_BclDateTime = _make_class("tau.keyvault.bcl_DateTime")

_PbKeyEntryResponse = _make_class("tau.keyvault.KeyEntryResponse")
_PbKeyEntryListResponse = _make_class("tau.keyvault.KeyEntryListResponse")
_PbEnvironmentListResponse = _make_class("tau.keyvault.EnvironmentListResponse")
_PbDeleteEnvironmentResponse = _make_class("tau.keyvault.DeleteEnvironmentResponse")
_PbRenameEnvironmentResponse = _make_class("tau.keyvault.RenameEnvironmentResponse")
_PbExportKeyItemResponse = _make_class("tau.keyvault.ExportKeyItemResponse")
_PbExportPayloadResponse = _make_class("tau.keyvault.ExportPayloadResponse")
_PbImportResultResponse = _make_class("tau.keyvault.ImportResultResponse")
_PbErrorResponse = _make_class("tau.keyvault.ErrorResponse")

_PbUpsertRequest = _make_class("tau.keyvault.UpsertRequest")
_PbRenameRequest = _make_class("tau.keyvault.RenameRequest")
_PbImportKeyItem = _make_class("tau.keyvault.ImportKeyItem")
_PbImportRequest = _make_class("tau.keyvault.ImportRequest")


# ═══════════════════════════════════════════════════════════
#  Public encode / decode helpers
# ═══════════════════════════════════════════════════════════

# Maps model type → (protobuf class, decode function)


def _decode_key_entry(pb) -> models.KeyEntryResponse:
    return models.KeyEntryResponse(
        key=pb.key,
        value=pb.value,
        environment=pb.environment,
        data_type=pb.data_type,
        is_sensitive=pb.is_sensitive,
        updated_at=None,  # bcl.DateTime is opaque; string is preferred via JSON
    )


def decode_key_entry_response(data: bytes) -> models.KeyEntryResponse:
    """Decode a KeyEntryResponse from protobuf bytes."""
    pb = _PbKeyEntryResponse()
    pb.ParseFromString(data)
    return _decode_key_entry(pb)


def decode_key_entry_list_response(data: bytes) -> models.KeyEntryListResponse:
    """Decode a KeyEntryListResponse from protobuf bytes."""
    pb = _PbKeyEntryListResponse()
    pb.ParseFromString(data)
    return models.KeyEntryListResponse(
        items=[_decode_key_entry(item) for item in pb.items],
    )


def decode_environment_list_response(data: bytes) -> models.EnvironmentListResponse:
    pb = _PbEnvironmentListResponse()
    pb.ParseFromString(data)
    return models.EnvironmentListResponse(environments=list(pb.environments))


def decode_delete_environment_response(data: bytes) -> models.DeleteEnvironmentResponse:
    pb = _PbDeleteEnvironmentResponse()
    pb.ParseFromString(data)
    return models.DeleteEnvironmentResponse(message=pb.message, deleted_keys=pb.deleted_keys)


def decode_rename_environment_response(data: bytes) -> models.RenameEnvironmentResponse:
    pb = _PbRenameEnvironmentResponse()
    pb.ParseFromString(data)
    return models.RenameEnvironmentResponse(message=pb.message, updated_keys=pb.updated_keys)


def _decode_export_key_item(pb) -> models.ExportKeyItemResponse:
    return models.ExportKeyItemResponse(
        key=pb.key, value=pb.value,
        data_type=pb.data_type, is_sensitive=pb.is_sensitive,
    )


def decode_export_payload_response(data: bytes) -> models.ExportPayloadResponse:
    pb = _PbExportPayloadResponse()
    pb.ParseFromString(data)
    return models.ExportPayloadResponse(
        version=pb.version,
        export_date=None,
        environment=pb.environment,
        key_count=pb.key_count,
        keys=[_decode_export_key_item(k) for k in pb.keys],
    )


def decode_import_result_response(data: bytes) -> models.ImportResultResponse:
    pb = _PbImportResultResponse()
    pb.ParseFromString(data)
    return models.ImportResultResponse(
        imported=pb.imported, skipped=pb.skipped, message=pb.message,
    )


def decode_error_response(data: bytes) -> str | None:
    """Decode an ErrorResponse and return the error string, or None."""
    try:
        pb = _PbErrorResponse()
        pb.ParseFromString(data)
        return pb.error or None
    except Exception:
        return None


# ── Encode helpers ──

def encode_upsert_request(
    key: str, value: str, environment: str,
    data_type: str, is_sensitive: bool,
) -> bytes:
    pb = _PbUpsertRequest()
    pb.key = key
    pb.value = value
    pb.environment = environment
    pb.data_type = data_type
    pb.is_sensitive = is_sensitive
    return pb.SerializeToString()


def encode_rename_request(new_name: str) -> bytes:
    pb = _PbRenameRequest()
    pb.new_name = new_name
    return pb.SerializeToString()


def encode_import_request(
    environment: str | None,
    mode: str,
    keys: list[models.ImportKeyItem],
) -> bytes:
    pb = _PbImportRequest()
    if environment:
        pb.environment = environment
    pb.mode = mode
    for k in keys:
        item = pb.keys.add()
        item.key = k.key
        item.value = k.value
        item.data_type = k.data_type
        item.is_sensitive = k.is_sensitive
    return pb.SerializeToString()
