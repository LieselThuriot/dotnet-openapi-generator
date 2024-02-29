using System.Text;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaProperty
{
    [System.Text.Json.Serialization.JsonPropertyName("$ref")]
    public string? @ref { get; set; }
    public string? type { get; set; }
    public string? format { get; set; }
    public object? @default { get; set; }
    public bool nullable { get; set; }
    //public bool? required { get; set; }
    public SwaggerSchemaPropertyAdditionalProperties? additionalProperties { get; set; }
    public System.Text.Json.JsonElement? items { get; set; }

    public string GetBody(string name, bool supportRequiredProperties, string? jsonPropertyNameAttribute)
    {
        StringBuilder builder = new();

        bool startsWithDigit = char.IsDigit(name[0]);

        if (startsWithDigit && jsonPropertyNameAttribute is not null)
        {
            builder.Append('[')
                   .Append(jsonPropertyNameAttribute.Replace("{name}", name))
                   .Append(']');
        }

        builder.Append("public ");

        if (supportRequiredProperties && (!nullable /*|| required.GetValueOrDefault()*/))
        {
            builder.Append("required ");
        }

        builder.Append(ResolveType());

        if (nullable)
        {
            builder.Append('?');
        }

        builder.Append(' ');

        if (char.IsDigit(name[0]))
        {
            builder.Append('_');
        }

        builder.Append(name[0..1].ToUpperInvariant())
               .Append(name[1..])
               .Append(" { get; set; }");

        return builder.ToString().TrimEnd();
    }

    public string? ResolveType()
    {
        if (format is not null)
        {
            string? result = format.ResolveType(format.Contains("#/components/schemas/"), items, additionalProperties);
            if (result is not null)
            {
                return result;
            }
        }

        return (type ?? @ref).ResolveType(items, additionalProperties);
    }

    public IEnumerable<string> GetComponents(IReadOnlyDictionary<string, SwaggerSchema> schemas, int depth)
    {
        string? resolvedType = format == "array" || type == "array"
                                ? items.ResolveArrayType(additionalProperties)
                                : ResolveType();

        if (!string.IsNullOrWhiteSpace(resolvedType))
        {
            yield return resolvedType;

            if (schemas.TryGetValue(resolvedType, out var schema))
            {
                foreach (var usedType in schema.GetComponents(schemas, depth))
                {
                    yield return usedType;
                }
            }
        }
    }
}