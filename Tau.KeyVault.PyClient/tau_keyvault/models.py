"""Data models for the Tau Key Vault client."""

from __future__ import annotations

from dataclasses import dataclass, field


# ═══════════════════════════════════════════════════════════
#  Response models
# ═══════════════════════════════════════════════════════════


@dataclass
class KeyEntryResponse:
    """A single key-value entry."""

    key: str = ""
    value: str = ""
    environment: str = ""
    data_type: str = "Text"
    is_sensitive: bool = False
    updated_at: str | None = None


@dataclass
class KeyEntryListResponse:
    """List of key-value entries."""

    items: list[KeyEntryResponse] = field(default_factory=list)


@dataclass
class EnvironmentListResponse:
    """List of environment names."""

    environments: list[str] = field(default_factory=list)


@dataclass
class DeleteEnvironmentResponse:
    """Result of deleting an environment."""

    message: str = ""
    deleted_keys: int = 0


@dataclass
class ApiKeyResponse:
    """Metadata for a per-environment API credential. Never carries the secret."""

    id: int = 0
    name: str = ""
    environment: str = ""
    enabled: bool = False
    created_at: str = ""
    last_rotated_at: str = ""


@dataclass
class ApiKeyListResponse:
    items: list[ApiKeyResponse] = field(default_factory=list)


@dataclass
class ApiKeySecretResponse:
    """Returned by create and rotate only.

    The server stores just a hash, so ``key`` cannot be recovered afterwards —
    store it immediately.
    """

    id: int = 0
    name: str = ""
    environment: str = ""
    #: Store this now; it is never shown again.
    key: str = ""
    message: str = ""


@dataclass
class RevokeApiKeyResponse:
    message: str = ""
    id: int = 0


@dataclass
class AuditEntryResponse:
    """One access audit row.

    There is deliberately no value field: the trail records access to a key,
    never its contents.
    """

    timestamp: str = ""
    action: str = ""
    key: str = ""
    environment: str = ""
    actor_type: str = ""
    #: API key name or admin username. Never the API key itself.
    actor_id: str = ""
    outcome: str = ""
    ip_address: str = ""
    item_count: int = 0
    id: int = 0


@dataclass
class AuditEntryListResponse:
    """A page of audit rows, newest first."""

    items: list[AuditEntryResponse] = field(default_factory=list)
    #: Total rows matching the filter, ignoring limit/offset.
    total_count: int = 0
    limit: int = 0
    offset: int = 0


@dataclass
class DeleteKeyResponse:
    """Result of deleting a single key."""

    message: str = ""
    key: str = ""
    environment: str = ""


@dataclass
class RenameEnvironmentResponse:
    """Result of renaming an environment."""

    message: str = ""
    updated_keys: int = 0


@dataclass
class ExportKeyItemResponse:
    """A single key in an export payload."""

    key: str = ""
    value: str = ""
    data_type: str = "Text"
    is_sensitive: bool = False


@dataclass
class ExportPayloadResponse:
    """Full export payload."""

    version: str = ""
    export_date: str | None = None
    environment: str = ""
    key_count: int = 0
    keys: list[ExportKeyItemResponse] = field(default_factory=list)


@dataclass
class ImportResultResponse:
    """Result of an import operation."""

    imported: int = 0
    skipped: int = 0
    message: str = ""


@dataclass
class ErrorResponse:
    """Error returned by the API."""

    error: str = ""


# ═══════════════════════════════════════════════════════════
#  Request models
# ═══════════════════════════════════════════════════════════


@dataclass
class ImportKeyItem:
    """A single key for import."""

    key: str = ""
    value: str = ""
    data_type: str = "Text"
    is_sensitive: bool = False


@dataclass
class ImportRequest:
    """Import request payload."""

    environment: str | None = None
    mode: str = "AddMissing"
    keys: list[ImportKeyItem] = field(default_factory=list)
