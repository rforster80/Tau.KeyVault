using System.Net;

namespace Tau.KeyVault.Client;

/// <summary>
/// Exception thrown when the Tau Key Vault API returns a non-success status code.
/// </summary>
public class KeyVaultApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ApiError { get; }

    public KeyVaultApiException(HttpStatusCode statusCode, string? apiError, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ApiError = apiError;
    }

    public KeyVaultApiException(HttpStatusCode statusCode, string? apiError, string message, Exception inner)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ApiError = apiError;
    }
}
