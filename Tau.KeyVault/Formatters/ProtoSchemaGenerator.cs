using System.Text;
using ProtoBuf;
using ProtoBuf.Meta;
using Tau.KeyVault.Models;

namespace Tau.KeyVault.Formatters;

/// <summary>
/// Generates a consolidated .proto schema file from all [ProtoContract]-annotated
/// API models at application startup. The schema is saved to wwwroot/proto/keyvault.proto
/// and can be downloaded via GET /api/keys/proto.
/// </summary>
public static class ProtoSchemaGenerator
{
    public static void Generate(string webRootPath)
    {
        var protoDir = Path.Combine(webRootPath, "proto");
        Directory.CreateDirectory(protoDir);
        var protoPath = Path.Combine(protoDir, "keyvault.proto");

        // Register all protobuf-annotated types in the runtime model
        var model = RuntimeTypeModel.Default;

        // Ensure all types are registered (they should be via [ProtoContract] but let's be explicit)
        var protoTypes = new[]
        {
            // Response types
            typeof(KeyEntryResponse),
            typeof(KeyEntryListResponse),
            typeof(TypedKeyEntryResponse),
            typeof(TypedKeyEntryListResponse),
            typeof(EnvironmentListResponse),
            typeof(DeleteEnvironmentResponse),
            typeof(RenameEnvironmentResponse),
            typeof(ExportPayloadResponse),
            typeof(ExportKeyItemResponse),
            typeof(ImportResultResponse),
            typeof(ErrorResponse),
            // Request types
            typeof(UpsertRequest),
            typeof(RenameRequest),
            typeof(ImportRequest),
            typeof(ImportKeyItem),
        };

        foreach (var type in protoTypes)
        {
            if (!model.IsDefined(type))
                model.Add(type, applyDefaultBehaviour: true);
        }

        // Generate the .proto schema
        var schema = model.GetSchema(null, ProtoSyntax.Proto3);

        // Post-process: add package and better header
        var sb = new StringBuilder();
        sb.AppendLine("// ─────────────────────────────────────────────────────────────");
        sb.AppendLine("// Tau Key Vault — Protocol Buffers Schema");
        sb.AppendLine("// Auto-generated at application startup. Do not edit manually.");
        sb.AppendLine("// Download: GET /api/keys/proto");
        sb.AppendLine("// ─────────────────────────────────────────────────────────────");
        sb.AppendLine();

        // Insert package declaration after syntax line
        var lines = schema.Split('\n');
        foreach (var line in lines)
        {
            sb.AppendLine(line.TrimEnd());
            if (line.TrimStart().StartsWith("syntax"))
            {
                sb.AppendLine();
                sb.AppendLine("package tau.keyvault;");
            }
        }

        File.WriteAllText(protoPath, sb.ToString());
    }
}
