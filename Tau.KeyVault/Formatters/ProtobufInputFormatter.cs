using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using ProtoBuf;

namespace Tau.KeyVault.Formatters;

/// <summary>
/// ASP.NET Core input formatter that deserializes Protocol Buffer request bodies
/// when the client sends Content-Type: application/x-protobuf.
/// </summary>
public class ProtobufInputFormatter : InputFormatter
{
    public const string ProtobufMediaType = "application/x-protobuf";

    public ProtobufInputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ProtobufMediaType));
    }

    protected override bool CanReadType(Type type)
    {
        // Only deserialize types annotated with [ProtoContract]
        return Attribute.IsDefined(type, typeof(ProtoContractAttribute));
    }

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        var request = context.HttpContext.Request;
        var type = context.ModelType;

        try
        {
            using var stream = new MemoryStream();
            await request.Body.CopyToAsync(stream);
            stream.Position = 0;

            var result = Serializer.Deserialize(type, stream);
            return await InputFormatterResult.SuccessAsync(result);
        }
        catch (Exception ex)
        {
            context.ModelState.AddModelError(string.Empty, $"Failed to deserialize protobuf: {ex.Message}");
            return await InputFormatterResult.FailureAsync();
        }
    }
}
