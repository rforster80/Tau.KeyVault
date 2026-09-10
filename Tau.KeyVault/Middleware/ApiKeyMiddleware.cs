using Tau.KeyVault.Models;
using Tau.KeyVault.Services;

namespace Tau.KeyVault.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context, AuditService audit, ApiKeyService apiKeys)
    {
        // Only apply to /api/* routes (skip swagger, blazor, auth, etc.)
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            // Rejected credentials are the primary signal for a compromised key, so they are
            // audited too — never with the presented value, only that one was missing/invalid.
            await audit.WriteAsync(AuditAction.AuthFailure, AuditActorType.Anonymous, "",
                environment: "", outcome: AuditOutcome.Denied, ipAddress: ip);

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "API key is required. Provide X-Api-Key header." });
            return;
        }

        var presented = extractedApiKey.ToString();

        // Configured keys are Global and keep working exactly as before — rotating one is a
        // config change plus a restart, by design.
        var configuredKeys = ApiKeyRegistry.Load(_config);
        var keyName = ApiKeyRegistry.ResolveName(configuredKeys, presented);

        if (keyName is not null)
        {
            context.Items[AuditActorAccessor.ApiKeyNameItem] = keyName;
            context.Items[CallerScopeAccessor.ScopeItem] = CallerScope.Global(keyName);
            await _next(context);
            return;
        }

        // Otherwise it may be a per-environment credential, if the feature is switched on.
        // ResolveAsync returns null while EnableAPIKeyPerEnvironment is false, so a disabled
        // feature behaves identically to the credential simply not existing.
        var scoped = await apiKeys.ResolveAsync(presented, context.RequestAborted);

        if (scoped is null)
        {
            await audit.WriteAsync(AuditAction.AuthFailure, AuditActorType.Anonymous, "",
                environment: "", outcome: AuditOutcome.Denied, ipAddress: ip);

            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
            return;
        }

        // The key itself stops here; only the name and the binding travel on.
        context.Items[AuditActorAccessor.ApiKeyNameItem] = scoped.Name;
        context.Items[CallerScopeAccessor.ScopeItem] =
            CallerScope.ForEnvironment(scoped.Environment, scoped.Name);

        await _next(context);
    }
}
