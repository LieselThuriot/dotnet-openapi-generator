namespace dotnet.openapi.generator;

internal sealed class SwaggerPathGet : SwaggerPathBase;
internal sealed class SwaggerPathPost : SwaggerPathBase;
internal sealed class SwaggerPathPut : SwaggerPathBase;
internal sealed class SwaggerPathDelete : SwaggerPathBase;

internal abstract class SwaggerPathBase
{
    public IEnumerable<string> tags { get; set; } = default!;
    public string? summary { get; set; }
    public IEnumerable<SwaggerPathParameter>? parameters { get; set; }
    public SwaggerPathRequestBody? requestBody { get; set; }
    public SwaggerPathResponses? responses { get; set; }
    public bool deprecated { get; set; }

    public string? operationId { get; set; }

    public string GetBodySignature(string apiPath, HashSet<string> methodNames, bool excludeObsolete, SwaggerComponentSchemas componentSchemas, bool includeOptions) => GetBodyInternal(apiPath, methodNames, excludeObsolete, true, componentSchemas, includeOptions);
    public string GetBody(string apiPath, HashSet<string> methodNames, bool excludeObsolete, SwaggerComponentSchemas componentSchemas, bool includeOptions) => GetBodyInternal(apiPath, methodNames, excludeObsolete, false, componentSchemas, includeOptions);

    private string GetBodyInternal(string apiPath, HashSet<string> methodNames, bool excludeObsolete, bool signaturesOnly, SwaggerComponentSchemas componentSchemas, bool includeOptionsDictionary)
    {
        if (excludeObsolete && deprecated)
        {
            return "";
        }

        apiPath = apiPath.TrimStart('/');

        string operation = GetType().Name["SwaggerPath".Length..];
        string name = (operationId ?? apiPath).AsMethodName();

        if (!name.StartsWith(operation, StringComparison.OrdinalIgnoreCase))
        {
            name = operation + name;
        }

        if (name.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
        {
            name = name[0..^5];
        }

        string ogName = name;
        int counter = 1;
        while (!methodNames.Add(name))
        {
            name = ogName + "__" + counter++ + "__";

            if (counter > 1000)
            {
                throw new OverflowException("Too many similar method names");
            }
        }

        var methodParameters = (parameters ?? [])
                                           .OrderBy(x => x.required ? 0 : 1)
                                           .ThenBy(x => x.@in is "path" ? 0 : 1)
                                           .ToList();

        string methodParameterBodies = string.Join(", ", methodParameters.Select(x => x.GetBody()));

        if (methodParameterBodies.Length > 0)
        {
            methodParameterBodies += ", ";
        }

        var queryParams = (parameters ?? []).Where(x => x.@in is "query").ToList();

        string queryContent = "";
        if (queryParams.Count > 0)
        {
            queryContent += "__QueryBuilder __my_queryBuilder = new __QueryBuilder();" + Environment.NewLine + string.Concat(queryParams.Select(x => $"        __my_queryBuilder.AddParameter({GenerateParameterSyntax(x)}, \"{x.name.TrimStart('@')}\");" + Environment.NewLine)) + Environment.NewLine + "        ";
            apiPath += "{__my_queryBuilder.ToString()}";

            string GenerateParameterSyntax(SwaggerPathParameter x)
            {
                string syntax = x.name.AsSafeString();

                if (componentSchemas.TryGenerateFastEnumToString(x.GetComponentType()!, syntax, out string? fastToString))
                {
                    return fastToString;
                }

                return syntax;
            }
        }

        var headerParams = (parameters ?? []).Where(x => x.@in is "header").ToList();

        string headersToAdd = "";
        if (headerParams.Count > 0)
        {
            headersToAdd += Environment.NewLine;
            foreach (var header in headerParams)
            {
                string safeString = header.name.AsSafeString();
                string type = header.schema.ResolveType() ?? "";

                if (type.StartsWith(typeof(List<>).FullName![..^2]))
                {
                    safeString = "System.Linq.Enumerable.Select(" + safeString + ", x => \"\" + x)";
                }
                else if (type is not "string")
                {
                    safeString = "\"\" + " + safeString;
                }

                if (header.schema.@ref is not null && componentSchemas.TryGenerateFastEnumToString(type, safeString, out string? fastToString))
                {
                    safeString = fastToString;
                }

                headersToAdd += $@"
        if ({header.name.AsSafeString()} != default)
        {{
            __my_request.Headers.Add(""{header.name}"", {safeString});
        }}
";
            }
        }

        string content = $@"$""{apiPath}""";
        string clientCall;

        if (requestBody is not null)
        {
            methodParameterBodies = requestBody.GetBody() + methodParameterBodies;
            if (requestBody.content?.multipartformdata is not null)
            {
                content += string.Concat(requestBody.content.multipartformdata.schema.IterateProperties()
                                               .Select(x => x.Value.ResolveType()!)
                                               .Select(x => x.AsSafeString())
                                               .Select(x => "@" + x[0..1].ToLowerInvariant() + x[1..])
                                               .AsUniques()
                                               .Select(x => ", " + x));
            }
            else
            {
                content += ", body";
            }
        }
        //else if (operation is "Post" or "Put")
        //{
        //    content += ", content: null";
        //}

        if (requestBody?.content?.multipartformdata is not null)
        {
            string contents = "";
            List<string> contentNames = [];

            foreach (var x in requestBody.content.multipartformdata.schema.IterateProperties().Select(x => x.Value)
                                            .Select(x => x.ResolveType()!)
                                            .Select(x => x.AsSafeString())
                                            .AsUniques(x => "@" + x[0..1].ToLowerInvariant() + x[1..]))
            {
                string paramName = x.name;
                string paramContentName = $"__{paramName.AsSpan(1)}";
                contentNames.Add(paramContentName);

                if (x.item == typeof(Stream).FullName)
                {
                    contents += $"""

        using System.Net.Http.StreamContent {paramContentName} = new({paramName});
        {paramContentName}.Headers.Add("Content-Disposition", "form-data; name=\"formFile\"");
        {paramContentName}.Headers.Add("Content-Type", System.Net.Mime.MediaTypeNames.Text.Plain);

""";
                }
                else if (paramName.StartsWith("@system_IO_Stream"))
                {
                    contents += $@"
        using System.Net.Http.StreamContent {paramContentName} = new({paramName});
";
                }
                else
                {
                    contents += $@"
        using var {paramContentName} = __my_options.CreateContent({paramName});
";
                }
            }

            clientCall = $@"        {queryContent}using System.Net.Http.HttpRequestMessage __my_request = new(System.Net.Http.HttpMethod.{operation}, $""{apiPath}"");{(includeOptionsDictionary ? @"
        
        if (options is not null)
        {
            foreach (var option in options)
            {
                __my_request.Options.Set(new System.Net.Http.HttpRequestOptionsKey<object>(option.Key), option.Value);
            }
        }" : "")}
        {contents}
        __my_request.Content = new System.Net.Http.MultipartFormDataContent
        {{
            {string.Join("," + Environment.NewLine + "            ", contentNames)}
        }};{headersToAdd}

        using var __my_intercepted_request = await __my_options.InterceptRequest(__my_request, token);
        return await __my_http_client.SendAsync(__my_intercepted_request, token);";
        }
        else
        {
            clientCall = $@"        {queryContent}var __my_request = await __my_options.CreateRequest(System.Net.Http.HttpMethod.{operation}, {content}{(includeOptionsDictionary ? ", options" : "")}, token);{headersToAdd}
        return await __my_http_client.SendAsync(__my_request, token);";
        }

        string passThroughParams = string.Join(", ", methodParameters.Select(x => x.name.AsSafeString()));

        if (passThroughParams.Length > 0)
        {
            if (requestBody is not null)
            {
                if (requestBody.content?.multipartformdata is not null)
                {
                    passThroughParams = string.Concat(requestBody.content.multipartformdata.schema.IterateProperties()
                                                   .Select(x => x.Value.ResolveType()!)
                                                   .Select(x => x.AsSafeString())
                                                   .Select(x => "@" + x[0..1].ToLowerInvariant() + x[1..])
                                                   .AsUniques()
                                                   .Select(x => x + ", "))
                                                   + passThroughParams;
                }
                else
                {
                    passThroughParams = "body, " + passThroughParams;
                }
            }

            passThroughParams += ", ";
        }
        else
        {
            if (requestBody is not null)
            {
                if (requestBody.content?.multipartformdata is not null)
                {
                    passThroughParams = string.Concat(requestBody.content.multipartformdata.schema.IterateProperties()
                                                   .Select(x => x.Value.ResolveType()!)
                                                   .Select(x => x.AsSafeString())
                                                   .Select(x => "@" + x[0..1].ToLowerInvariant() + x[1..])
                                                   .AsUniques()
                                                   .Select(x => x + ", "));
                }
                else
                {
                    passThroughParams = "body, ";
                }
            }
        }

        string obsolete = deprecated
            ? Environment.NewLine + "    [System.Obsolete]"
            : "";

        string? methodSummary = summary?.Replace('\n', ' ').Replace('\r', ' ');
        if (string.IsNullOrWhiteSpace(methodSummary))
        {
            methodSummary = "HTTP " + operation + " on /" + apiPath.Replace("{__my_queryBuilder.ToString()}", "");
        }

        string result = $@"
    /// <summary>
    /// {methodSummary}
    /// </summary>{obsolete}
    public {(signaturesOnly ? "" : "async ")}System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> {name}WithHttpInfoAsync({methodParameterBodies}{(includeOptionsDictionary ? "System.Collections.Generic.IDictionary<string, object>? options = null, " : string.Empty)}System.Threading.CancellationToken token = default)"
    + (signaturesOnly
            ? @";
"
            : $@"
    {{
{clientCall}
    }}
");

        string responseType = "";
        if (responses is not null)
        {
            responseType = $"<{responses.ResolveType().TrimEnd('?')}?>";

            if (responseType.Length > 3)
            {
                result += $@"
    /// <summary>
    /// {methodSummary}
    /// </summary>{obsolete}
    public {(signaturesOnly ? "" : "async ")}System.Threading.Tasks.Task{responseType} {name}Async({methodParameterBodies}{(includeOptionsDictionary ? "System.Collections.Generic.IDictionary<string, object>? options = null, " : string.Empty)}System.Threading.CancellationToken token = default)"
    + (signaturesOnly
                    ? @";
"
: $@"
    {{
        var __result = await {name}WithHttpInfoAsync({passThroughParams}{(includeOptionsDictionary ? "options, " : string.Empty)}token);
        await __my_options.InterceptResponse(__result, token);
        return await {(responseType is "<System.IO.Stream?>" ? "__result.Content.ReadAsStreamAsync(token)" : $"__my_options.DeSerializeContent{responseType}(__result, token)")};
    }}
");
            }
            else
            {
                AppendDefault();
            }
        }
        else
        {
            AppendDefault();
        }

        void AppendDefault()
        {
            result += $@"
    /// <summary>
    /// {methodSummary}
    /// </summary>{obsolete}
    public {(signaturesOnly ? "" : "async ")}System.Threading.Tasks.Task {name}Async({methodParameterBodies}{(includeOptionsDictionary ? "System.Collections.Generic.IDictionary<string, object>? options = null, " : string.Empty)}System.Threading.CancellationToken token = default)"
    + (signaturesOnly
    ? @";
"
    : $@"
    {{
        var __result = await {name}WithHttpInfoAsync({passThroughParams}{(includeOptionsDictionary ? "options, " : string.Empty)}token);
        await __my_options.InterceptResponse(__result, token);
    }}
");
        }

        return result;
    }

    public IEnumerable<string> GetComponents()
    {
        var result = Enumerable.Empty<string>();

        if (responses is not null)
        {
            string type = responses.ResolveType().TrimEnd('?');
            if (!string.IsNullOrWhiteSpace(type))
            {
                result = result.Append(type);
            }
        }

        if (parameters is not null)
        {
            var methodParameters = parameters.Select(x => x.GetComponentType()).Where(x => !string.IsNullOrWhiteSpace(x));
            result = result.Concat(methodParameters)!;
        }

        if (requestBody is not null)
        {
            result = result.Append(requestBody.ResolveType());
        }

        return result;
    }
}