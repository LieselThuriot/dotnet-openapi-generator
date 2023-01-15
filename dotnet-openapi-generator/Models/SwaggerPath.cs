using System.Text;

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

    public bool HasMembers(bool excludeDeprecated)
    {
        return IterateMembers().Any(x => !excludeDeprecated || !x.deprecated);
    }

    public string GetBody(string apiPath, HashSet<string> methodNames, bool excludeObsolete)
    {
        return GetBodyInternal(apiPath, methodNames, excludeObsolete, false);
    }

    public string GetBodySignature(string apiPath, HashSet<string> methodNames, bool excludeObsolete)
    {
        return GetBodyInternal(apiPath, methodNames, excludeObsolete, true);
    }

    private string GetBodyInternal(string apiPath, HashSet<string> methodNames, bool excludeObsolete, bool signaturesOnly)
    {
        StringBuilder builder = new();

        foreach (var item in IterateMembers())
        {
            var body = item.GetBody(apiPath, methodNames, excludeObsolete, signaturesOnly);
            builder.Append(body);
        }

        return builder.ToString();
    }

    public IEnumerable<string> GetTags()
    {
        return IterateMembers().SelectMany(x => x.tags.Take(1));
    }

    public IEnumerable<string> GetComponents()
    {
        return IterateMembers().SelectMany(x => x.GetComponents());
    }
}
