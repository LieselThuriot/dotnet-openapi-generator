using System.Text.Json;
using System.Text.Json.Serialization;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaPropertyAdditionalProperties
{
    public string? type { get; set; }
    public bool nullable { get; set; }
}

internal class BooleanOrObjectConverter<T> : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.False)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}