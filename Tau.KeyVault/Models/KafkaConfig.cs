namespace Tau.KeyVault.Models;

/// <summary>How the client connects to the broker. Maps to librdkafka's <c>security.protocol</c>.</summary>
public enum KafkaSecurityProtocol
{
    Plaintext = 0,
    Ssl = 1,
    SaslPlaintext = 2,
    SaslSsl = 3
}

/// <summary>SASL mechanism. <see cref="None"/> means no SASL, valid only with Plaintext/Ssl.</summary>
public enum KafkaSaslMechanism
{
    None = 0,
    Plain = 1,
    ScramSha256 = 2,
    ScramSha512 = 3,
    /// <summary>Kerberos.</summary>
    Gssapi = 4,
    OAuthBearer = 5
}

/// <summary>Broker acknowledgement level for a published notification.</summary>
public enum KafkaAcks
{
    /// <summary>Fire and forget; fastest, no delivery guarantee.</summary>
    None = 0,
    /// <summary>Leader only.</summary>
    Leader = 1,
    /// <summary>All in-sync replicas.</summary>
    All = 2
}

/// <summary>
/// A Kafka endpoint that receives a notification whenever a key in this environment changes.
/// Mirrors <see cref="NatsConfig"/>, but carries the full connection surface a real broker
/// needs: TLS, SASL (PLAIN, SCRAM, Kerberos, OAuth), and producer tuning.
/// <para>
/// Secrets — <see cref="SaslPassword"/>, <see cref="SslKeyPassword"/> and
/// <see cref="OauthBearerClientSecret"/> — are encrypted at rest with the vault salt, the same
/// way key values are, and are masked in the admin UI.
/// </para>
/// </summary>
public class KafkaConfig
{
    public int Id { get; set; }

    public string Environment { get; set; } = string.Empty;

    /// <summary>Comma-separated host:port list, e.g. <c>broker1:9092,broker2:9092</c>.</summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>Topic name. Supports the <c>{environment}</c> and <c>{key}</c> placeholders.</summary>
    public string Topic { get; set; } = string.Empty;

    public bool LowercaseEnvironment { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional client.id reported to the broker; defaults to the app name.</summary>
    public string? ClientId { get; set; }

    // ── Security ─────────────────────────────────────────────────────

    public KafkaSecurityProtocol SecurityProtocol { get; set; } = KafkaSecurityProtocol.Plaintext;
    public KafkaSaslMechanism SaslMechanism { get; set; } = KafkaSaslMechanism.None;

    /// <summary>SASL username, for PLAIN and SCRAM.</summary>
    public string? SaslUsername { get; set; }

    /// <summary>SASL password, for PLAIN and SCRAM. Stored encrypted.</summary>
    public string? SaslPassword { get; set; }

    // ── TLS ──────────────────────────────────────────────────────────

    /// <summary>CA certificate path, for a private or self-signed broker CA.</summary>
    public string? SslCaLocation { get; set; }

    /// <summary>Client certificate path, for mutual TLS.</summary>
    public string? SslCertificateLocation { get; set; }

    /// <summary>Client private key path, for mutual TLS.</summary>
    public string? SslKeyLocation { get; set; }

    /// <summary>Passphrase for the client private key. Stored encrypted.</summary>
    public string? SslKeyPassword { get; set; }

    /// <summary>Leave on unless you are deliberately trusting an unverified broker certificate.</summary>
    public bool EnableSslCertificateVerification { get; set; } = true;

    // ── Kerberos (GSSAPI) ────────────────────────────────────────────

    public string? SaslKerberosServiceName { get; set; }
    public string? SaslKerberosPrincipal { get; set; }

    /// <summary>Path to the keytab used to obtain the Kerberos ticket.</summary>
    public string? SaslKerberosKeytab { get; set; }

    // ── OAuth bearer / OIDC ──────────────────────────────────────────

    public string? OauthBearerClientId { get; set; }

    /// <summary>OIDC client secret. Stored encrypted.</summary>
    public string? OauthBearerClientSecret { get; set; }

    public string? OauthBearerTokenEndpointUrl { get; set; }
    public string? OauthBearerScope { get; set; }

    /// <summary>Extra OAuth extensions, as <c>key=value</c> pairs separated by commas.</summary>
    public string? OauthBearerExtensions { get; set; }

    // ── Producer behaviour ───────────────────────────────────────────

    public KafkaAcks Acks { get; set; } = KafkaAcks.All;

    /// <summary>
    /// Bounds how long a publish may block. Dispatch happens inline on the key-write path,
    /// so this is deliberately short: a slow broker must not hold up a vault write.
    /// </summary>
    public int MessageTimeoutMs { get; set; } = 5000;

    public int RequestTimeoutMs { get; set; } = 5000;

    public bool EnableIdempotence { get; set; }

    /// <summary>none, gzip, snappy, lz4 or zstd.</summary>
    public string? CompressionType { get; set; }

    /// <summary>
    /// Escape hatch for any librdkafka setting not modelled above, one <c>key=value</c> per
    /// line. Applied last, so it overrides the fields above.
    /// <para>
    /// Stored in plain text — put credentials in the dedicated encrypted fields, not here.
    /// </para>
    /// </summary>
    public string? AdditionalConfig { get; set; }
}
