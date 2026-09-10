using Microsoft.AspNetCore.Components.Authorization;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

/// <summary>Who is acting, as recorded in the audit trail. Never carries a credential.</summary>
public readonly record struct AuditActor(AuditActorType Type, string Id, string? IpAddress);

/// <summary>
/// Resolves the acting identity for audit rows across both entry points:
/// REST callers (identified by API key <em>name</em>, stashed by <c>ApiKeyMiddleware</c>) and
/// admin UI users (identified by username).
/// <para>
/// The Blazor case needs care: once an interactive circuit is established there is no
/// <c>HttpContext</c>, so <see cref="IHttpContextAccessor"/> returns null and the identity has
/// to come from <see cref="AuthenticationStateProvider"/> instead. IP is unavailable on that
/// path, which is why <see cref="AccessAuditLog.IpAddress"/> is nullable.
/// </para>
/// </summary>
public class AuditActorAccessor
{
    /// <summary>HttpContext.Items slot holding the resolved API key name for this request.</summary>
    public const string ApiKeyNameItem = "Tau.KeyVault.ApiKeyName";

    private readonly IHttpContextAccessor _http;
    private readonly IServiceProvider _services;

    public AuditActorAccessor(IHttpContextAccessor http, IServiceProvider services)
    {
        _http = http;
        _services = services;
    }

    public async Task<AuditActor> GetAsync()
    {
        var ctx = _http.HttpContext;

        if (ctx is not null)
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString();

            if (ctx.Items.TryGetValue(ApiKeyNameItem, out var name) && name is string apiKeyName)
                return new AuditActor(AuditActorType.ApiKey, apiKeyName, ip);

            var username = ctx.User?.Identity?.Name;
            if (ctx.User?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(username))
                return new AuditActor(AuditActorType.User, username, ip);

            return new AuditActor(AuditActorType.Anonymous, string.Empty, ip);
        }

        // Blazor interactive circuit — no HttpContext, so ask the auth state provider.
        try
        {
            var provider = _services.GetService<AuthenticationStateProvider>();
            if (provider is not null)
            {
                var state = await provider.GetAuthenticationStateAsync();
                var username = state.User?.Identity?.Name;
                if (state.User?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(username))
                    return new AuditActor(AuditActorType.User, username, null);
            }
        }
        catch
        {
            // Fall through to System rather than let actor resolution break the operation.
        }

        return new AuditActor(AuditActorType.System, "system", null);
    }
}
