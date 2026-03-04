"""Error types for the Tau Key Vault client."""

from __future__ import annotations


class KeyVaultApiError(Exception):
    """Raised when the Tau Key Vault API returns a non-success status code."""

    def __init__(self, status_code: int, api_error: str | None, message: str) -> None:
        super().__init__(message)
        self.status_code = status_code
        """HTTP status code returned by the server."""
        self.api_error = api_error
        """Error message from the API response body, if available."""
