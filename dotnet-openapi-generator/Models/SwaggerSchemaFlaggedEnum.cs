namespace dotnet.openapi.generator;

internal sealed class SwaggerSchemaFlaggedEnum
{
    public bool combineAsString { get; set; }
    public string separatingStrings { get; set; } = default!;
}