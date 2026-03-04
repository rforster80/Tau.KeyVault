/**
 * Error thrown when the Tau Key Vault API returns a non-success status code.
 */
export class KeyVaultApiError extends Error {
  /**
   * @param {number} statusCode  HTTP status code
   * @param {string|null} apiError  Error message from the API body
   * @param {string} message  Human-readable message
   */
  constructor(statusCode, apiError, message) {
    super(message);
    this.name = 'KeyVaultApiError';
    /** @type {number} */
    this.statusCode = statusCode;
    /** @type {string|null} */
    this.apiError = apiError;
  }
}
