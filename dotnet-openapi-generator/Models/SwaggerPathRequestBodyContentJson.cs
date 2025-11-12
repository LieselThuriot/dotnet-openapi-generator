namespace dotnet.openapi.generator;

internal sealed class SwaggerPathRequestBodyContentJson
{
    public SwaggerSchemaProperty schema { get; set; } = default!;

    public string GetBody() => (schema.ResolveType() ?? "object") + " body, ";

    public string ResolveType() => schema.ResolveType() ?? "object";
}