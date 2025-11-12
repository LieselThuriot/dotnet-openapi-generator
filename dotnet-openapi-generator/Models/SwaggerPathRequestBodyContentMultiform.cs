namespace dotnet.openapi.generator;

internal sealed class SwaggerPathRequestBodyContentMultiform
{
    public SwaggerPathRequestBodyContentMultiformSchema schema { get; set; } = default!;

    public string GetBody()
    {
        string result = "";

        foreach (var (Key, Value) in schema.IterateProperties())
        {
            string type = Value.ResolveType()!;
            result += $"{type} @{(type[0..1].ToLowerInvariant() + type[1..]).AsSafeString()}, ";
        }

        return result;
    }

    public static string ResolveType()
    {
        return typeof(Stream).FullName!;
    }
}