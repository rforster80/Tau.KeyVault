using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Tau.KeyVault.Models;
using Tau.KeyVault.Services;

namespace Tau.KeyVault.Controllers;

/// <summary>
/// Manages per-environment API credentials.
/// <para>
/// Every endpoint here is Global-only: a credential bound to an environment cannot mint,
/// rotate, revoke or even enumerate credentials, so a leaked scoped key cannot escalate into
/// more credentials. Global callers are the keys configured in <c>appsettings.json</c>, which
/// are themselves rotated by editing configuration and restarting.
/// </para>
/// <para>
/// The whole controller is inert unless <c>EnableAPIKeyPerEnvironment</c> is true.
/// </para>
/// </summary>
[ApiController]
[Route("api/apikeys")]
[Produces("application/json", "application/x-protobuf")]
public class ApiKeyApiController : ControllerBase
{
    private readonly ApiKeyService _keys;
    private readonly CallerScopeAccessor _scopes;
    private readonly AuditService _audit;
    private readonly AuditActorAccessor _actor;

    public ApiKeyApiController(ApiKeyService keys, CallerScopeAccessor scopes,
        AuditService audit, AuditActorAccessor actor)
    {
        _keys = keys;
        _scopes = scopes;
        _audit = audit;
        _actor = actor;
    }

    /// <summary>Global-only, and only while the feature is enabled.</summary>
    private IActionResult? Gate()
    {
        if (!_keys.Enabled)
            return StatusCode(StatusCodes.Status409Conflict, new ErrorResponse
            {
                Error = $"Per-environment API keys are disabled. Set \"{ApiKeyService.EnableConfigKey}\": true in appsettings.json and restart."
            });

        var scope = _scopes.Current;
        if (!scope.IsGlobal)
            throw new ScopeViolationException(
                $"API key '{scope.Name}' is bound to environment '{scope.Environment}' and may not manage API keys. " +
                "Use a Global API key configured in appsettings.json.",
                scope.Environment);

        return null;
    }

    private async Task AuditAsync(AuditAction action, string name, string environment,
        AuditOutcome outcome = AuditOutcome.Success)
    {
        var actor = await _actor.GetAsync();
        await _audit.WriteAsync(action, actor.Type, actor.Id, key: name, environment: environment,
            outcome: outcome, ipAddress: actor.IpAddress, itemCount: 1);
    }

    /// <summary>List credentials. Secrets are never returned.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiKeyListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        if (Gate() is { } blocked) return blocked;

        var rows = await _keys.ListAsync();
        await AuditAsync(AuditAction.ListApiKeys, "", "");

        return Ok(new ApiKeyListResponse { Items = rows.Select(ToResponse).ToList() });
    }

    /// <summary>
    /// Mint a credential bound to one environment. The secret is in the response and is never
    /// retrievable again.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiKeySecretResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request)
    {
        if (Gate() is { } blocked) return blocked;

        try
        {
            // Audit before minting: an unrecordable credential must not come into existence.
            await AuditAsync(AuditAction.CreateApiKey, request.Name ?? "",
                KeyVaultService.NormalizeEnvironment(request.Environment ?? ""));

            var minted = await _keys.CreateAsync(request.Name ?? "", request.Environment ?? "");

            return StatusCode(StatusCodes.Status201Created, new ApiKeySecretResponse
            {
                Id = minted.Record.Id,
                Name = minted.Record.Name,
                Environment = minted.Record.Environment,
                Key = minted.PlaintextKey,
                Message = "Store this key now — it is hashed at rest and cannot be shown again."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>Replace a credential's secret. The previous key stops working immediately.</summary>
    [HttpPost("{id:int}/rotate")]
    [ProducesResponseType(typeof(ApiKeySecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rotate(int id)
    {
        if (Gate() is { } blocked) return blocked;

        var existing = await _keys.GetAsync(id);
        if (existing is null)
            return NotFound(new ErrorResponse { Error = $"API key {id} not found." });

        await AuditAsync(AuditAction.RotateApiKey, existing.Name, existing.Environment);

        var rotated = await _keys.RotateAsync(id);
        if (rotated is null)
            return NotFound(new ErrorResponse { Error = $"API key {id} not found." });

        return Ok(new ApiKeySecretResponse
        {
            Id = rotated.Value.Record.Id,
            Name = rotated.Value.Record.Name,
            Environment = rotated.Value.Record.Environment,
            Key = rotated.Value.PlaintextKey,
            Message = "Store this key now — the previous key is already invalid and this one cannot be shown again."
        });
    }

    /// <summary>Enable or disable a credential without deleting it, so the audit trail keeps its subject.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UpdateApiKeyRequest? request)
    {
        if (Gate() is { } blocked) return blocked;

        // proto3 omits default values, so { Enabled = false } serializes to zero bytes and
        // arrives as an empty body. That is a valid encoding of "all fields default", not a
        // malformed request — MVC would otherwise reject it before the formatter runs.
        request ??= new UpdateApiKeyRequest();

        var existing = await _keys.GetAsync(id);
        if (existing is null)
            return NotFound(new ErrorResponse { Error = $"API key {id} not found." });

        await AuditAsync(AuditAction.UpdateApiKey, existing.Name, existing.Environment);

        var updated = await _keys.SetEnabledAsync(id, request.Enabled);
        return updated is null
            ? NotFound(new ErrorResponse { Error = $"API key {id} not found." })
            : Ok(ToResponse(updated));
    }

    /// <summary>Permanently remove a credential.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(RevokeApiKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(int id)
    {
        if (Gate() is { } blocked) return blocked;

        var existing = await _keys.GetAsync(id);
        if (existing is null)
            return NotFound(new ErrorResponse { Error = $"API key {id} not found." });

        await AuditAsync(AuditAction.RevokeApiKey, existing.Name, existing.Environment);

        await _keys.RevokeAsync(id);
        return Ok(new RevokeApiKeyResponse
        {
            Message = $"API key '{existing.Name}' for environment '{existing.Environment}' revoked.",
            Id = id
        });
    }

    private static ApiKeyResponse ToResponse(EnvironmentApiKey k) => new()
    {
        Id = k.Id,
        Name = k.Name,
        Environment = k.Environment,
        Enabled = k.Enabled,
        CreatedAt = k.CreatedAt,
        LastRotatedAt = k.LastRotatedAt ?? default
    };
}
