using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using ProtoBuf;

namespace Tau.KeyVault.Formatters;

/// <summary>
/// ASP.NET Core output formatter that serializes responses as Protocol Buffers
/// when the client sends Accept: application/x-protobuf.
/// </summary>
public class ProtobufOutputFormatter : OutputFormatter
{
    public const string ProtobufMediaType = "application/x-protobuf";

    public ProtobufOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ProtobufMediaType));
    }

    protected override bool CanWriteType(Type? type)
    {
        if (type is null) return false;

        // Only serialize types that are annotated with [ProtoContract]
        return Attribute.IsDefined(type, typeof(ProtoContractAttribute));
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = ProtobufMediaType;

        if (context.Object is not null)
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, context.Object);
            stream.Position = 0;
            await stream.CopyToAsync(response.Body);
        }
    }
}
