namespace Tau.KeyVault.Client;

/// <summary>
/// Determines how the client communicates with the Tau Key Vault API.
/// </summary>
public enum KeyVaultTransport
{
    /// <summary>
    /// Use JSON serialization for all requests and responses (default).
    /// </summary>
    Api = 0,

    /// <summary>
    /// Use Protocol Buffers serialization for all requests and responses.
    /// </summary>
    Protobuf = 1,

    /// <summary>
    /// Attempt Protocol Buffers first; if the request fails, automatically
    /// retry with JSON. Useful during migration or when server protobuf
    /// support is uncertain.
    /// </summary>
    ProtobufWithApiFallback = 2
}
