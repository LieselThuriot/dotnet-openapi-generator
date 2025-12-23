using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace dotnet.openapi.generator;

internal sealed class SwaggerComponents
{
    private bool _skipShake;

    public SwaggerComponentSchemas schemas { get; set; } = default!;

    public void BuildSchemas(SwaggerPaths paths)
    {
        _skipShake = true;
        schemas = [];

        foreach ((string pathKey, SwaggerPath pathValue) in paths)
        {
            string apiPath = pathKey.Trim('/').AsSafeString();

            var members = pathValue.IterateMembers().Where(x => x.responses is not null).ToList();
            foreach (SwaggerPathBase operation in members)
            {
                string name = operation.operationId?.AsSafeString() ?? apiPath;
                if (members.Count > 1)
                {
                    name = operation.GetType().Name["SwaggerPath".Length..] + name;
                }

                var responses = operation.responses!.Where(x => x.Value.content is not null).ToList();

                foreach ((string typeKey, SwaggerPathRequestBody typeValue) in responses)
                {
                    string schemaName = name;

                    if (responses.Count > 1)
                    {
                        schemaName += typeKey;
                    }

                    schemaName = schemaName.AsClassNameFromSafeString();

                    foreach (SwaggerSchemaProperty schema in typeValue.content!.ResolveSchemas())
                    {
                        Traverse(schemaName, schema);

                        schema.format = "#/components/schemas/" + schemaName;
                        schemas[schemaName] = new()
                        {
                            properties = schema.properties
                        };
                    }
                }
            }
        }
    }

    private void Traverse(string schemaName, SwaggerSchemaProperty schema)
    {
        if (schema.properties is null)
        {
            return;
        }

        foreach ((string propKey, SwaggerSchemaProperty propValue) in schema.properties)
        {
            string innerSchema = schemaName + propKey.AsClassNameFromSafeString();

            if (propValue.type is "array" && propValue.items.HasValue)
            {
                string genericType = innerSchema + "Item";
                var swaggerSchema = propValue.items.GetValueOrDefault().Deserialize<SwaggerSchema>();

                if (swaggerSchema is not null)
                {
                    schemas[genericType] = swaggerSchema;

                    var node = JsonNode.Parse(propValue.items.GetValueOrDefault().ToString())!;
                    node["type"] = "#/components/schemas/" + genericType;
                    propValue.items = JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
                }
            }
            else if (propValue.type is "object" && propValue.properties is not null)
            {
                propValue.format = "#/components/schemas/" + innerSchema;
                schemas[innerSchema] = new()
                {
                    properties = propValue.properties
                };
            }

            Traverse(innerSchema, propValue);
        }
    }

    public async Task Generate(ProgressContext ctx, string path, string @namespace, string modifier, string clientModifierValue, IEnumerable<string> usedComponents, bool treeShaking, string? jsonConstructorAttribute, string? jsonPolymorphicAttribute, string? jsonDerivedTypeAttribute, string? jsonPropertyNameAttribute, bool includeJsonSourceGenerators, bool supportRequiredProperties, bool verbose, CancellationToken token)
    {
        path = Path.Combine(path, "Models");

        if (!Directory.Exists(path))
        {
            if (verbose)
            {
                AnsiConsole.WriteLine("Making sure models directory exists");
            }

            Directory.CreateDirectory(path);
        }

        await GenerateInternals(path, @namespace, token);

        var schemasToGenerate = schemas;

        if (!_skipShake && treeShaking)
        {
            schemasToGenerate = new(schemasToGenerate.ToDictionary(x => x.Key.AsSafeString(), x => x.Value));
            ShakeTree(usedComponents, schemasToGenerate, verbose);
        }

        var modelsTask = ctx.AddTask("Models", maxValue: schemasToGenerate.Count);

        foreach (var schema in schemasToGenerate)
        {
            await schema.Value.Generate(path, @namespace, modifier, schema.Key, jsonConstructorAttribute, jsonPolymorphicAttribute, jsonDerivedTypeAttribute, jsonPropertyNameAttribute, supportRequiredProperties, schemasToGenerate, token);
            modelsTask.Increment(1);
        }

        if (includeJsonSourceGenerators)
        {
            var jsonGeneratorTask = ctx.AddTask("Json Source Generators", maxValue: 1);

            var attributes = schemasToGenerate.Keys.Select(x => x.AsSafeString())
                                              .Order()
                                              .Select(x => $"[System.Text.Json.Serialization.JsonSerializable(typeof({x}))]")
                                              .ToHashSet();

            if (attributes.Count > 0)
            {
                string className = @namespace.AsSafeString(replaceDots: true, replacement: "");

                string template = Constants.Header + $@"using {@namespace}.Models;

namespace {@namespace}.Clients;

[System.Text.Json.Serialization.JsonSourceGenerationOptions("
#if NET8_0_OR_GREATER
+ @"System.Text.Json.JsonSerializerDefaults.Web, UseStringEnumConverter = true"
#else
+ @"PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase"
#endif
+ $@", DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
{string.Join(Environment.NewLine, attributes)}
{clientModifierValue} sealed partial class {className}JsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
{{"
#if !NET8_0_OR_GREATER
        + $@"
    static {className}JsonSerializerContext()
    {{
        s_defaultOptions.PropertyNameCaseInsensitive = true;
        s_defaultOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
        s_defaultOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }}"
#endif
        + @"
}";

                await File.WriteAllTextAsync(Path.Combine(path, "../Clients/__JsonSerializerContext.cs"), template, token);
            }

            jsonGeneratorTask.Increment(1);
        }
    }

    private static Task GenerateInternals(string path, string @namespace, CancellationToken token)
    {
        return File.WriteAllTextAsync(Path.Combine(path, "__ICanIterate.cs"), Constants.Header + $$"""
namespace {{@namespace}}.Models;

[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
internal interface __ICanIterate
{
    System.Collections.Generic.IEnumerable<(string name, object? value)> IterateProperties();
}
""", token);
    }

    private static void ShakeTree(IEnumerable<string> usedComponents, Dictionary<string, SwaggerSchema> schemas, bool verbose)
    {
        if (verbose)
        {
            AnsiConsole.WriteLine($"Shaking the trees: Currently contains {schemas.Count} models");
        }

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
            bool hasIt = schemas.TryGetValue(key, out var schema);
            return (hasIt, key, schema);
        })
        .Where(x => x.hasIt)
        .SelectMany(x => x.schema!.GetComponents(schemas, depth: 0).Append(x.key));

        foreach (string? key in schemas.Keys.Except(relevantSchemas).ToList())
        {
            schemas.Remove(key);
        }

        if (verbose)
        {
            AnsiConsole.WriteLine($"Done shaking the trees: Currently contains {schemas.Count} models");
        }
    }
}