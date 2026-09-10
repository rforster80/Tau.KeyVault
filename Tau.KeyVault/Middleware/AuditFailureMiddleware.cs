using Tau.KeyVault.Services;

namespace Tau.KeyVault.Middleware;

/// <summary>
/// Turns a failed audit write into a 503 instead of a 500.
/// <para>
/// The vault is fail-closed on auditing (ADR-045): if a security-relevant event cannot be
/// recorded, the operation must not happen. <see cref="AuditWriteException"/> means exactly
/// that — the request was refused because it could not be audited, and it is retryable once
/// the audit store recovers, which is what 503 communicates.
/// </para>
/// </summary>
public class AuditFailureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditFailureMiddleware> _logger;

    public AuditFailureMiddleware(RequestDelegate next, ILogger<AuditFailureMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AuditWriteException ex)
        {
            _logger.LogError(ex, "Refusing {Method} {Path}: the access audit log is unavailable.",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Operation refused: the access audit log is unavailable and this action cannot be recorded. Retry once the audit store recovers."
            });
        }
    }
}
