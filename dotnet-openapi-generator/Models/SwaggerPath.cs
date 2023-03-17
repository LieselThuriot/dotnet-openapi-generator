namespace dotnet.openapi.generator;

internal class SwaggerPath
{
    public SwaggerPathGet? get { get; set; }
    public SwaggerPathPost? post { get; set; }
    public SwaggerPathPut? put { get; set; }
    public SwaggerPathDelete? delete { get; set; }

    public IEnumerable<SwaggerPathBase> IterateMembers()
    {
        if (get is not null) yield return get;
        if (post is not null) yield return post;
        if (put is not null) yield return put;
        if (delete is not null) yield return delete;
    }
}
