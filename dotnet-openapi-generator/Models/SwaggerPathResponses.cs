namespace dotnet.openapi.generator;

internal class SwaggerPathResponses
{
    [System.Text.Json.Serialization.JsonPropertyName("200")]
    public SwaggerPathRequestBody? _200 { get; set; }

    public string ResolveType()
    {
        if (_200 is not null)
        {
            return _200.ResolveType();
        }

        return "";
    }
}
