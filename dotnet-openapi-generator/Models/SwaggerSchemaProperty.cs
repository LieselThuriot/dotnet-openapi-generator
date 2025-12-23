using System.Text;

namespace dotnet.openapi.generator;

internal sealed class SwaggerSchemaProperty
{
    [System.Text.Json.Serialization.JsonPropertyName("$ref")]
    public string? @ref { get; set; }
    public string? type { get; set; }
    public string? format { get; set; }
    public object? @default { get; set; }
    public bool nullable { get; set; }
    //public bool? required { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(BooleanOrObjectConverter<SwaggerSchemaPropertyAdditionalProperties>))]
    public SwaggerSchemaPropertyAdditionalProperties? additionalProperties { get; set; }
    public System.Text.Json.JsonElement? items { get; set; }
    public SwaggerSchemaProperties? properties { get; set; }

    public string GetBody(string name, string parentName, bool supportRequiredProperties, string? jsonPropertyNameAttribute)
    {
        StringBuilder builder = new();

        bool startsWithDigit = char.IsDigit(name[0]);
        bool isClassName = StringComparer.OrdinalIgnoreCase.Equals(name, parentName);
        string safeName = name.AsSafeCSharpName("@", "_").Replace("-", "_");
        bool isInvalidName = !StringComparer.OrdinalIgnoreCase.Equals(safeName, name);

        if (startsWithDigit || isClassName || isInvalidName)
        {
            if (!string.IsNullOrWhiteSpace(jsonPropertyNameAttribute))
            {
                builder.Append('[')
                       .Append(jsonPropertyNameAttribute.Replace("{name}", name))
                       .Append(']');

                if (isInvalidName)
                {
                    name = safeName;
                    isClassName = false;
                }
            }
            else
            {
                ErrorContext.AddError($"Property '{name}' in schema '{parentName}' conflicts with C# naming conventions but no JsonPropertyNameAttribute was provided.");
                startsWithDigit = false;
                isClassName = false;
            }
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

        if (startsWithDigit)
        {
            builder.Append('_');
        }

        builder.Append(name[0..1].ToUpperInvariant())
               .Append(name[1..]);

        if (isClassName)
        {
            builder.Append("Property");
        }

        builder.Append(" { get; set; }");

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
        string? resolvedType = format is "array" || type is "array"
                                ? items.ResolveArrayType(additionalProperties)
                                : ResolveType();

        if (!string.IsNullOrWhiteSpace(resolvedType))
        {
            yield return resolvedType;

            if (schemas.TryGetValue(resolvedType, out var schema))
            {
                foreach (string usedType in schema.GetComponents(schemas, depth))
                {
                    yield return usedType;
                }
            }
        }
    }
}