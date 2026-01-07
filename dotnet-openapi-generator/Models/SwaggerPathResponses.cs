namespace dotnet.openapi.generator;

internal sealed class SwaggerPathResponses : Dictionary<string, SwaggerPathRequestBody>
{
    public string ResolveType()
    {
        // Since we don't support responses based on status codes yet, we will just return the 200 response type if it exists.
        if (TryGetValue("200", out SwaggerPathRequestBody? response))
        {
            return response.ResolveType();
        }

        // Or 201
        if (TryGetValue("201", out response))
        {
            return response.ResolveType();
        }

        // Otherwise, order by status code and return the first successful response type we find, or just the first if none exist.
        return this.OrderBy(x => x.Key.FirstOrDefault() is '2' ? 0 : 1)
                   .ThenBy(x => x.Key)
                   .Select(x => x.Value)
                   .FirstOrDefault()
                  ?.ResolveType() ?? "";
    }
}