using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tau.KeyVault.Models;
using Tau.KeyVault.Services;

namespace Tau.KeyVault.Controllers;

/// <summary>
/// Manages key-value pairs across environments.
/// Keys resolve with global fallback — if a key is not found in the
/// requested environment, the global (blank environment) value is returned.
/// Supports JSON (default) and Protobuf (Accept: application/x-protobuf).
/// </summary>
[ApiController]
[Route("api/keys")]
[Produces("application/json", "application/x-protobuf")]
public class KeyVaultApiController : ControllerBase
{
    private readonly KeyVaultService _vault;
    private readonly NotificationDispatchService _dispatch;

    public KeyVaultApiController(KeyVaultService vault, NotificationDispatchService dispatch)
    {
        _vault = vault;
        _dispatch = dispatch;
    }

    // ───────────────────────────────────────────────
    //  Standard endpoints (Value always returned as string)
    // ───────────────────────────────────────────────

    /// <summary>
    /// List all keys for an environment (with global fallback).
    /// When no environment is specified, returns keys for the global (blank) environment.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(KeyEntryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? environment, [FromQuery] bool raw = false)
    {
        var env = environment ?? "";
        List<KeyEntry> keys;

        if (raw)
            keys = await _vault.GetKeysAsync(env);
        else
            keys = await _vault.ResolveAllKeysAsync(env);

        return Ok(ToKeyEntryListResponse(keys));
    }

    /// <summary>
    /// List all keys across all environments (no filtering).
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(KeyEntryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllEnvironments()
    {
        var keys = await _vault.GetKeysAsync(null);
        return Ok(ToKeyEntryListResponse(keys));
    }

    /// <summary>
    /// Get a single key by name (with global fallback).
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(KeyEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string key, [FromQuery] string environment = "")
    {
        var entry = await _vault.ResolveKeyAsync(key, environment);
        if (entry is null)
            return NotFound(new ErrorResponse { Error = $"Key '{key}' not found for environment '{environment}' (including global fallback)." });

        return Ok(new KeyEntryResponse
        {
            Key = entry.Key,
            Value = entry.Value,
            Environment = entry.Environment,
            DataType = entry.DataType.ToString(),
            IsSensitive = entry.IsSensitive,
            UpdatedAt = entry.UpdatedAt
        });
    }

    /// <summary>
    /// Create or update a key-value pair.
    /// </summary>
    /// <remarks>
    /// DataType is optional and defaults to "Text" for backward compatibility.
    /// Valid types: Text, Code, Numeric, Boolean, Date, Time, DateTime, Json, Csv.
    /// </remarks>
    [HttpPut]
    [ProducesResponseType(typeof(KeyEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert([FromBody] UpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest(new ErrorResponse { Error = "Key is required." });

        // Parse DataType — default to Text for backward compat
        var dataType = DataType.Text;
        if (!string.IsNullOrEmpty(request.DataType))
        {
            if (!Enum.TryParse<DataType>(request.DataType, ignoreCase: true, out var parsed))
                return BadRequest(new ErrorResponse { Error = $"Invalid DataType '{request.DataType}'. Valid values: {string.Join(", ", Enum.GetNames<DataType>())}" });
            dataType = parsed;
        }

        try
        {
            var entry = await _vault.UpsertKeyAsync(
                request.Key,
                request.Value ?? "",
                request.Environment ?? "",
                dataType,
                request.IsSensitive ?? false);

            // Dispatch notifications (errors are logged internally, never thrown)
            await _dispatch.DispatchAsync(entry.Environment, entry.Key);

            return Ok(new KeyEntryResponse
            {
                Key = entry.Key,
                Value = entry.Value,
                Environment = entry.Environment,
                DataType = entry.DataType.ToString(),
                IsSensitive = entry.IsSensitive,
                UpdatedAt = entry.UpdatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    // ───────────────────────────────────────────────
    //  Typed endpoints (Value returned as native type)
    // ───────────────────────────────────────────────

    /// <summary>
    /// Get a single key with the value returned as its native data type.
    /// </summary>
    /// <remarks>
    /// For protobuf responses, the value is always a string — interpret based on ValueType.
    /// For JSON responses: Numeric → number, Boolean → bool, Csv → string[], Json → object, etc.
    /// </remarks>
    [HttpGet("typed/{key}")]
    [ProducesResponseType(typeof(TypedKeyEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTyped(string key, [FromQuery] string environment = "")
    {
        var entry = await _vault.ResolveKeyAsync(key, environment);
        if (entry is null)
            return NotFound(new ErrorResponse { Error = $"Key '{key}' not found for environment '{environment}' (including global fallback)." });

        // For protobuf: always return string value with ValueType indicator
        if (IsProtobufRequest())
        {
            return Ok(new TypedKeyEntryResponse
            {
                Key = entry.Key,
                Value = entry.Value,
                ValueType = entry.DataType.ToString(),
                IsSensitive = entry.IsSensitive,
                Environment = entry.Environment,
                UpdatedAt = entry.UpdatedAt
            });
        }

        // JSON: return native typed values (backward compatible)
        return Ok(new
        {
            entry.Key,
            Value = ConvertToTypedValue(entry.Value, entry.DataType),
            ValueType = entry.DataType.ToString(),
            entry.IsSensitive,
            entry.Environment,
            entry.UpdatedAt
        });
    }

    /// <summary>
    /// List all keys with values returned as their native data types.
    /// When no environment is specified, returns keys for the global (blank) environment.
    /// </summary>
    [HttpGet("typed")]
    [ProducesResponseType(typeof(TypedKeyEntryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllTyped([FromQuery] string? environment, [FromQuery] bool raw = false)
    {
        var env = environment ?? "";
        List<KeyEntry> keys;

        if (raw)
            keys = await _vault.GetKeysAsync(env);
        else
            keys = await _vault.ResolveAllKeysAsync(env);

        // For protobuf: always return string values with ValueType indicators
        if (IsProtobufRequest())
        {
            var response = new TypedKeyEntryListResponse
            {
                Items = keys.Select(k => new TypedKeyEntryResponse
                {
                    Key = k.Key,
                    Value = k.Value,
                    ValueType = k.DataType.ToString(),
                    IsSensitive = k.IsSensitive,
                    Environment = k.Environment,
                    UpdatedAt = k.UpdatedAt
                }).ToList()
            };
            return Ok(response);
        }

        // JSON: return native typed values (backward compatible)
        return Ok(keys.Select(k => new
        {
            k.Key,
            Value = ConvertToTypedValue(k.Value, k.DataType),
            ValueType = k.DataType.ToString(),
            k.IsSensitive,
            k.Environment,
            k.UpdatedAt
        }));
    }

    /// <summary>
    /// List all keys across all environments with values returned as their native data types (no filtering).
    /// </summary>
    [HttpGet("typed/all")]
    [ProducesResponseType(typeof(TypedKeyEntryListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllTypedAllEnvironments()
    {
        var keys = await _vault.GetKeysAsync(null);

        if (IsProtobufRequest())
        {
            var response = new TypedKeyEntryListResponse
            {
                Items = keys.Select(k => new TypedKeyEntryResponse
                {
                    Key = k.Key,
                    Value = k.Value,
                    ValueType = k.DataType.ToString(),
                    IsSensitive = k.IsSensitive,
                    Environment = k.Environment,
                    UpdatedAt = k.UpdatedAt
                }).ToList()
            };
            return Ok(response);
        }

        return Ok(keys.Select(k => new
        {
            k.Key,
            Value = ConvertToTypedValue(k.Value, k.DataType),
            ValueType = k.DataType.ToString(),
            k.IsSensitive,
            k.Environment,
            k.UpdatedAt
        }));
    }

    // ───────────────────────────────────────────────
    //  Environment management
    // ───────────────────────────────────────────────

    /// <summary>
    /// List all known environments.
    /// </summary>
    [HttpGet("environments")]
    [ProducesResponseType(typeof(EnvironmentListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnvironments()
    {
        var envs = await _vault.GetEnvironmentsAsync();
        return Ok(new EnvironmentListResponse { Environments = envs });
    }

    /// <summary>
    /// Delete an environment and ALL its key-value pairs.
    /// </summary>
    [HttpDelete("environments/{environment}")]
    [ProducesResponseType(typeof(DeleteEnvironmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteEnvironment(string environment)
    {
        var env = string.IsNullOrEmpty(environment) ? "" : environment.Trim().ToUpperInvariant();
        try
        {
            var count = await _vault.DeleteEnvironmentAsync(env);
            return Ok(new DeleteEnvironmentResponse
            {
                Message = $"Environment '{env}' deleted with {count} key(s).",
                DeletedKeys = count
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Rename an environment (updates all associated keys).
    /// </summary>
    [HttpPut("environments/{environment}/rename")]
    [ProducesResponseType(typeof(RenameEnvironmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RenameEnvironment(string environment, [FromBody] RenameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewName))
            return BadRequest(new ErrorResponse { Error = "New environment name is required." });

        var env = string.IsNullOrEmpty(environment) ? "" : environment.Trim().ToUpperInvariant();
        var newEnv = request.NewName.Trim().ToUpperInvariant();
        try
        {
            var count = await _vault.RenameEnvironmentAsync(env, newEnv);
            return Ok(new RenameEnvironmentResponse
            {
                Message = $"Environment renamed from '{env}' to '{newEnv}'. {count} key(s) updated.",
                UpdatedKeys = count
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    // ───────────────────────────────────────────────
    //  Export / Import
    // ───────────────────────────────────────────────

    /// <summary>
    /// Export all keys for an environment as a portable payload.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(ExportPayloadResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportEnvironment([FromQuery] string environment = "")
    {
        var payload = await _vault.ExportEnvironmentAsync(environment);
        return Ok(new ExportPayloadResponse
        {
            Version = payload.Version,
            ExportDate = payload.ExportDate,
            Environment = payload.Environment,
            KeyCount = payload.KeyCount,
            Keys = payload.Keys.Select(k => new ExportKeyItemResponse
            {
                Key = k.Key,
                Value = k.Value,
                DataType = k.DataType,
                IsSensitive = k.IsSensitive
            }).ToList()
        });
    }

    /// <summary>
    /// Import keys into an environment.
    /// </summary>
    /// <remarks>
    /// Mode determines how existing keys are handled:
    /// - DeleteAll: Remove all existing keys in the environment first, then import.
    /// - Overwrite: Update existing keys, add new ones. Keys not in the import are preserved.
    /// - AddMissing: Only add keys that don't already exist; skip existing ones.
    /// </remarks>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportEnvironment([FromBody] ImportRequest request)
    {
        if (request.Keys is null || request.Keys.Count == 0)
            return BadRequest(new ErrorResponse { Error = "No keys provided for import." });

        if (!Enum.TryParse<ImportMode>(request.Mode, ignoreCase: true, out var mode))
            return BadRequest(new ErrorResponse { Error = $"Invalid import mode '{request.Mode}'. Valid values: {string.Join(", ", Enum.GetNames<ImportMode>())}" });

        try
        {
            // Map ImportKeyItem → ExportKeyItem for the service layer
            var exportKeys = request.Keys
                .Select(k => new ExportKeyItem(k.Key, k.Value, k.DataType, k.IsSensitive))
                .ToList();

            var (imported, skipped) = await _vault.ImportKeysAsync(
                request.Environment ?? "", exportKeys, mode);

            return Ok(new ImportResultResponse
            {
                Imported = imported,
                Skipped = skipped,
                Message = $"Imported {imported} key(s), skipped {skipped}."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    // ───────────────────────────────────────────────
    //  Proto schema endpoint
    // ───────────────────────────────────────────────

    /// <summary>
    /// Download the auto-generated .proto schema file for use in other projects.
    /// </summary>
    [HttpGet("proto")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public IActionResult GetProtoSchema([FromServices] IWebHostEnvironment env)
    {
        var protoPath = Path.Combine(env.WebRootPath, "proto", "keyvault.proto");
        if (!System.IO.File.Exists(protoPath))
            return NotFound(new ErrorResponse { Error = "Proto schema file not found. It is generated at application startup." });

        var content = System.IO.File.ReadAllText(protoPath);
        return Content(content, "text/plain");
    }

    // ───────────────────────────────────────────────
    //  Helpers
    // ───────────────────────────────────────────────

    private static KeyEntryListResponse ToKeyEntryListResponse(List<KeyEntry> keys) =>
        new()
        {
            Items = keys.Select(k => new KeyEntryResponse
            {
                Key = k.Key,
                Value = k.Value,
                Environment = k.Environment,
                DataType = k.DataType.ToString(),
                IsSensitive = k.IsSensitive,
                UpdatedAt = k.UpdatedAt
            }).ToList()
        };

    /// <summary>
    /// Checks whether the current request expects protobuf response.
    /// </summary>
    private bool IsProtobufRequest()
    {
        var accept = Request.Headers.Accept.ToString();
        return accept.Contains("application/x-protobuf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a stored string value to its native .NET type based on DataType.
    /// Used for JSON typed endpoints only (protobuf always uses string).
    /// </summary>
    private static object ConvertToTypedValue(string value, DataType dataType) => dataType switch
    {
        DataType.Numeric => decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var num) ? num : value,
        DataType.Boolean => value.Trim().ToLowerInvariant() is "true" or "1" or "yes",
        DataType.Json => TryParseJson(value),
        DataType.Csv => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        _ => value // Text, Code, Date, Time, DateTime → returned as string
    };

    private static object TryParseJson(string value)
    {
        try { return JsonSerializer.Deserialize<JsonElement>(value); }
        catch { return value; }
    }
}
