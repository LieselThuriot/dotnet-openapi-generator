namespace dotnet.openapi.generator;

internal sealed class SwaggerPathRequestBodyContent
{
    [System.Text.Json.Serialization.JsonPropertyName("application/json")]
    public SwaggerPathRequestBodyContentJson? applicationjson { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("application/vnd.api+json")]
    public SwaggerPathRequestBodyContentJson? applicationjsonapi { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("multipart/form-data")]
    public SwaggerPathRequestBodyContentMultiform? multipartformdata { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("application/octet-stream")]
    public SwaggerPathRequestBodyContentOctetStream? octetstream { get; set; }

    public string GetBody()
    {
        if (multipartformdata is not null)
        {
            return multipartformdata.GetBody();
        }

        if (octetstream is not null)
        {
            return octetstream.GetBody();
        }

        if (applicationjson is not null)
        {
            return applicationjson.GetBody();
        }

        if (applicationjsonapi is not null)
        {
            return applicationjsonapi.GetBody();
        }

        return "";
    }

    public string ResolveType()
    {
        if (multipartformdata is not null)
        {
            return SwaggerPathRequestBodyContentMultiform.ResolveType();
        }

        if (octetstream is not null)
        {
            return octetstream.ResolveType();
        }

        if (applicationjson is not null)
        {
            return applicationjson.ResolveType();
        }

        if (applicationjsonapi is not null)
        {
            return applicationjsonapi.ResolveType();
        }

        return "";
    }

    public IEnumerable<SwaggerSchemaProperty> ResolveSchemas()
    {
        if (applicationjson is not null)
        {
            yield return applicationjson.schema;
        }
        else if (applicationjsonapi is not null)
        {
            yield return applicationjsonapi.schema;
        }
        else if (octetstream is not null)
        {
            yield return octetstream.schema;
        }
        else if (multipartformdata is not null)
        {
            foreach (var prop in multipartformdata.schema.properties.Values)
            {
                yield return prop;
            }
        }
    }
}