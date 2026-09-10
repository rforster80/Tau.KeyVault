using System.Text.Json.Serialization;
using ProtoBuf;

namespace Tau.KeyVault.Client.Models;

// ─────────────────────────────────────────────────────────────
//  Key entry responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class KeyEntryResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [ProtoMember(4)]
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "Text";

    [ProtoMember(5)]
    [JsonPropertyName("isSensitive")]
    public bool IsSensitive { get; set; }

    [ProtoMember(6)]
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

[ProtoContract]
public class KeyEntryListResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("items")]
    public List<KeyEntryResponse> Items { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────
//  Environment responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class EnvironmentListResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("environments")]
    public List<string> Environments { get; set; } = new();
}

[ProtoContract]
public class DeleteEnvironmentResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("deletedKeys")]
    public int DeletedKeys { get; set; }
}

/// <summary>Metadata for a per-environment API credential. Never carries the secret.</summary>
[ProtoContract]
public class ApiKeyResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [ProtoMember(4)]
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [ProtoMember(5)]
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [ProtoMember(6)]
    [JsonPropertyName("lastRotatedAt")]
    public DateTime LastRotatedAt { get; set; }
}

[ProtoContract]
public class ApiKeyListResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("items")]
    public List<ApiKeyResponse> Items { get; set; } = new();
}

/// <summary>
/// The one and only disclosure of a credential's secret, returned by create and rotate.
/// The server stores only a hash, so <see cref="Key"/> cannot be recovered later.
/// </summary>
[ProtoContract]
public class ApiKeySecretResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    /// <summary>Store this now; it is never shown again.</summary>
    [ProtoMember(4)]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(5)]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

[ProtoContract]
public class RevokeApiKeyResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

[ProtoContract]
public class CreateApiKeyRequest
{
    [ProtoMember(1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;
}

[ProtoContract]
public class UpdateApiKeyRequest
{
    [ProtoMember(1)]
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

/// <summary>
/// One access audit row: who touched which key, in which environment, and when.
/// There is deliberately no value field — the trail records access, never contents.
/// </summary>
[ProtoContract]
public class AuditEntryResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(4)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [ProtoMember(5)]
    [JsonPropertyName("actorType")]
    public string ActorType { get; set; } = string.Empty;

    /// <summary>API key name or admin username. Never the API key itself.</summary>
    [ProtoMember(6)]
    [JsonPropertyName("actorId")]
    public string ActorId { get; set; } = string.Empty;

    [ProtoMember(7)]
    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [ProtoMember(8)]
    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [ProtoMember(9)]
    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [ProtoMember(10)]
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

[ProtoContract]
public class AuditEntryListResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("items")]
    public List<AuditEntryResponse> Items { get; set; } = new();

    /// <summary>Total rows matching the filter, ignoring limit/offset.</summary>
    [ProtoMember(2)]
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [ProtoMember(4)]
    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

[ProtoContract]
public class DeleteKeyResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>The environment the key was actually deleted from (normalized; blank = global).</summary>
    [ProtoMember(3)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;
}

[ProtoContract]
public class RenameEnvironmentResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("updatedKeys")]
    public int UpdatedKeys { get; set; }
}

// ─────────────────────────────────────────────────────────────
//  Import / Export responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class ExportPayloadResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("exportDate")]
    public DateTime ExportDate { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [ProtoMember(4)]
    [JsonPropertyName("keyCount")]
    public int KeyCount { get; set; }

    [ProtoMember(5)]
    [JsonPropertyName("keys")]
    public List<ExportKeyItemResponse> Keys { get; set; } = new();
}

[ProtoContract]
public class ExportKeyItemResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "Text";

    [ProtoMember(4)]
    [JsonPropertyName("isSensitive")]
    public bool IsSensitive { get; set; }
}

[ProtoContract]
public class ImportResultResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("imported")]
    public int Imported { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────
//  Error response
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class ErrorResponse
{
    [ProtoMember(1)]
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────
//  Request DTOs
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class UpsertRequest
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [ProtoMember(3)]
    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [ProtoMember(4)]
    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }

    [ProtoMember(5)]
    [JsonPropertyName("isSensitive")]
    public bool? IsSensitive { get; set; }
}

[ProtoContract]
public class RenameRequest
{
    [ProtoMember(1)]
    [JsonPropertyName("newName")]
    public string NewName { get; set; } = string.Empty;
}

[ProtoContract]
public class ImportRequest
{
    [ProtoMember(1)]
    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [ProtoMember(2)]
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "AddMissing";

    [ProtoMember(3)]
    [JsonPropertyName("keys")]
    public List<ImportKeyItem> Keys { get; set; } = new();
}

[ProtoContract]
public class ImportKeyItem
{
    [ProtoMember(1)]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [ProtoMember(2)]
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [ProtoMember(3)]
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "Text";

    [ProtoMember(4)]
    [JsonPropertyName("isSensitive")]
    public bool IsSensitive { get; set; }
}
