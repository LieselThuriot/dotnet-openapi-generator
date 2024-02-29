using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;

namespace dotnet.openapi.generator;

internal class SwaggerComponentSchemas : Dictionary<string, SwaggerSchema>
{
    [JsonConstructor] public SwaggerComponentSchemas() { }

    public SwaggerComponentSchemas(IDictionary<string, SwaggerSchema> values) : base(values) { }

    public bool TryGenerateFastEnumToString(string type, string property, [NotNullWhen(true)] out string? result)
    {
        if (type is null || !TryGetValue(type, out var schema))
        {
            result = null;
            return false;
        }

        if (schema.@enum is null)
        {
            result = null;
            return false;
        }

        StringBuilder builder = new(property);

        builder.AppendLine(" switch")
               .AppendLine("\t\t{");

        foreach (var (name, safeName) in schema.@enum.IterateValues(schema.flaggedEnum, schema.enumNames))
        {
            builder.Append("\t\t\t")
                   .Append(type)
                   .Append('.')
                   .Append(safeName)
                   .Append(" => \"")
                   .Append(name)
                   .AppendLine("\",");
        }

        builder.AppendLine("\t\t\t_ => null");
        builder.Append("\t\t}");

        result = builder.ToString();
        return true;
    }
}
