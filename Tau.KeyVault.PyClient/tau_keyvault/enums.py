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


class KeyVaultAuditAction(StrEnum):
    """Action recorded in the access audit log."""

    READ_KEY = "ReadKey"
    LIST_KEYS = "ListKeys"
    WRITE_KEY = "WriteKey"
    DELETE_KEY = "DeleteKey"
    LIST_ENVIRONMENTS = "ListEnvironments"
    DELETE_ENVIRONMENT = "DeleteEnvironment"
    RENAME_ENVIRONMENT = "RenameEnvironment"
    EXPORT = "Export"
    IMPORT = "Import"
    READ_AUDIT = "ReadAudit"
    AUTH_FAILURE = "AuthFailure"


class KeyVaultAuditOutcome(StrEnum):
    """Result of an audited action."""

    SUCCESS = "Success"
    NOT_FOUND = "NotFound"
    DENIED = "Denied"
    ERROR = "Error"
