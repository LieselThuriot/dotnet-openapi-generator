namespace dotnet.openapi.generator;

internal static class Extensions
{
    public static string AsSafeString(this string? value, bool replaceDots = true)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        value = Regexes.FullnameType().Replace(value, x => x.Groups["genericType"].Value + "_" + x.Groups["type"].Value);

        if (replaceDots)
        {
            value = Regexes.SafeStringWithoutDots().Replace(value, "_");
        }
        else
        {
            value = Regexes.SafeString().Replace(value, "_");
        }

        return Regexes.MultiUnderscore().Replace(value, "_").Trim('_');
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
}
