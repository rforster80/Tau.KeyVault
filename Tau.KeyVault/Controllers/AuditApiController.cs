using Microsoft.AspNetCore.Mvc;
using Tau.KeyVault.Models;
using Tau.KeyVault.Services;

namespace Tau.KeyVault.Controllers;

/// <summary>
/// Reads the access audit trail: who read, wrote or deleted which key, in which environment,
/// and when. Values are never recorded and never returned.
/// </summary>
[ApiController]
[Route("api/audit")]
[Produces("application/json", "application/x-protobuf")]
public class AuditApiController : ControllerBase
{
    private readonly AuditService _audit;
    private readonly AuditActorAccessor _actor;
    private readonly CallerScopeAccessor _scopes;

    public AuditApiController(AuditService audit, AuditActorAccessor actor, CallerScopeAccessor scopes)
    {
        _audit = audit;
        _actor = actor;
        _scopes = scopes;
    }

    /// <summary>
    /// Query the access audit log, newest first. All filters are optional and combine with AND.
    /// </summary>
    /// <remarks>
    /// Reading the audit log is itself an audited event (<c>ReadAudit</c>).
    ///
    /// For erasure evidence, filter by the key and look for a <c>DeleteKey</c> row:
    /// <c>GET /api/audit?key=SubjectEmail&amp;action=DeleteKey</c>.
    /// To review one credential's activity: <c>GET /api/audit?actorId=adapter-prod</c>.
    /// To review rejected credentials: <c>GET /api/audit?action=AuthFailure</c>.
    /// </remarks>
    /// <param name="key">Exact key name. Collection-level actions have a blank key.</param>
    /// <param name="environment">Environment filter; pass an empty value to match Global.</param>
    /// <param name="actorId">API key name or admin username.</param>
    /// <param name="action">One of: ReadKey, ListKeys, WriteKey, DeleteKey, ListEnvironments, DeleteEnvironment, RenameEnvironment, Export, Import, ReadAudit, AuthFailure.</param>
    /// <param name="outcome">One of: Success, NotFound, Denied, Error.</param>
    /// <param name="from">Inclusive lower bound on timestamp (UTC).</param>
    /// <param name="to">Inclusive upper bound on timestamp (UTC).</param>
    /// <param name="limit">Page size, 1–1000. Defaults to 100.</param>
    /// <param name="offset">Rows to skip. Defaults to 0.</param>
    [HttpGet]
    [ProducesResponseType(typeof(AuditEntryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query(
        [FromQuery] string? key,
        [FromQuery] string? environment,
        [FromQuery] string? actorId,
        [FromQuery] string? action,
        [FromQuery] string? outcome,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        AuditAction? parsedAction = null;
        if (!string.IsNullOrWhiteSpace(action))
        {
            if (!Enum.TryParse<AuditAction>(action, ignoreCase: true, out var a))
                return BadRequest(new ErrorResponse { Error = $"Invalid action '{action}'. Valid values: {string.Join(", ", Enum.GetNames<AuditAction>())}" });
            parsedAction = a;
        }

        AuditOutcome? parsedOutcome = null;
        if (!string.IsNullOrWhiteSpace(outcome))
        {
            if (!Enum.TryParse<AuditOutcome>(outcome, ignoreCase: true, out var o))
                return BadRequest(new ErrorResponse { Error = $"Invalid outcome '{outcome}'. Valid values: {string.Join(", ", Enum.GetNames<AuditOutcome>())}" });
            parsedOutcome = o;
        }

        // A bound credential reads only its own environment's trail, whatever it asked for.
        var scope = _scopes.Current;
        if (!scope.IsGlobal)
        {
            if (environment is not null && !scope.CanReach(environment))
                throw new ScopeViolationException(
                    $"API key '{scope.Name}' is bound to environment '{scope.Environment}' and cannot read another environment's audit trail.",
                    KeyVaultService.NormalizeEnvironment(environment));

            environment = scope.Environment;
        }

        var (rows, total) = await _audit.QueryAsync(
            key, environment, actorId, parsedAction, parsedOutcome, from, to, limit, offset);

        // Querying the trail is security-relevant in its own right.
        var actor = await _actor.GetAsync();
        await _audit.WriteAsync(AuditAction.ReadAudit, actor.Type, actor.Id,
            key: key ?? "", environment: environment ?? "",
            ipAddress: actor.IpAddress, itemCount: rows.Count);

        return Ok(new AuditEntryListResponse
        {
            Items = rows.Select(ToResponse).ToList(),
            TotalCount = total,
            Limit = Math.Clamp(limit, 1, 1000),
            Offset = Math.Max(0, offset)
        });
    }

    private static AuditEntryResponse ToResponse(AccessAuditLog a) => new()
    {
        Id = a.Id,
        Timestamp = a.Timestamp,
        Action = a.Action.ToString(),
        Key = a.Key,
        Environment = a.Environment,
        ActorType = a.ActorType.ToString(),
        ActorId = a.ActorId,
        Outcome = a.Outcome.ToString(),
        IpAddress = a.IpAddress ?? string.Empty,
        ItemCount = a.ItemCount
    };
}
