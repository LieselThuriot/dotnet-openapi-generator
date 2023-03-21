namespace dotnet.openapi.generator;

internal class SwaggerSchema
{
    public SwaggerSchemaEnum? @enum { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("flagged-enum")]
    public SwaggerSchemaFlaggedEnum? FlaggedEnum { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("x-enumNames")]
    public List<string>? EnumNames { get; set; }

    public SwaggerSchemaProperties? properties { get; set; }

    public SwaggerAllOfs? allOf { get; set; }

    private string GetDefinitionType(string name, IEnumerable<SwaggerSchema> schemas)
    {
        if (@enum is not null)
        {
            return "enum";
        }

        if (IsTopLevel(name, schemas))
        {
            return "sealed class";
        }

        return "class";
    }

    private bool IsTopLevel(string name, IEnumerable<SwaggerSchema> schemas)
    {
        foreach (var schema in schemas)
        {
            if (schema.allOf is not null)
            {
                foreach (var allOf in schema.allOf)
                {
                    if (allOf.ResolveType() == name)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public string? GetCtor(string name, string? jsonConstructorAttribute, bool supportRequiredProperties, IReadOnlyDictionary<string, SwaggerSchema> schemas)
    {
        if (properties is null)
        {
            return null;
        }

        HashSet<(string key, SwaggerSchemaProperty value)> baseProperties = new();
        HashSet<(string key, SwaggerSchemaProperty value)> requiredProperties = GetRequiredProperties();

        if (allOf is not null)
        {
            foreach (var inherited in allOf)
            {
                var type = inherited.ResolveType();
                if (!string.IsNullOrEmpty(type) && schemas.TryGetValue(type, out var parentSchema))
                {
                    foreach (var property in parentSchema.GetRequiredProperties())
                    {
                        baseProperties.Add(property);
                    }
                }
            }
        }

        if (baseProperties.Count == 0 && requiredProperties.Count == 0)
        {
            return null;
        }

        var parameters = baseProperties.Union(requiredProperties).Select(x => x.value.ResolveType() + " " + x.key);
        var assignements = requiredProperties.Select(x => x.key[0..1].ToUpperInvariant() + x.key[1..] + " = " + x.key + ";");

        var requiredCtorAttributes = "";
        var shouldOmitDefaultCtor = string.IsNullOrWhiteSpace(jsonConstructorAttribute);

        jsonConstructorAttribute = "[" + jsonConstructorAttribute + "] ";

        if (supportRequiredProperties)
        {
            requiredCtorAttributes = "[System.Diagnostics.CodeAnalysis.SetsRequiredMembers] " + jsonConstructorAttribute;
            jsonConstructorAttribute = "";
            shouldOmitDefaultCtor = true;
        }

        return $@"
    {(shouldOmitDefaultCtor ? "" : jsonConstructorAttribute)}public {name}() {{ }}

    {requiredCtorAttributes}public {name}({string.Join(", ", parameters)}){(baseProperties.Count == 0 ? "" : " : base(" + string.Join(", ", baseProperties.Select(x => x.key)) + ")")}
    {{
{string.Join(Environment.NewLine, assignements.Select(x => "        " + x))}
    }}
";
    }

    private HashSet<(string key, SwaggerSchemaProperty value)> GetRequiredProperties()
    {
        return properties?.Where(x => x.Value.required.GetValueOrDefault() || !x.Value.nullable).Select(x => (x.Key, x.Value)).ToHashSet() ?? new(0);
    }

    private string GetInheritance()
    {
        HashSet<string> toImplement = new();

        if (allOf is not null)
        {
            foreach (var item in allOf.Select(x => x.ResolveType()))
            {
                if (item is not null)
                {
                    toImplement.Add(item);
                }
            }
        }

        if (properties is not null)
        {
            toImplement.Add("__ICanIterate");
        }

        if (toImplement.Count > 0)
        {
            return " : " + string.Join(", ", toImplement);
        }

        return "";
    }

    public string? GetBody(bool supportRequiredProperties, IReadOnlyDictionary<string, SwaggerSchema> schemas)
    {
        return @enum?.GetBody(FlaggedEnum, EnumNames) ?? properties?.GetBody(allOf, supportRequiredProperties, schemas);
    }

    public Task Generate(string path, string @namespace, string modifier, string name, string? jsonConstructorAttribute, bool supportRequiredProperties, IReadOnlyDictionary<string, SwaggerSchema> schemas, CancellationToken token)
    {
        name = name.AsSafeString();
        var fileName = Path.Combine(path, name + ".cs");

        var template = Constants.Header + $@"namespace {@namespace}.Models;

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
{(@enum is null
    ? "[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]" + Environment.NewLine
    : FlaggedEnum is not null
        ? "[System.Flags]" + Environment.NewLine
        : "")}{modifier} {GetDefinitionType(name, schemas.Values)} {name}{GetInheritance()}
{{{GetCtor(name, jsonConstructorAttribute, supportRequiredProperties, schemas)}
{GetBody(supportRequiredProperties, schemas)}
}}
";
        return File.WriteAllTextAsync(fileName, template, token);
    }

    public IEnumerable<string> GetComponents(IReadOnlyDictionary<string, SwaggerSchema> schemas, int depth)
    {
        if (++depth < 1000)
        {
            if (properties is not null)
            {
                foreach (var property in properties)
                {
                    foreach (var component in property.Value.GetComponents(schemas, depth))
                    {
                        yield return component;
                    }
                }
            }

            if (allOf is not null)
            {
                foreach (var item in allOf)
                {
                    foreach (var component in item.GetComponents(schemas, depth))
                    {
                        yield return component;
                    }
                }
            }
        }
    }
}
