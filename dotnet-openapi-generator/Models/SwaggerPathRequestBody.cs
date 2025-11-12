namespace dotnet.openapi.generator;

internal sealed class SwaggerPathRequestBody
{
    public SwaggerPathRequestBodyContent? content { get; set; }

    public string GetBody()
    {
        if (content is null)
        {
            return "";
        }

        return content.GetBody();
    }

    public string ResolveType()
    {
        if (content is not null)
        {
            return content.ResolveType();
        }

        return "";
    }
}