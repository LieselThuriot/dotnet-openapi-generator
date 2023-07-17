namespace dotnet.openapi.generator;

internal class SwaggerComponents
{
    public Dictionary<string, SwaggerSchema> schemas { get; set; } = default!;

    public async Task Generate(string path, string @namespace, string modifier, string clientModifierValue, IEnumerable<string> usedComponents, bool treeShaking, string? jsonConstructorAttribute, bool includeJsonSourceGenerators, bool supportRequiredProperties, CancellationToken token)
    {
        path = Path.Combine(path, "Models");

        if (!Directory.Exists(path))
        {
            Logger.LogVerbose("Making sure models directory exists");
            Directory.CreateDirectory(path);
        }

        await GenerateInternals(path, @namespace, token);

        var schemasToGenerate = this.schemas;

        if (treeShaking)
        {
            schemasToGenerate = schemasToGenerate.ToDictionary(x => x.Key.AsSafeString(), x => x.Value);
            ShakeTree(usedComponents, schemasToGenerate);
        }

        Logger.LogInformational("Generating Models");

        int i = 0;
        foreach (var schema in schemasToGenerate)
        {
            Logger.LogStatus(++i, schemasToGenerate.Count, schema.Key);
            await schema.Value.Generate(path, @namespace, modifier, schema.Key, jsonConstructorAttribute, supportRequiredProperties, schemasToGenerate, token);
        }

        Logger.BlankLine();

        if (includeJsonSourceGenerators)
        {
            Logger.LogInformational("Generating Json Source Generators");

            var attributes = schemasToGenerate.Keys.Select(x => x.AsSafeString())
                                              .OrderBy(x => x)
                                              .Select(x => $"[System.Text.Json.Serialization.JsonSerializable(typeof({x}))]")
                                              .ToHashSet();

            if (attributes.Count > 0)
            {
                var className = @namespace.AsSafeString(replaceDots: true, replacement: "");

                var template = Constants.Header + $@"using {@namespace}.Models;

namespace {@namespace}.Clients;

[System.Text.Json.Serialization.JsonSourceGenerationOptions(DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull, PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
{string.Join(Environment.NewLine, attributes)}
{clientModifierValue} sealed partial class {className}JsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
{{
    static {className}JsonSerializerContext()
    {{
        s_defaultOptions.PropertyNameCaseInsensitive = true;
        s_defaultOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
        s_defaultOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }}
}}";

                await File.WriteAllTextAsync(Path.Combine(path, "../Clients/__JsonSerializerContext.cs"), template, token);
            }
        }
    }

    private static async Task GenerateInternals(string path, string @namespace, CancellationToken token)
    {
        await File.WriteAllTextAsync(Path.Combine(path, "__ICanIterate.cs"), Constants.Header + $@"namespace {@namespace}.Models;

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
internal interface __ICanIterate
{{
    System.Collections.Generic.IEnumerable<(string name, object? value)> IterateProperties();
}}", token);
    }

    private static void ShakeTree(IEnumerable<string> usedComponents, Dictionary<string, SwaggerSchema> schemas)
    {
        Logger.LogVerbose($"Shaking the trees: Currently contains {schemas.Count} models");

        var relevantSchemas = usedComponents.Select(x =>
        {
            var match = Regexes.FindActualComponent().Match(x);

            if (match.Success)
            {
                return match.Groups["actualComponent"].Value;
            }

            return x;
        }).Select(key =>
        {
            var hasIt = schemas.TryGetValue(key, out var schema);
            return (hasIt, key, schema);
        })
        .Where(x => x.hasIt)
        .SelectMany(x => x.schema!.GetComponents(schemas, depth: 0).Append(x.key));

        foreach (var key in schemas.Keys.Except(relevantSchemas).ToList())
        {
            schemas.Remove(key);
        }

        Logger.LogVerbose($"Done shaking the trees: Currently contains {schemas.Count} models");
    }
}
