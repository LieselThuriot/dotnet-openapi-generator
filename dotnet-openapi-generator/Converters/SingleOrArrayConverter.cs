using System.Text.Json;
using System.Text.Json.Serialization;

namespace dotnet.openapi.generator.Converters;

internal sealed class SingleOrArrayConverter<T> : JsonConverter<T>
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);

            JsonElement root = document.RootElement;

            if (root.GetArrayLength() > 0)
            {
                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind is not JsonValueKind.Null)
                    {
                        return element.Deserialize<T>(options);
                    }
                }
            }
            else
            {
                return default;
            }

            throw new JsonException("Unable to deserialize array");
        }
        else
        {
            return JsonSerializer.Deserialize<T>(ref reader, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
