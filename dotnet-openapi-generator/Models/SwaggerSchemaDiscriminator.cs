namespace dotnet.openapi.generator;

internal class SwaggerSchemaDiscriminator
{
    public string propertyName { get; set; } = default!;
    public Dictionary<string, string> mapping { get; set; } = default!;
}