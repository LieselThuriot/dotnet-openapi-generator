using System.Text;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaProperties : Dictionary<string, SwaggerSchemaProperty>
{
    public IEnumerable<(string Key, SwaggerSchemaProperty Value)> Iterate(string? exclusion)
    {
        foreach (var (key, value) in this)
        {
            if (key != exclusion)
            {
                yield return (key, value);
            }
        }
    }

    public string GetBody(SwaggerAllOfs? allOf, bool supportRequiredProperties, string? jsonPropertyNameAttribute, SwaggerComponentSchemas schemas, string? exclusion)
    {
        StringBuilder builder = new();

        foreach (var item in Iterate(exclusion))
        {
            builder.Append('\t').AppendLine(item.Value.GetBody(item.Key, supportRequiredProperties, jsonPropertyNameAttribute));
        }

        builder.AppendLine()
               .Append('\t').AppendLine("System.Collections.Generic.IEnumerable<(string name, object? value)> __ICanIterate.IterateProperties()")
               .Append('\t').AppendLine("{");

        var yields = GetAllYields(allOf, schemas, exclusion).DistinctBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

        if (yields.Count == 0)
        {
            builder.Append('\t').Append('\t').AppendLine("return System.Linq.Enumerable.Empty<(string name, object? value)>();");
        }
        else
        {
            foreach (var (item, property) in yields)
            {
                builder.Append('\t').Append('\t')
                       .Append("yield return (\"")
                       .Append(item)
                       .Append("\", ");

                var propertyName = item[0..1].ToUpperInvariant() + item[1..];
                if (char.IsDigit(item[0]))
                {
                    propertyName = '_' + propertyName;
                }

                if (property.@ref is not null)
                {
                    if (schemas.TryGenerateFastEnumToString(property.ResolveType()!, propertyName, out var result))
                    {
                        builder.Append(result);
                    }
                    else
                    {
                        builder.Append(propertyName);
                    }
                }
                else
                {
                    builder.Append(propertyName);
                }

                builder.AppendLine(");");
            }
        }

        builder.Append('\t').AppendLine("}");

        return builder.ToString().TrimEnd();
    }

    private IEnumerable<(string Key, SwaggerSchemaProperty Value)> GetAllYields(SwaggerAllOfs? allOf, IReadOnlyDictionary<string, SwaggerSchema> schemas, string? discriminatorProperty)
    {
        if (allOf is not null)
        {
            foreach (var item in allOf)
            {
                var type = item.ResolveType();
                if (!string.IsNullOrEmpty(type) && schemas.TryGetValue(type, out var parentSchema))
                {
                    if (parentSchema.properties is not null)
                    {
                        foreach (var parentYield in parentSchema.properties.GetAllYields(parentSchema.allOf, schemas, parentSchema.discriminator?.propertyName))
                        {
                            yield return parentYield;
                        }
                    }
                }
            }
        }

        foreach (var item in Iterate(discriminatorProperty))
        {
            yield return item;
        }
    }
}