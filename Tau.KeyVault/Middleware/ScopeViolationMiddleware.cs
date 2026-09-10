using Tau.KeyVault.Models;
using Tau.KeyVault.Services;

namespace Tau.KeyVault.Middleware;

/// <summary>
/// Turns a <see cref="ScopeViolationException"/> into a 403 and records it.
/// <para>
/// This is a valid credential reaching outside its environment — a different signal from a
/// bad credential, and a more interesting one: it is what a misconfigured consumer or a
/// stolen key being probed looks like. It is audited as <c>ScopeViolation</c> with the
/// credential name and the environment it reached for.
/// </para>
/// </summary>
public class ScopeViolationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ScopeViolationMiddleware> _logger;

    public ScopeViolationMiddleware(RequestDelegate next, ILogger<ScopeViolationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AuditService audit, CallerScopeAccessor scopes)
    {
        try
        {
            await _next(context);
        }
        catch (ScopeViolationException ex)
        {
            var scope = scopes.Current;

            _logger.LogWarning(
                "Credential '{Name}' (bound to '{Bound}') was refused access to '{Attempted}': {Message}",
                scope.Name, scope.Environment, ex.AttemptedEnvironment, ex.Message);

            await audit.WriteAsync(AuditAction.ScopeViolation, AuditActorType.ApiKey, scope.Name,
                environment: ex.AttemptedEnvironment, outcome: AuditOutcome.Denied,
                ipAddress: context.Connection.RemoteIpAddress?.ToString());

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }
}
