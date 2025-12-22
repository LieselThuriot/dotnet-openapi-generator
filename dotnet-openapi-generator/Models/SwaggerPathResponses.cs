namespace dotnet.openapi.generator;

internal sealed class SwaggerPathResponses : Dictionary<string, SwaggerPathRequestBody>
{
    public string ResolveType() => Values.FirstOrDefault()?.ResolveType() ?? "";
}