namespace dotnet.openapi.generator;

internal sealed class SwaggerSchemaDiscriminator
{
    public string propertyName { get; set; } = default!;
    public Dictionary<string, string> mapping { get; set; } = default!;
}