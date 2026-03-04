"""
Tau Key Vault — Python Client

Async/sync client for the Tau Key Vault REST API. Supports JSON and Protocol
Buffers transport with typed helper methods for all key-value data types.
"""

from __future__ import annotations

import json
from datetime import date, datetime, time
from decimal import Decimal
from typing import Any, TypeVar, overload
from urllib.parse import quote

import httpx

from .enums import KeyVaultDataType, KeyVaultTransport
from .errors import KeyVaultApiError
from .models import (
    DeleteEnvironmentResponse,
    EnvironmentListResponse,
    ExportKeyItemResponse,
    ExportPayloadResponse,
    ImportKeyItem,
    ImportRequest,
    ImportResultResponse,
    KeyEntryListResponse,
    KeyEntryResponse,
    RenameEnvironmentResponse,
)
from . import proto as _proto

T = TypeVar("T")

_PROTO_CT = "application/x-protobuf"
_JSON_CT = "application/json"


# ─────────────────────────────────────────────────────────
#  JSON ↔ Model helpers
# ─────────────────────────────────────────────────────────


def _key_entry_from_json(data: dict) -> KeyEntryResponse:
    return KeyEntryResponse(
        key=data.get("key", ""),
        value=data.get("value", ""),
        environment=data.get("environment", ""),
        data_type=data.get("dataType", "Text"),
        is_sensitive=data.get("isSensitive", False),
        updated_at=data.get("updatedAt"),
    )


def _key_entry_list_from_json(data: dict) -> KeyEntryListResponse:
    return KeyEntryListResponse(
        items=[_key_entry_from_json(i) for i in data.get("items", [])],
    )


def _environment_list_from_json(data: dict) -> EnvironmentListResponse:
    return EnvironmentListResponse(environments=data.get("environments", []))


def _delete_env_from_json(data: dict) -> DeleteEnvironmentResponse:
    return DeleteEnvironmentResponse(
        message=data.get("message", ""),
        deleted_keys=data.get("deletedKeys", 0),
    )


def _rename_env_from_json(data: dict) -> RenameEnvironmentResponse:
    return RenameEnvironmentResponse(
        message=data.get("message", ""),
        updated_keys=data.get("updatedKeys", 0),
    )


def _export_payload_from_json(data: dict) -> ExportPayloadResponse:
    return ExportPayloadResponse(
        version=data.get("version", ""),
        export_date=data.get("exportDate"),
        environment=data.get("environment", ""),
        key_count=data.get("keyCount", 0),
        keys=[
            ExportKeyItemResponse(
                key=k.get("key", ""),
                value=k.get("value", ""),
                data_type=k.get("dataType", "Text"),
                is_sensitive=k.get("isSensitive", False),
            )
            for k in data.get("keys", [])
        ],
    )


def _import_result_from_json(data: dict) -> ImportResultResponse:
    return ImportResultResponse(
        imported=data.get("imported", 0),
        skipped=data.get("skipped", 0),
        message=data.get("message", ""),
    )


def _parse_csv(value: str) -> list[str]:
    """Parse a CSV string into a trimmed, non-empty list."""
    if not value or not value.strip():
        return []
    return [s.strip() for s in value.split(",") if s.strip()]


# ═══════════════════════════════════════════════════════════
#  KeyVaultClient
# ═══════════════════════════════════════════════════════════


class KeyVaultClient:
    """
    Client for the Tau Key Vault REST API.

    Supports JSON and Protocol Buffers transport with typed helper methods
    for all nine key-value data types.

    Args:
        base_url: Base URL of the Tau Key Vault server.
        api_key: API key for authentication (X-Api-Key header).
        default_environment: Default environment (blank = Global).
        transport: Transport mode — Api, Protobuf, or ProtobufWithApiFallback.
        timeout: Request timeout in seconds.
        http_client: Optional pre-configured httpx.Client to use.
    """

    def __init__(
        self,
        base_url: str,
        api_key: str,
        *,
        default_environment: str = "",
        transport: KeyVaultTransport = KeyVaultTransport.API,
        timeout: float = 30.0,
        http_client: httpx.Client | None = None,
    ) -> None:
        if not base_url:
            raise ValueError("base_url is required.")
        if not api_key:
            raise ValueError("api_key is required.")

        self._base_url = base_url.rstrip("/")
        self._api_key = api_key
        self._default_environment = default_environment
        self._transport = transport
        self._timeout = timeout

        if http_client is not None:
            self._http = http_client
            self._owns_client = False
        else:
            self._http = httpx.Client(
                base_url=self._base_url,
                headers={"X-Api-Key": self._api_key},
                timeout=self._timeout,
            )
            self._owns_client = True

    # ── Context manager ─────────────────────────────────

    def __enter__(self) -> "KeyVaultClient":
        return self

    def __exit__(self, *args: Any) -> None:
        self.close()

    def close(self) -> None:
        """Close the underlying HTTP client if we own it."""
        if self._owns_client:
            self._http.close()

    # ═══════════════════════════════════════════════════════════
    #  CORE API METHODS (Swagger endpoints)
    # ═══════════════════════════════════════════════════════════

    # ── Keys ──────────────────────────────────────────────

    def get_all_keys(
        self,
        environment: str | None = None,
        raw: bool = False,
    ) -> KeyEntryListResponse:
        """List all keys for an environment with global fallback."""
        env = environment if environment is not None else self._default_environment
        url = f"api/keys?environment={quote(env)}&raw={str(raw).lower()}"
        return self._send_get(url, _key_entry_list_from_json, _proto.decode_key_entry_list_response)

    def get_key(
        self,
        key: str,
        environment: str | None = None,
    ) -> KeyEntryResponse:
        """Get a single key by name with global fallback."""
        env = environment if environment is not None else self._default_environment
        url = f"api/keys/{quote(key)}"
        if env:
            url += f"?environment={quote(env)}"
        else:
            url += "?environment="
        return self._send_get(url, _key_entry_from_json, _proto.decode_key_entry_response)

    def upsert_key(
        self,
        key: str,
        value: str,
        environment: str | None = None,
        data_type: KeyVaultDataType = KeyVaultDataType.TEXT,
        is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Create or update a key-value pair."""
        env = environment if environment is not None else self._default_environment
        json_body = {
            "key": key,
            "value": value,
            "environment": env,
            "dataType": str(data_type),
            "isSensitive": is_sensitive,
        }
        proto_bytes = lambda: _proto.encode_upsert_request(key, value, env, str(data_type), is_sensitive)
        return self._send_put(
            "api/keys", json_body, proto_bytes,
            _key_entry_from_json, _proto.decode_key_entry_response,
        )

    # ── Environments ──────────────────────────────────────

    def get_environments(self) -> EnvironmentListResponse:
        """List all known environments."""
        return self._send_get(
            "api/keys/environments",
            _environment_list_from_json, _proto.decode_environment_list_response,
        )

    def delete_environment(self, environment: str) -> DeleteEnvironmentResponse:
        """Delete an environment and all its keys."""
        url = f"api/keys/environments/{quote(environment)}"
        return self._send_delete(url, _delete_env_from_json, _proto.decode_delete_environment_response)

    def rename_environment(
        self, environment: str, new_name: str,
    ) -> RenameEnvironmentResponse:
        """Rename an environment."""
        url = f"api/keys/environments/{quote(environment)}/rename"
        json_body = {"newName": new_name}
        proto_bytes = lambda: _proto.encode_rename_request(new_name)
        return self._send_put(
            url, json_body, proto_bytes,
            _rename_env_from_json, _proto.decode_rename_environment_response,
        )

    # ── Export / Import ───────────────────────────────────

    def export(self, environment: str | None = None) -> ExportPayloadResponse:
        """Export all keys for an environment."""
        env = environment if environment is not None else self._default_environment
        url = f"api/keys/export?environment={quote(env)}"
        return self._send_get(url, _export_payload_from_json, _proto.decode_export_payload_response)

    def import_keys(self, request: ImportRequest) -> ImportResultResponse:
        """Import keys into an environment."""
        json_body = {
            "environment": request.environment or "",
            "mode": request.mode,
            "keys": [
                {
                    "key": k.key,
                    "value": k.value,
                    "dataType": k.data_type,
                    "isSensitive": k.is_sensitive,
                }
                for k in request.keys
            ],
        }
        proto_bytes = lambda: _proto.encode_import_request(
            request.environment, request.mode, request.keys,
        )
        return self._send_post(
            "api/keys/import", json_body, proto_bytes,
            _import_result_from_json, _proto.decode_import_result_response,
        )

    def get_proto_schema(self) -> str:
        """Download the auto-generated .proto schema file as a string."""
        resp = self._raw_request("GET", "api/keys/proto", headers={"Accept": "text/plain"})
        return resp.text

    # ═══════════════════════════════════════════════════════════
    #  KEY EXISTS
    # ═══════════════════════════════════════════════════════════

    def key_exists(self, key: str, environment: str | None = None) -> bool:
        """Check if a key exists in the specified environment (with global fallback)."""
        try:
            self.get_key(key, environment)
            return True
        except KeyVaultApiError as e:
            if e.status_code == 404:
                return False
            raise

    # ═══════════════════════════════════════════════════════════
    #  GET OR CREATE (typed, creates with default if missing)
    # ═══════════════════════════════════════════════════════════

    def get_or_create_text(
        self, key: str, default_value: str,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> str:
        """Get a Text value, creating the key with the default if it does not exist."""
        return self._get_or_create_raw(key, default_value, KeyVaultDataType.TEXT, environment, is_sensitive)

    def get_or_create_code(
        self, key: str, default_value: str,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> str:
        """Get a Code value (always uppercase), creating the key with the default if it does not exist."""
        return self._get_or_create_raw(key, default_value.upper(), KeyVaultDataType.CODE, environment, is_sensitive)

    def get_or_create_numeric(
        self, key: str, default_value: Decimal | float | int,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> Decimal:
        """Get a Numeric value, creating the key with the default if it does not exist."""
        raw = self._get_or_create_raw(key, str(default_value), KeyVaultDataType.NUMERIC, environment, is_sensitive)
        return Decimal(raw)

    def get_or_create_boolean(
        self, key: str, default_value: bool,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> bool:
        """Get a Boolean value, creating the key with the default if it does not exist."""
        raw = self._get_or_create_raw(
            key, str(default_value).lower(), KeyVaultDataType.BOOLEAN, environment, is_sensitive,
        )
        return raw.strip().lower() in ("true", "1", "yes")

    def get_or_create_date(
        self, key: str, default_value: date,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> date:
        """Get a Date value, creating the key with the default if it does not exist."""
        raw = self._get_or_create_raw(
            key, default_value.isoformat(), KeyVaultDataType.DATE, environment, is_sensitive,
        )
        return date.fromisoformat(raw)

    def get_or_create_time(
        self, key: str, default_value: time,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> time:
        """Get a Time value, creating the key with the default if it does not exist."""
        raw = self._get_or_create_raw(
            key, default_value.isoformat(), KeyVaultDataType.TIME, environment, is_sensitive,
        )
        return time.fromisoformat(raw)

    def get_or_create_datetime(
        self, key: str, default_value: datetime,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> datetime:
        """Get a DateTime value, creating the key with the default if it does not exist."""
        raw = self._get_or_create_raw(
            key, default_value.isoformat(timespec="seconds"),
            KeyVaultDataType.DATE_TIME, environment, is_sensitive,
        )
        return datetime.fromisoformat(raw)

    def get_or_create_json(
        self, key: str, default_value: Any,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> Any:
        """Get a JSON value as a parsed object, creating the key with the serialized default if it does not exist."""
        raw = self._get_or_create_raw(
            key, json.dumps(default_value), KeyVaultDataType.JSON, environment, is_sensitive,
        )
        return json.loads(raw)

    def get_or_create_csv(
        self, key: str, default_values: list[str],
        environment: str | None = None, is_sensitive: bool = False,
    ) -> list[str]:
        """Get a CSV value as a list, creating the key with the default list if it does not exist."""
        raw = self._get_or_create_raw(
            key, ",".join(default_values), KeyVaultDataType.CSV, environment, is_sensitive,
        )
        return _parse_csv(raw)

    # ═══════════════════════════════════════════════════════════
    #  TYPED GET HELPERS
    # ═══════════════════════════════════════════════════════════

    def get_text(self, key: str, environment: str | None = None) -> str:
        """Get a Text value."""
        return self.get_key(key, environment).value

    def get_code(self, key: str, environment: str | None = None) -> str:
        """Get a Code value (uppercase text)."""
        return self.get_key(key, environment).value

    def get_numeric(self, key: str, environment: str | None = None) -> Decimal:
        """Get a Numeric value as Decimal."""
        return Decimal(self.get_key(key, environment).value)

    def get_boolean(self, key: str, environment: str | None = None) -> bool:
        """Get a Boolean value."""
        val = self.get_key(key, environment).value.strip().lower()
        return val in ("true", "1", "yes")

    def get_date(self, key: str, environment: str | None = None) -> date:
        """Get a Date value."""
        return date.fromisoformat(self.get_key(key, environment).value)

    def get_time(self, key: str, environment: str | None = None) -> time:
        """Get a Time value."""
        return time.fromisoformat(self.get_key(key, environment).value)

    def get_datetime(self, key: str, environment: str | None = None) -> datetime:
        """Get a DateTime value."""
        return datetime.fromisoformat(self.get_key(key, environment).value)

    def get_json(self, key: str, environment: str | None = None) -> Any:
        """Get a JSON value deserialized."""
        return json.loads(self.get_key(key, environment).value)

    def get_csv(self, key: str, environment: str | None = None) -> list[str]:
        """Get a CSV value as a list of strings."""
        return _parse_csv(self.get_key(key, environment).value)

    # ═══════════════════════════════════════════════════════════
    #  TYPED UPDATE HELPERS
    # ═══════════════════════════════════════════════════════════

    def update_text(
        self, key: str, value: str,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a Text value."""
        return self.upsert_key(key, value, environment, KeyVaultDataType.TEXT, is_sensitive)

    def update_code(
        self, key: str, value: str,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a Code value (stored uppercase)."""
        return self.upsert_key(key, value.upper(), environment, KeyVaultDataType.CODE, is_sensitive)

    def update_numeric(
        self, key: str, value: Decimal | float | int,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a Numeric value."""
        return self.upsert_key(key, str(value), environment, KeyVaultDataType.NUMERIC, is_sensitive)

    def update_boolean(
        self, key: str, value: bool,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a Boolean value."""
        return self.upsert_key(key, str(value).lower(), environment, KeyVaultDataType.BOOLEAN, is_sensitive)

    def update_date(
        self, key: str, value: date,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a Date value."""
        return self.upsert_key(key, value.isoformat(), environment, KeyVaultDataType.DATE, is_sensitive)

    def update_time(
        self, key: str, value: time,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a Time value."""
        return self.upsert_key(key, value.isoformat(), environment, KeyVaultDataType.TIME, is_sensitive)

    def update_datetime(
        self, key: str, value: datetime,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a DateTime value."""
        return self.upsert_key(
            key, value.isoformat(timespec="seconds"),
            environment, KeyVaultDataType.DATE_TIME, is_sensitive,
        )

    def update_json(
        self, key: str, value: Any,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a JSON value by serializing the object."""
        return self.upsert_key(key, json.dumps(value), environment, KeyVaultDataType.JSON, is_sensitive)

    def update_csv(
        self, key: str, values: list[str],
        environment: str | None = None, is_sensitive: bool = False,
    ) -> KeyEntryResponse:
        """Update a CSV value from a list of strings."""
        return self.upsert_key(key, ",".join(values), environment, KeyVaultDataType.CSV, is_sensitive)

    # ═══════════════════════════════════════════════════════════
    #  CSV LIST MANAGEMENT HELPERS
    # ═══════════════════════════════════════════════════════════

    def csv_add(
        self, key: str, item: str,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> list[str]:
        """Add an item to a CSV list key. Creates the key if it does not exist."""
        items = self.get_or_create_csv(key, [], environment, is_sensitive)
        items.append(item)
        self.update_csv(key, items, environment, is_sensitive)
        return items

    def csv_remove(
        self, key: str, item: str,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> list[str]:
        """Remove the first occurrence of an item from a CSV list key."""
        items = self.get_csv(key, environment)
        try:
            items.remove(item)
        except ValueError:
            pass
        self.update_csv(key, items, environment, is_sensitive)
        return items

    def csv_contains(
        self, key: str, item: str,
        environment: str | None = None,
    ) -> bool:
        """Check if a CSV list key contains an item."""
        return item in self.get_csv(key, environment)

    def csv_replace(
        self, key: str, old_item: str, new_item: str,
        environment: str | None = None, is_sensitive: bool = False,
    ) -> list[str]:
        """Replace all occurrences of old_item with new_item in a CSV list key."""
        items = self.get_csv(key, environment)
        items = [new_item if i == old_item else i for i in items]
        self.update_csv(key, items, environment, is_sensitive)
        return items

    # ═══════════════════════════════════════════════════════════
    #  INTERNAL: Transport (JSON / Protobuf / Failover)
    # ═══════════════════════════════════════════════════════════

    def _get_or_create_raw(
        self, key: str, default_value: str, data_type: KeyVaultDataType,
        environment: str | None, is_sensitive: bool,
    ) -> str:
        """Try to GET, on 404 create with PUT, return raw string value."""
        try:
            entry = self.get_key(key, environment)
            return entry.value
        except KeyVaultApiError as e:
            if e.status_code == 404:
                created = self.upsert_key(key, default_value, environment, data_type, is_sensitive)
                return created.value
            raise

    # ── Transport dispatch ────────────────────────────────

    def _send_get(self, url, json_decoder, proto_decoder):
        t = self._transport
        if t == KeyVaultTransport.PROTOBUF:
            return self._exec_get(url, proto_decoder, use_protobuf=True)
        elif t == KeyVaultTransport.PROTOBUF_WITH_API_FALLBACK:
            try:
                return self._exec_get(url, proto_decoder, use_protobuf=True)
            except Exception:
                return self._exec_get(url, json_decoder, use_protobuf=False)
        else:
            return self._exec_get(url, json_decoder, use_protobuf=False)

    def _send_put(self, url, json_body, proto_bytes_fn, json_decoder, proto_decoder):
        t = self._transport
        if t == KeyVaultTransport.PROTOBUF:
            return self._exec_put(url, proto_bytes_fn(), proto_decoder, use_protobuf=True)
        elif t == KeyVaultTransport.PROTOBUF_WITH_API_FALLBACK:
            try:
                return self._exec_put(url, proto_bytes_fn(), proto_decoder, use_protobuf=True)
            except Exception:
                return self._exec_put_json(url, json_body, json_decoder)
        else:
            return self._exec_put_json(url, json_body, json_decoder)

    def _send_post(self, url, json_body, proto_bytes_fn, json_decoder, proto_decoder):
        t = self._transport
        if t == KeyVaultTransport.PROTOBUF:
            return self._exec_post(url, proto_bytes_fn(), proto_decoder, use_protobuf=True)
        elif t == KeyVaultTransport.PROTOBUF_WITH_API_FALLBACK:
            try:
                return self._exec_post(url, proto_bytes_fn(), proto_decoder, use_protobuf=True)
            except Exception:
                return self._exec_post_json(url, json_body, json_decoder)
        else:
            return self._exec_post_json(url, json_body, json_decoder)

    def _send_delete(self, url, json_decoder, proto_decoder):
        t = self._transport
        if t == KeyVaultTransport.PROTOBUF:
            return self._exec_delete(url, proto_decoder, use_protobuf=True)
        elif t == KeyVaultTransport.PROTOBUF_WITH_API_FALLBACK:
            try:
                return self._exec_delete(url, proto_decoder, use_protobuf=True)
            except Exception:
                return self._exec_delete(url, json_decoder, use_protobuf=False)
        else:
            return self._exec_delete(url, json_decoder, use_protobuf=False)

    # ── Core HTTP executors ───────────────────────────────

    def _exec_get(self, url, decoder, use_protobuf: bool):
        accept = _PROTO_CT if use_protobuf else _JSON_CT
        resp = self._raw_request("GET", url, headers={"Accept": accept})
        return self._handle_response(resp, decoder, use_protobuf)

    def _exec_put(self, url, body_bytes, decoder, use_protobuf: bool):
        resp = self._raw_request("PUT", url, content=body_bytes, headers={
            "Accept": _PROTO_CT,
            "Content-Type": _PROTO_CT,
        })
        return self._handle_response(resp, decoder, use_protobuf=True)

    def _exec_put_json(self, url, json_body, decoder):
        resp = self._raw_request("PUT", url, json=json_body, headers={"Accept": _JSON_CT})
        return self._handle_response(resp, decoder, use_protobuf=False)

    def _exec_post(self, url, body_bytes, decoder, use_protobuf: bool):
        resp = self._raw_request("POST", url, content=body_bytes, headers={
            "Accept": _PROTO_CT,
            "Content-Type": _PROTO_CT,
        })
        return self._handle_response(resp, decoder, use_protobuf=True)

    def _exec_post_json(self, url, json_body, decoder):
        resp = self._raw_request("POST", url, json=json_body, headers={"Accept": _JSON_CT})
        return self._handle_response(resp, decoder, use_protobuf=False)

    def _exec_delete(self, url, decoder, use_protobuf: bool):
        accept = _PROTO_CT if use_protobuf else _JSON_CT
        resp = self._raw_request("DELETE", url, headers={"Accept": accept})
        return self._handle_response(resp, decoder, use_protobuf)

    # ── Raw request ───────────────────────────────────────

    def _raw_request(self, method: str, url: str, **kwargs) -> httpx.Response:
        """Execute an HTTP request, merging default headers."""
        headers = kwargs.pop("headers", {})
        headers.setdefault("X-Api-Key", self._api_key)
        full_url = f"{self._base_url}/{url}"
        resp = self._http.request(method, full_url, headers=headers, **kwargs)
        if not resp.is_success:
            self._raise_api_error(resp)
        return resp

    # ── Response handling ─────────────────────────────────

    def _handle_response(self, resp, decoder, use_protobuf: bool):
        if use_protobuf:
            return decoder(resp.content)
        else:
            return decoder(resp.json())

    def _raise_api_error(self, resp: httpx.Response) -> None:
        api_error: str | None = None
        try:
            body = resp.json()
            api_error = body.get("error")
        except Exception:
            try:
                api_error = _proto.decode_error_response(resp.content)
            except Exception:
                pass

        raise KeyVaultApiError(
            status_code=resp.status_code,
            api_error=api_error,
            message=api_error or f"HTTP {resp.status_code} {resp.reason_phrase}",
        )
