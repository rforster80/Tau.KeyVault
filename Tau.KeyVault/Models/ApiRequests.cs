using ProtoBuf;

namespace Tau.KeyVault.Models;

// ─────────────────────────────────────────────────────────────
//  API request DTOs — support both JSON and Protobuf input
// ─────────────────────────────────────────────────────────────

[ProtoContract]
public class UpsertRequest
{
    [ProtoMember(1)] public string Key { get; set; } = string.Empty;
    [ProtoMember(2)] public string? Value { get; set; }
    [ProtoMember(3)] public string? Environment { get; set; }
    [ProtoMember(4)] public string? DataType { get; set; }
    [ProtoMember(5)] public bool? IsSensitive { get; set; }
}

[ProtoContract]
public class RenameRequest
{
    [ProtoMember(1)] public string NewName { get; set; } = string.Empty;
}

[ProtoContract]
public class ImportRequest
{
    [ProtoMember(1)] public string? Environment { get; set; }
    [ProtoMember(2)] public string Mode { get; set; } = "AddMissing";
    [ProtoMember(3)] public List<ImportKeyItem> Keys { get; set; } = new();
}

[ProtoContract]
public class ImportKeyItem
{
    [ProtoMember(1)] public string Key { get; set; } = string.Empty;
    [ProtoMember(2)] public string Value { get; set; } = string.Empty;
    [ProtoMember(3)] public string DataType { get; set; } = "Text";
    [ProtoMember(4)] public bool IsSensitive { get; set; }
}

/// <summary>Mint a credential bound to one environment.</summary>
[ProtoContract]
public class CreateApiKeyRequest
{
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;
    /// <summary>Must be a named environment; Global access comes from appsettings.json.</summary>
    [ProtoMember(2)] public string Environment { get; set; } = string.Empty;
}

/// <summary>Enable or disable a credential without deleting it.</summary>
[ProtoContract]
public class UpdateApiKeyRequest
{
    [ProtoMember(1)] public bool Enabled { get; set; }
}
