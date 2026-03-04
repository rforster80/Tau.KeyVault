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
