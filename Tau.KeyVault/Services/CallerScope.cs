using Tau.KeyVault.Models;

namespace Tau.KeyVault.Services;

/// <summary>
/// What the current caller is allowed to reach.
/// <para>
/// <c>Global</c> callers — the keys configured in appsettings.json, and the signed-in admin
/// UI — see everything, exactly as before this feature existed. A caller bound to an
/// environment sees only that environment: no other environment's keys, no Global fallback,
/// and no environment or credential administration.
/// </para>
/// </summary>
public readonly record struct CallerScope(bool IsGlobal, string Environment, string Name)
{
    public static CallerScope Global(string name) => new(true, string.Empty, name);
    public static CallerScope ForEnvironment(string environment, string name) => new(false, environment, name);

    /// <summary>The environment to use when a request does not name one.</summary>
    public string DefaultEnvironment => IsGlobal ? string.Empty : Environment;

    /// <summary>True when this caller may touch <paramref name="environment"/>.</summary>
    public bool CanReach(string environment) =>
        IsGlobal || string.Equals(
            KeyVaultService.NormalizeEnvironment(environment), Environment, StringComparison.Ordinal);
}

/// <summary>
/// Thrown when a valid credential is used outside the environment it is bound to, or attempts
/// an administrative action reserved for Global callers. Surfaces as 403.
/// </summary>
public class ScopeViolationException : Exception
{
    /// <summary>The environment the caller tried to reach, for the audit row.</summary>
    public string AttemptedEnvironment { get; }

    public ScopeViolationException(string message, string attemptedEnvironment) : base(message)
    {
        AttemptedEnvironment = attemptedEnvironment;
    }
}

/// <summary>Resolves the current caller's scope. Anything without an API key context is Global.</summary>
public class CallerScopeAccessor
{
    /// <summary>HttpContext.Items slot holding the resolved scope for this request.</summary>
    public const string ScopeItem = "Tau.KeyVault.CallerScope";

    private readonly IHttpContextAccessor _http;

    public CallerScopeAccessor(IHttpContextAccessor http) => _http = http;

    /// <summary>
    /// The active scope. The admin UI (cookie auth, and Blazor circuits with no HttpContext
    /// at all) is Global — per-environment credentials govern API callers only.
    /// </summary>
    public CallerScope Current
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx is not null && ctx.Items.TryGetValue(ScopeItem, out var v) && v is CallerScope scope)
                return scope;

            return CallerScope.Global(ctx?.User?.Identity?.Name ?? "system");
        }
    }
}
