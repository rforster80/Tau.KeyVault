using ProtoBuf;

namespace Tau.KeyVault.Models;

// ─────────────────────────────────────────────────────────────
//  Key entry responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class KeyEntryResponse
{
    [ProtoMember(1)] public string Key { get; set; } = string.Empty;
    [ProtoMember(2)] public string Value { get; set; } = string.Empty;
    [ProtoMember(3)] public string Environment { get; set; } = string.Empty;
    [ProtoMember(4)] public string DataType { get; set; } = "Text";
    [ProtoMember(5)] public bool IsSensitive { get; set; }
    [ProtoMember(6)] public DateTime UpdatedAt { get; set; }
}

[ProtoContract]
public class KeyEntryListResponse
{
    [ProtoMember(1)] public List<KeyEntryResponse> Items { get; set; } = new();
}

[ProtoContract]
public class TypedKeyEntryResponse
{
    [ProtoMember(1)] public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Value serialized as a string. Consumer interprets based on ValueType:
    /// Numeric → parse as decimal, Boolean → parse as bool, Csv → split on comma,
    /// Json → parse as JSON object, others → use as-is.
    /// </summary>
    [ProtoMember(2)] public string Value { get; set; } = string.Empty;
    [ProtoMember(3)] public string ValueType { get; set; } = "Text";
    [ProtoMember(4)] public bool IsSensitive { get; set; }
    [ProtoMember(5)] public string Environment { get; set; } = string.Empty;
    [ProtoMember(6)] public DateTime UpdatedAt { get; set; }
}

[ProtoContract]
public class TypedKeyEntryListResponse
{
    [ProtoMember(1)] public List<TypedKeyEntryResponse> Items { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────
//  Environment responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class EnvironmentListResponse
{
    [ProtoMember(1)] public List<string> Environments { get; set; } = new();
}

[ProtoContract]
public class DeleteEnvironmentResponse
{
    [ProtoMember(1)] public string Message { get; set; } = string.Empty;
    [ProtoMember(2)] public int DeletedKeys { get; set; }
}

[ProtoContract]
public class DeleteKeyResponse
{
    [ProtoMember(1)] public string Message { get; set; } = string.Empty;
    [ProtoMember(2)] public string Key { get; set; } = string.Empty;
    /// <summary>The environment the key was actually deleted from (normalized; blank = global).</summary>
    [ProtoMember(3)] public string Environment { get; set; } = string.Empty;
}

[ProtoContract]
public class RenameEnvironmentResponse
{
    [ProtoMember(1)] public string Message { get; set; } = string.Empty;
    [ProtoMember(2)] public int UpdatedKeys { get; set; }
}

// ─────────────────────────────────────────────────────────────
//  Import / Export responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class ExportPayloadResponse
{
    [ProtoMember(1)] public string Version { get; set; } = string.Empty;
    [ProtoMember(2)] public DateTime ExportDate { get; set; }
    [ProtoMember(3)] public string Environment { get; set; } = string.Empty;
    [ProtoMember(4)] public int KeyCount { get; set; }
    [ProtoMember(5)] public List<ExportKeyItemResponse> Keys { get; set; } = new();
}

[ProtoContract]
public class ExportKeyItemResponse
{
    [ProtoMember(1)] public string Key { get; set; } = string.Empty;
    [ProtoMember(2)] public string Value { get; set; } = string.Empty;
    [ProtoMember(3)] public string DataType { get; set; } = "Text";
    [ProtoMember(4)] public bool IsSensitive { get; set; }
}

[ProtoContract]
public class ImportResultResponse
{
    [ProtoMember(1)] public int Imported { get; set; }
    [ProtoMember(2)] public int Skipped { get; set; }
    [ProtoMember(3)] public string Message { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────
//  Per-environment API credential responses
// ─────────────────────────────────────────────────────────────

/// <summary>
/// A credential's metadata. Never carries the secret — the key is returned exactly once,
/// on creation and on rotation, in <see cref="ApiKeySecretResponse"/>.
/// </summary>
[ProtoContract]
public class ApiKeyResponse
{
    [ProtoMember(1)] public int Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public string Environment { get; set; } = string.Empty;
    [ProtoMember(4)] public bool Enabled { get; set; }
    [ProtoMember(5)] public DateTime CreatedAt { get; set; }
    [ProtoMember(6)] public DateTime LastRotatedAt { get; set; }
}

[ProtoContract]
public class ApiKeyListResponse
{
    [ProtoMember(1)] public List<ApiKeyResponse> Items { get; set; } = new();
}

/// <summary>
/// The one and only time a credential's secret is disclosed. It is stored hashed, so this
/// value cannot be recovered afterwards — a lost key must be rotated.
/// </summary>
[ProtoContract]
public class ApiKeySecretResponse
{
    [ProtoMember(1)] public int Id { get; set; }
    [ProtoMember(2)] public string Name { get; set; } = string.Empty;
    [ProtoMember(3)] public string Environment { get; set; } = string.Empty;
    /// <summary>Store this now; it is never shown again.</summary>
    [ProtoMember(4)] public string Key { get; set; } = string.Empty;
    [ProtoMember(5)] public string Message { get; set; } = string.Empty;
}

[ProtoContract]
public class RevokeApiKeyResponse
{
    [ProtoMember(1)] public string Message { get; set; } = string.Empty;
    [ProtoMember(2)] public int Id { get; set; }
}

// ─────────────────────────────────────────────────────────────
//  Access audit responses
// ─────────────────────────────────────────────────────────────

/// <summary>
/// One access audit row. Note the absence of any value field — the audit trail records
/// access to a key, never its contents.
/// </summary>
[ProtoContract]
public class AuditEntryResponse
{
    [ProtoMember(1)] public DateTime Timestamp { get; set; }
    [ProtoMember(2)] public string Action { get; set; } = string.Empty;
    [ProtoMember(3)] public string Key { get; set; } = string.Empty;
    [ProtoMember(4)] public string Environment { get; set; } = string.Empty;
    [ProtoMember(5)] public string ActorType { get; set; } = string.Empty;
    [ProtoMember(6)] public string ActorId { get; set; } = string.Empty;
    [ProtoMember(7)] public string Outcome { get; set; } = string.Empty;
    [ProtoMember(8)] public string IpAddress { get; set; } = string.Empty;
    [ProtoMember(9)] public int ItemCount { get; set; }
    [ProtoMember(10)] public int Id { get; set; }
}

[ProtoContract]
public class AuditEntryListResponse
{
    [ProtoMember(1)] public List<AuditEntryResponse> Items { get; set; } = new();
    /// <summary>Total rows matching the filter, ignoring limit/offset.</summary>
    [ProtoMember(2)] public int TotalCount { get; set; }
    [ProtoMember(3)] public int Limit { get; set; }
    [ProtoMember(4)] public int Offset { get; set; }
}

// ─────────────────────────────────────────────────────────────
//  Generic responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class ErrorResponse
{
    [ProtoMember(1)] public string Error { get; set; } = string.Empty;
}
