namespace dotnet.openapi.generator;

internal static class Extensions
{
    public static string AsSafeVariableName(this string value)
    {
        string result = char.IsLower(value[0])
            ? value
            : value[0..1].ToLowerInvariant() + value[1..];

        if (s_keywords.Contains(result))
        {
            result = '@' + result;
        }

        return result;
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
        HashSet<string> returnedValues = new();

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
        HashSet<string> returnedValues = new();

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

    private static readonly IEnumerable<string> s_keywords = new HashSet<string>()
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
