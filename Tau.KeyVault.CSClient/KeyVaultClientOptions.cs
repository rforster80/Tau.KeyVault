namespace Tau.KeyVault.Client;

/// <summary>
/// Configuration options for <see cref="KeyVaultClient"/>.
/// </summary>
public class KeyVaultClientOptions
{
    /// <summary>
    /// Base URL of the Tau Key Vault server (e.g. "https://vault.example.com").
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key for authenticating requests. Sent as the X-Api-Key header.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The default environment to use when none is specified.
    /// An empty string represents the Global environment.
    /// </summary>
    public string DefaultEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// Transport mode: Api (JSON), Protobuf, or ProtobufWithApiFallback.
    /// </summary>
    public KeyVaultTransport Transport { get; set; } = KeyVaultTransport.Api;

    /// <summary>
    /// HTTP request timeout. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
