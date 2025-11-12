namespace dotnet.openapi.generator;

internal sealed class SwaggerPathResponses
{
    [System.Text.Json.Serialization.JsonPropertyName("200")]
    public SwaggerPathRequestBody? _200 { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("201")]
    public SwaggerPathRequestBody? _201 { get; set; }

    public string ResolveType()
    {
        if (_200 is not null)
        {
            return _200.ResolveType();
        }

        if (_201 is not null)
        {
            return _201.ResolveType();
        }

        return "";
    }
}