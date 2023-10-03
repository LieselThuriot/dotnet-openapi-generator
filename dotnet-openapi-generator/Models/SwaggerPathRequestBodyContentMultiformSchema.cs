namespace dotnet.openapi.generator;

internal class SwaggerPathRequestBodyContentMultiformSchema
{
    public SwaggerSchemaProperties properties { get; set; } = default!;

    public IEnumerable<(string Key, SwaggerSchemaProperty Value)> IterateProperties()
    {
        foreach (var (key, value) in properties)
        {
            yield return (key, value);
        }
    }
}
