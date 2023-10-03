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

    public string GetBody(SwaggerAllOfs? allOf, bool supportRequiredProperties, IReadOnlyDictionary<string, SwaggerSchema> schemas, string? exclusion)
    {
        StringBuilder builder = new();

        foreach (var item in Iterate(exclusion))
        {
            builder.Append('\t').AppendLine(item.Value.GetBody(item.Key, supportRequiredProperties));
        }

        builder.AppendLine()
               .Append('\t').AppendLine("System.Collections.Generic.IEnumerable<(string name, object? value)> __ICanIterate.IterateProperties()")
               .Append('\t').AppendLine("{");

        var yields = GetAllYields(allOf, schemas, exclusion).ToHashSet();

        if (yields.Count == 0)
        {
            builder.Append('\t').Append('\t').AppendLine("return System.Linq.Enumerable.Empty<(string name, object? value)>();");
        }
        else
        {
            foreach (var item in yields)
            {
                builder.Append('\t').Append('\t')
                       .Append("yield return (\"")
                       .Append(item)
                       .Append("\", ")
                       .Append(item[0..1].ToUpperInvariant()).Append(item[1..])
                       .AppendLine(");");
            }
        }

        builder.Append('\t').AppendLine("}");

        return builder.ToString().TrimEnd();
    }

    private IEnumerable<string> GetAllYields(SwaggerAllOfs? allOf, IReadOnlyDictionary<string, SwaggerSchema> schemas, string? discriminatorProperty)
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

        foreach (var (key, _) in Iterate(discriminatorProperty))
        {
            yield return key;
        }
    }
}