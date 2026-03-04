using Microsoft.Extensions.DependencyInjection;

namespace Tau.KeyVault.Client;

/// <summary>
/// Extension methods for registering <see cref="KeyVaultClient"/> with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KeyVaultClient"/> as a typed HttpClient with the specified options.
    /// </summary>
    public static IServiceCollection AddKeyVaultClient(
        this IServiceCollection services,
        Action<KeyVaultClientOptions> configure)
    {
        services.Configure(configure);

        services.AddHttpClient<KeyVaultClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeyVaultClientOptions>>().Value;

            if (!string.IsNullOrEmpty(options.BaseUrl))
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");

            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            client.Timeout = options.Timeout;
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="KeyVaultClient"/> using configuration from the provided options instance.
    /// </summary>
    public static IServiceCollection AddKeyVaultClient(
        this IServiceCollection services,
        KeyVaultClientOptions options)
    {
        return services.AddKeyVaultClient(o =>
        {
            o.BaseUrl = options.BaseUrl;
            o.ApiKey = options.ApiKey;
            o.DefaultEnvironment = options.DefaultEnvironment;
            o.Transport = options.Transport;
            o.Timeout = options.Timeout;
        });
    }
}
