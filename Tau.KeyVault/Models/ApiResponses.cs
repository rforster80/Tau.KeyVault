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
//  Generic responses
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class ErrorResponse
{
    [ProtoMember(1)] public string Error { get; set; } = string.Empty;
}
