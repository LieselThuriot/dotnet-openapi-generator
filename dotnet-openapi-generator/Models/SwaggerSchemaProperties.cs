using System.Text;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaProperties : Dictionary<string, SwaggerSchemaProperty>
{
    public string GetBody(SwaggerAllOfs? allOf, bool supportRequiredProperties, IReadOnlyDictionary<string, SwaggerSchema> schemas)
    {
        StringBuilder builder = new();

        foreach (var item in this)
        {
            builder.Append('\t').AppendLine(item.Value.GetBody(item.Key, supportRequiredProperties));
        }

        builder.AppendLine()
               .Append('\t').AppendLine("System.Collections.Generic.IEnumerable<(string name, object? value)> __ICanIterate.IterateProperties()")
               .Append('\t').AppendLine("{");

        var yields = GetAllYields(allOf, schemas).ToHashSet();

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

    private IEnumerable<string> GetAllYields(SwaggerAllOfs? allOf, IReadOnlyDictionary<string, SwaggerSchema> schemas)
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
                        foreach (var parentYield in parentSchema.properties.GetAllYields(parentSchema.allOf, schemas))
                        {
                            yield return parentYield;
                        }
                    }
                }
            }
        }

        foreach (var key in Keys)
        {
            yield return key;
        }
    }

    private void AppendAllYields(SwaggerAllOfs? allOf, IReadOnlyDictionary<string, SwaggerSchema> schemas, StringBuilder builder)
    {
        if (allOf is not null)
        {
            AppendAllOfYields(allOf, schemas, builder);
        }

        AppendYields(builder);
    }

    private static void AppendAllOfYields(SwaggerAllOfs allOf, IReadOnlyDictionary<string, SwaggerSchema> schemas, StringBuilder builder)
    {
        foreach (var item in allOf)
        {
            var type = item.ResolveType();
            if (!string.IsNullOrEmpty(type) && schemas.TryGetValue(type, out var parentSchema))
            {
                parentSchema.properties?.AppendYields(builder);
            }
        }
    }

    private void AppendYields(StringBuilder builder)
    {
        foreach (var item in Keys)
        {
            builder.Append('\t').Append('\t')
                   .Append("yield return (\"")
                   .Append(item)
                   .Append("\", ")
                   .Append(item[0..1].ToUpperInvariant()).Append(item[1..])
                   .AppendLine(");");
        }
    }
}
