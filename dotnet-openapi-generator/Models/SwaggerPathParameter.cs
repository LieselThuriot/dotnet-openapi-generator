namespace dotnet.openapi.generator;

internal class SwaggerPathParameter
{
    public string name { get; set; } = default!;
    public bool required { get; set; }
    public SwaggerSchemaProperty schema { get; set; } = default!;

    public string? @in { get; set; }

    public string GetBody()
    {
        var type = schema.ResolveType();
        var myName = name.AsSafeString();

        var @default = schema.@default;
        if (@default is not null)
        {
            if (type == "string")
            {
                myName += $" = \"{@default}\"";
            }
            else
            {
                myName += " = " + @default.ToString()!.ToLowerInvariant();
            }
        }
        else if (@in == "query" || schema.nullable)
        {
            myName += " = default";
            if (!type!.EndsWith('?'))
            {
                type += '?';
            }
        }

        return type + " " + myName;
    }

    public string? GetComponentType()
    {
        return schema.ResolveType()?.TrimEnd('?');
    }
}
