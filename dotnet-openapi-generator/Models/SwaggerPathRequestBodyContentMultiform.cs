namespace dotnet.openapi.generator;

internal class SwaggerPathRequestBodyContentMultiform
{
    public SwaggerPathRequestBodyContentMultiformSchema schema { get; set; } = default!;

    public string GetBody()
    {
        var result = "";

        foreach (var item in schema.IterateProperties())
        {
            var type = item.Value.ResolveType()!;
            result += $"{type} @{(type[0..1].ToLowerInvariant() + type[1..]).AsSafeString()}, ";
        }

        return result;
    }

    public string ResolveType()
    {
        return typeof(Stream).FullName!;
    }
}
