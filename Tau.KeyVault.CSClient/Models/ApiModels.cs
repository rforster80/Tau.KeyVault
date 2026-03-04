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
