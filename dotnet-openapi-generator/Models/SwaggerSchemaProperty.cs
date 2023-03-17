using System.Text;
using System.Text.Json;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaPropertyAdditionalProperties
{
    public string? type { get; set; }
    public bool nullable { get; set; }
}

internal class SwaggerSchemaProperty
{
    [System.Text.Json.Serialization.JsonPropertyName("$ref")]
    public string? @ref { get; set; }
    public string? type { get; set; }
    public string? format { get; set; }
    public object? @default { get; set; }
    public bool nullable { get; set; }
    public bool? required { get; set; }
    public SwaggerSchemaPropertyAdditionalProperties? additionalProperties { get; set; }
    public System.Text.Json.JsonElement? items { get; set; }

    public string GetBody(string name)
    {
        StringBuilder builder = new("public ");
        
        builder.Append(ResolveType());

        if (nullable)
        {
            builder.Append('?');
        }

        builder.Append(' ')
               .Append(name[0..1].ToUpperInvariant())
               .Append(name[1..])
               .Append(" { get; set; }");

        return builder.ToString().TrimEnd();
    }

    private string? TypeToResolve => format ?? type ?? @ref;
    public string? ResolveType() => ResolveType(TypeToResolve, items, additionalProperties);

    private static string? ResolveType(string? typeToResolve, System.Text.Json.JsonElement? items, SwaggerSchemaPropertyAdditionalProperties? additionalProperties)
    {
        return typeToResolve switch
        {
            "date" or "date-time" => typeof(DateTime).FullName,
            "date-span" => typeof(TimeSpan).FullName,
            "boolean" => "bool",
            "int32" or "integer" => "int",
            "int64" => "long",
            "uri" => typeof(Uri).FullName,
            "uuid" => typeof(Guid).FullName,
            "binary" => typeof(Stream).FullName,
            "array" => typeof(List<>).FullName![..^2] + "<" + GetArrayType(items, additionalProperties) + ">",
            "object" when additionalProperties is not null => $"{typeof(Dictionary<,>).FullName![..^2]}<string, {ResolveType(additionalProperties.type, items, null)}>",
            null => "object",
            _ => typeToResolve.Replace("#/components/schemas/", "").AsSafeString()
        };
    }

    private static string? GetArrayType(JsonElement? items, SwaggerSchemaPropertyAdditionalProperties? additionalProperties)
    {
        if (items is null)
        {
            return "object";
        }

        return items.Value.TryGetProperty("type", out var arrayType)
                    ? ResolveType(arrayType.GetString(), items.Value.TryGetProperty("items", out var innerItems) ? innerItems : null, additionalProperties)
                    : ResolveType(items.Value.GetProperty("$ref").GetString(), null, additionalProperties);
    }

    public IEnumerable<string> GetComponents(IReadOnlyDictionary<string, SwaggerSchema> schemas, int depth)
    {
        var typeToResolve = TypeToResolve;
        var type = typeToResolve == "array" ? GetArrayType(items, additionalProperties) : ResolveType(TypeToResolve, items, additionalProperties);

        if (!string.IsNullOrWhiteSpace(type))
        {
            yield return type;

            if (schemas.TryGetValue(type, out var schema))
            {
                foreach (var usedType in schema.GetComponents(schemas, depth))
                {
                    yield return usedType;
                }
            }
        }
    }
}