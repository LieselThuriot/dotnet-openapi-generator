namespace dotnet.openapi.generator;

internal class SwaggerPathRequestBodyContent
{
    [System.Text.Json.Serialization.JsonPropertyName("application/json")]
    public SwaggerPathRequestBodyContentJson? applicationjson { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("multipart/form-data")]
    public SwaggerPathRequestBodyContentMultiform? multipartformdata { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("application/octet-stream")]
    public SwaggerPathRequestBodyContentOctetStream? octetstream { get; set; }

    public string GetBody()
    {
        if (multipartformdata is not null)
        {
            var result = "";

            foreach (var item in multipartformdata.schema.properties)
            {
                var type = item.Value.ResolveType()!;
                result += $"{type} @{(type[0..1].ToLowerInvariant() + type[1..]).AsSafeString()}, ";
            }

            return result;
        }

        if (octetstream is not null)
        {
            return (octetstream.schema.ResolveType() ?? "object") + " body, ";
        }

        if (applicationjson is not null)
        {
            return (applicationjson.schema.ResolveType() ?? "object") + " body, ";
        }

        return "";
    }

    public string ResolveType()
    {
        if (multipartformdata is not null)
        {
            return typeof(Stream).FullName!;
        }

        if (octetstream is not null)
        {
            return octetstream.schema.ResolveType() ?? "object";
        }

        if (applicationjson is not null)
        {
            return applicationjson.schema.ResolveType() ?? "object";
        }

        return "";
    }
}
