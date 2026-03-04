"""Enumerations for the Tau Key Vault client."""

from enum import StrEnum


class KeyVaultTransport(StrEnum):
    """Determines how the client communicates with the Tau Key Vault API."""

    API = "Api"
    """Use JSON for all requests and responses (default)."""

    PROTOBUF = "Protobuf"
    """Use Protocol Buffers for all requests and responses."""

    PROTOBUF_WITH_API_FALLBACK = "ProtobufWithApiFallback"
    """Attempt Protobuf first; on failure, retry with JSON automatically."""


class KeyVaultDataType(StrEnum):
    """Data types supported by Tau Key Vault."""

    TEXT = "Text"
    CODE = "Code"
    NUMERIC = "Numeric"
    BOOLEAN = "Boolean"
    DATE = "Date"
    TIME = "Time"
    DATE_TIME = "DateTime"
    JSON = "Json"
    CSV = "Csv"
