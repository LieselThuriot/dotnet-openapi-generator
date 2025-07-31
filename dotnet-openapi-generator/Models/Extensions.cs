using System.Text.Json;

namespace dotnet.openapi.generator;

internal static class Extensions
{
    public static string AsSafeVariableName(this string value, string keywordPrefix = "@", string numberPrefix = "_")
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string result = char.IsLower(value[0])
            ? value
            : value[0..1].ToLowerInvariant() + value[1..];

        return result.AsSafeCSharpName(keywordPrefix, numberPrefix);
    }

    public static string AsSafeClientName(this string value, string prefix = "_")
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string result = char.IsUpper(value[0])
            ? value
            : value[0..1].ToUpperInvariant() + value[1..];

        if (result.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
        {
            result = result[..^"Client".Length].TrimEnd();
        }

        return result.AsSafeCSharpName(prefix)
                     .AsSafeString(replaceDots: true, replacement: "");
    }

    public static string AsSafeCSharpName(this string value, string prefix) => value.AsSafeCSharpName(prefix, prefix);
    public static string AsSafeCSharpName(this string value, string keywordPrefix, string numberPrefix)
    {
        if (s_keywords.Contains(value))
        {
            return keywordPrefix + value;
        }
        else if (char.IsNumber(value[0]))
        {
            return numberPrefix + value;
        }

        return value;
    }

    public static string AsSafeString(this string? value, bool replaceDots = true, string replacement = "_")
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        value = Regexes.FullnameType().Replace(value, x => x.Groups["genericType"].Value + replacement + x.Groups["type"].Value);

        if (replaceDots)
        {
            value = Regexes.SafeStringWithoutDots().Replace(value, replacement);
        }
        else
        {
            value = Regexes.SafeString().Replace(value, replacement);
        }

        return Regexes.MultiUnderscore().Replace(value, replacement).Trim(replacement.ToCharArray());
    }

    public static IEnumerable<string> AsUniques(this IEnumerable<string> values)
    {
        HashSet<string> returnedValues = [];

        foreach (var value in values)
        {
            var result = value;

            int count = 0;
            while (!returnedValues.Add(result))
            {
                result = $"{value}_{count++}";
            }

            yield return result;
        }
    }

    public static IEnumerable<(T item, string name)> AsUniques<T>(this IEnumerable<T> values, Func<T, string> get)
    {
        HashSet<string> returnedValues = [];

        foreach (var value in values)
        {
            string name = get(value!);
            var result = name;

            int count = 0;
            while (!returnedValues.Add(result))
            {
                result = $"{name}_{count++}";
            }

            yield return (value, result);
        }
    }

    public static string AsMethodName(this string value)
    {
        value = value.AsSafeString();

        if (value.StartsWith("api_"))
        {
            value = value[4..];
        }

        var split = value.Split('_', StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x[0..1].ToUpperInvariant() + x[1..]);

        return string.Concat(split);
    }

    public static string? ResolveType(this string? typeToResolve, System.Text.Json.JsonElement? items = null, SwaggerSchemaPropertyAdditionalProperties? additionalProperties = null) => typeToResolve.ResolveType(true, items, additionalProperties);
    public static string? ResolveType(this string? typeToResolve, bool fallBack, System.Text.Json.JsonElement? items = null, SwaggerSchemaPropertyAdditionalProperties? additionalProperties = null)
    {
        return typeToResolve switch
        {
            "date" or "date-time" => typeof(DateTime).FullName!,
            "date-span" => typeof(TimeSpan).FullName!,
            "boolean" => "bool",
            "int32" or "integer" => "int",
            "int64" => "long",
            "float" => "float",
            "double" or "number" => "double",
            "string" or "json" => "string",
            "uri" => typeof(Uri).FullName!,
            "uuid" => typeof(Guid).FullName!,
            "binary" => typeof(Stream).FullName!,
            "array" => typeof(List<>).FullName![..^2] + "<" + items.ResolveArrayType(additionalProperties) + ">",
            "object" when additionalProperties is not null => $"{typeof(Dictionary<,>).FullName![..^2]}<string, {ResolveType(additionalProperties.type, items, null)}>",
            null => "object",
            _ when fallBack => typeToResolve.Replace("#/components/schemas/", "").AsSafeString(),
            _ => null
        };
    }

    public static string ResolveArrayType(this JsonElement? items, SwaggerSchemaPropertyAdditionalProperties? additionalProperties)
    {
        if (items is not null)
        {
            if (items.Value.TryGetProperty("type", out var arrayType))
            {
                return ResolveType(arrayType.GetString(), items.Value.TryGetProperty("items", out var innerItems) ? innerItems : null, additionalProperties)!;
            }
            else if (items.Value.TryGetProperty("$ref", out var refProperty))
            {
                return ResolveType(refProperty.GetString(), null, additionalProperties)!;
            }
        }

        return "object";
    }

    private static readonly IEnumerable<string> s_keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };
}
