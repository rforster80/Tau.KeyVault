using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Confluent.Kafka;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

/// <summary>
/// Builds and caches Kafka producers, one per distinct effective configuration.
/// <para>
/// This deliberately differs from the NATS path, which opens a fresh connection per dispatch.
/// A librdkafka producer owns background threads and a broker metadata cache, so constructing
/// one per key change would be far more expensive than the publish itself. Producers are
/// therefore cached and reused, keyed by a fingerprint of the settings that actually affect
/// the connection — edit a config in the UI and the fingerprint changes, so the next dispatch
/// transparently builds a new producer and the stale one is disposed.
/// </para>
/// <para>Registered as a singleton; disposal tears down every cached producer.</para>
/// </summary>
public class KafkaProducerFactory : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<IProducer<string, string>>> _producers = new();
    private readonly ILogger<KafkaProducerFactory> _logger;
    private bool _disposed;

    public KafkaProducerFactory(ILogger<KafkaProducerFactory> logger) => _logger = logger;

    /// <summary>
    /// Returns the producer for this configuration, building it on first use.
    /// <paramref name="decryptedConfig"/> must already have its secrets in plaintext.
    /// </summary>
    public IProducer<string, string> GetProducer(KafkaConfig decryptedConfig)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var producerConfig = BuildProducerConfig(decryptedConfig);
        var fingerprint = Fingerprint(producerConfig);

        var entry = _producers.AddOrUpdate(
            KeyFor(decryptedConfig),
            _ => new Lazy<IProducer<string, string>>(() => Build(producerConfig)),
            (_, existing) =>
            {
                // Same config row, but its settings changed — retire the old producer.
                if (existing.IsValueCreated && FingerprintOf(existing) == fingerprint)
                    return existing;

                if (existing.IsValueCreated)
                    DisposeQuietly(existing.Value);

                return new Lazy<IProducer<string, string>>(() => Build(producerConfig));
            });

        _fingerprints[entry] = fingerprint;
        return entry.Value;
    }

    private readonly ConcurrentDictionary<Lazy<IProducer<string, string>>, string> _fingerprints = new();

    private string FingerprintOf(Lazy<IProducer<string, string>> entry) =>
        _fingerprints.TryGetValue(entry, out var f) ? f : string.Empty;

    private static string KeyFor(KafkaConfig config) => $"config:{config.Id}";

    private IProducer<string, string> Build(ProducerConfig config)
    {
        _logger.LogInformation("Building Kafka producer for {Brokers} ({Protocol})",
            config.BootstrapServers, config.SecurityProtocol);

        return new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) =>
                _logger.LogWarning("Kafka client error [{Code}]: {Reason}", e.Code, e.Reason))
            .Build();
    }

    /// <summary>
    /// Translates a <see cref="KafkaConfig"/> into librdkafka settings. Only fields that are
    /// actually set are emitted, so an unauthenticated broker is not handed empty SASL values.
    /// </summary>
    public static ProducerConfig BuildProducerConfig(KafkaConfig c)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = c.BootstrapServers,
            ClientId = string.IsNullOrWhiteSpace(c.ClientId) ? "tau-keyvault" : c.ClientId,
            Acks = c.Acks switch
            {
                KafkaAcks.None => Confluent.Kafka.Acks.None,
                KafkaAcks.Leader => Confluent.Kafka.Acks.Leader,
                _ => Confluent.Kafka.Acks.All
            },
            MessageTimeoutMs = c.MessageTimeoutMs > 0 ? c.MessageTimeoutMs : 5000,
            RequestTimeoutMs = c.RequestTimeoutMs > 0 ? c.RequestTimeoutMs : 5000,
            SecurityProtocol = c.SecurityProtocol switch
            {
                KafkaSecurityProtocol.Ssl => Confluent.Kafka.SecurityProtocol.Ssl,
                KafkaSecurityProtocol.SaslPlaintext => Confluent.Kafka.SecurityProtocol.SaslPlaintext,
                KafkaSecurityProtocol.SaslSsl => Confluent.Kafka.SecurityProtocol.SaslSsl,
                _ => Confluent.Kafka.SecurityProtocol.Plaintext
            }
        };

        if (c.EnableIdempotence)
            config.EnableIdempotence = true;

        if (!string.IsNullOrWhiteSpace(c.CompressionType) &&
            Enum.TryParse<CompressionType>(c.CompressionType, ignoreCase: true, out var compression))
            config.CompressionType = compression;

        // ── SASL ──────────────────────────────────────────────────────
        if (c.SaslMechanism != KafkaSaslMechanism.None)
        {
            config.SaslMechanism = c.SaslMechanism switch
            {
                KafkaSaslMechanism.Plain => Confluent.Kafka.SaslMechanism.Plain,
                KafkaSaslMechanism.ScramSha256 => Confluent.Kafka.SaslMechanism.ScramSha256,
                KafkaSaslMechanism.ScramSha512 => Confluent.Kafka.SaslMechanism.ScramSha512,
                KafkaSaslMechanism.Gssapi => Confluent.Kafka.SaslMechanism.Gssapi,
                KafkaSaslMechanism.OAuthBearer => Confluent.Kafka.SaslMechanism.OAuthBearer,
                _ => Confluent.Kafka.SaslMechanism.Plain
            };

            if (!string.IsNullOrWhiteSpace(c.SaslUsername)) config.SaslUsername = c.SaslUsername;
            if (!string.IsNullOrWhiteSpace(c.SaslPassword)) config.SaslPassword = c.SaslPassword;

            // Kerberos
            if (!string.IsNullOrWhiteSpace(c.SaslKerberosServiceName)) config.SaslKerberosServiceName = c.SaslKerberosServiceName;
            if (!string.IsNullOrWhiteSpace(c.SaslKerberosPrincipal)) config.SaslKerberosPrincipal = c.SaslKerberosPrincipal;
            if (!string.IsNullOrWhiteSpace(c.SaslKerberosKeytab)) config.SaslKerberosKeytab = c.SaslKerberosKeytab;

            // OAuth bearer / OIDC
            if (c.SaslMechanism == KafkaSaslMechanism.OAuthBearer)
            {
                config.SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc;
                if (!string.IsNullOrWhiteSpace(c.OauthBearerClientId)) config.SaslOauthbearerClientId = c.OauthBearerClientId;
                if (!string.IsNullOrWhiteSpace(c.OauthBearerClientSecret)) config.SaslOauthbearerClientSecret = c.OauthBearerClientSecret;
                if (!string.IsNullOrWhiteSpace(c.OauthBearerTokenEndpointUrl)) config.SaslOauthbearerTokenEndpointUrl = c.OauthBearerTokenEndpointUrl;
                if (!string.IsNullOrWhiteSpace(c.OauthBearerScope)) config.SaslOauthbearerScope = c.OauthBearerScope;
                if (!string.IsNullOrWhiteSpace(c.OauthBearerExtensions)) config.SaslOauthbearerExtensions = c.OauthBearerExtensions;
            }
        }

        // ── TLS ───────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(c.SslCaLocation)) config.SslCaLocation = c.SslCaLocation;
        if (!string.IsNullOrWhiteSpace(c.SslCertificateLocation)) config.SslCertificateLocation = c.SslCertificateLocation;
        if (!string.IsNullOrWhiteSpace(c.SslKeyLocation)) config.SslKeyLocation = c.SslKeyLocation;
        if (!string.IsNullOrWhiteSpace(c.SslKeyPassword)) config.SslKeyPassword = c.SslKeyPassword;
        if (!c.EnableSslCertificateVerification) config.EnableSslCertificateVerification = false;

        // ── Passthrough, applied last so it can override anything above ──
        foreach (var (name, value) in ParseAdditionalConfig(c.AdditionalConfig))
            config.Set(name, value);

        return config;
    }

    /// <summary>Parses the free-form <c>key=value</c> passthrough block, ignoring blanks and # comments.</summary>
    public static IEnumerable<(string Name, string Value)> ParseAdditionalConfig(string? block)
    {
        if (string.IsNullOrWhiteSpace(block)) yield break;

        foreach (var raw in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var split = line.IndexOf('=');
            if (split <= 0) continue;

            var name = line[..split].Trim();
            var value = line[(split + 1)..].Trim();
            if (name.Length > 0) yield return (name, value);
        }
    }

    private static string Fingerprint(ProducerConfig config)
    {
        // Ordered so the hash is stable regardless of how the settings were assembled.
        var material = string.Join('\n', config.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private void DisposeQuietly(IProducer<string, string> producer)
    {
        try
        {
            producer.Flush(TimeSpan.FromSeconds(2));
            producer.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing a replaced Kafka producer");
        }
    }

    /// <summary>Drops the cached producer for a config, e.g. after it is deleted.</summary>
    public void Invalidate(int configId)
    {
        if (_producers.TryRemove($"config:{configId}", out var entry) && entry.IsValueCreated)
            DisposeQuietly(entry.Value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _producers.Values)
            if (entry.IsValueCreated)
                DisposeQuietly(entry.Value);

        _producers.Clear();
        GC.SuppressFinalize(this);
    }
}
