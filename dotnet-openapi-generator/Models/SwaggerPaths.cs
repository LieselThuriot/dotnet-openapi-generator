using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace dotnet.openapi.generator;

internal class SwaggerPaths : Dictionary<string, SwaggerPath>
{
    public async Task<IEnumerable<string>> Generate(string path, string @namespace, string modifier, bool excludeObsolete, Regex? filter, bool includeInterfaces, string clientModifier, int stringBuilderPoolSize, OAuthType oAuthType, bool includeJsonSourceGenerators, CancellationToken token)
    {
        path = Path.Combine(path, "Clients");

        if (!Directory.Exists(path))
        {
            Logger.LogVerbose("Making sure clients directory exists");
            Directory.CreateDirectory(path);
        }

        var clients = GetClients(excludeObsolete, filter);

        if (clients.Count == 0)
        {
            Logger.LogWarning("No clients found to generate");
            return Enumerable.Empty<string>();
        }

        HashSet<string> usedComponents = new();

        await GenerateClientOptions(path, @namespace, modifier, oAuthType is not OAuthType.None, includeJsonSourceGenerators, token);
        await GenerateQueryBuilder(path, @namespace, stringBuilderPoolSize, token);
        await GenerateClients(path, @namespace, modifier, excludeObsolete, includeInterfaces, clientModifier, clients, usedComponents, token);
        await GenerateRegistrations(path, @namespace, modifier, includeInterfaces, clients.Keys, oAuthType, token);

        return usedComponents;
    }

    private Dictionary<string, List<(string apiPath, SwaggerPathBase path)>> GetClients(bool excludeObsolete, Regex? filter)
    {
        Dictionary<string, List<(string, SwaggerPathBase)>> clients = new(Count);

        foreach (var item in this)
        {
            foreach (var member in item.Value.IterateMembers().Where(x => x.tags is not null && (!excludeObsolete || !x.deprecated)))
            {
                foreach (var tag in member.tags)
                {
                    if (filter?.IsMatch(tag) == false)
                    {
                        continue;
                    }

                    var safeTag = tag.AsSafeClientName();

                    if (clients.TryGetValue(safeTag, out var list))
                    {
                        list.Add((item.Key, member));
                    }
                    else
                    {
                        clients[safeTag] = new()
                        {
                            (item.Key, member)
                        };
                    }
                }
            }
        }

        return clients;
    }

    private static async Task GenerateRegistrations(string path, string @namespace, string modifier, bool includeInterfaces, IEnumerable<string> clients, OAuthType oAuthType, CancellationToken token)
    {
        Logger.LogInformational("Generating Registrations");

        var clientNames = clients.OrderBy(x => x)
                                 .Select(x => $"        public const string {x} = \"{@namespace.AsSafeString(replaceDots: true, replacement: "")}{x}Client\";")
                                 .Aggregate((current, next) => current + Environment.NewLine + next);

        var addHttpClientRegistrations = clients.OrderBy(x => x)
                                                .Select(x => $"        Register<{GetClientGeneric(x)}>(__ClientNames.{x});")
                                                .Aggregate((current, next) => current + Environment.NewLine + next);

        string GetClientGeneric(string name)
        {
            var result = name + "Client";

            if (includeInterfaces)
            {
                result = $"I{result}, {result}";
            }

            return result;
        }

        bool singletonClientOptions = true;
        string registrationClassAdditionals = "";
        string additionalRegistrations = "";

        if (oAuthType is not OAuthType.None)
        {
            clientNames += $@"

        public const string TokenRequestClient = ""{@namespace.AsSafeString(replaceDots: true, replacement: "")}TokenRequestClient"";
        public const string DiscoveryCache = ""{@namespace.AsSafeString(replaceDots: true, replacement: "")}DiscoveryCache"";";

            additionalRegistrations = $@"

        TokenOptions tokenOptions = registration.TokenOptions;
        services.TryAddSingleton(tokenOptions);

        if (registration.TokenRequestClientFactory is not null)
        {{
            services.TryAddScoped<ITokenRequestClient>(registration.TokenRequestClientFactory);
        }}
        else if (registration.TokenRequestClient is not null)
        {{
            services.TryAddSingleton(registration.TokenRequestClient);
        }}
        else
        {{
            services.TryAddScoped(typeof(ITokenRequestClient), registration.TokenRequestClientType);
        }}

        var builderFor{@namespace.AsSafeString(replaceDots: true, replacement: "")}TokenRequestClientHttpClient = services.AddHttpClient(__ClientNames.TokenRequestClient);
        if (registration.ConfigureTokenRequestClientBuilder is not null)
        {{
            registration.ConfigureTokenRequestClientBuilder(builderFor{@namespace.AsSafeString(replaceDots: true, replacement: "")}TokenRequestClientHttpClient);
        }}

        var builderFor{@namespace.AsSafeString(replaceDots: true, replacement: "")}DiscoveryCacheHttpClient = services.AddHttpClient(__ClientNames.DiscoveryCache);
        if (registration.ConfigureDiscoveryCacheClientBuilder is not null)
        {{
            registration.ConfigureDiscoveryCacheClientBuilder(builderFor{@namespace.AsSafeString(replaceDots: true, replacement: "")}DiscoveryCacheHttpClient);
        }}

        string authorityUrl = tokenOptions.AuthorityUrl.ToString();
        services.TryAddSingleton(r =>
        {{
            var factory = r.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            return new __{@namespace.AsSafeString(replaceDots: true, replacement: "")}DiscoveryCache(authorityUrl, factory);
        }});";

            registrationClassAdditionals += @"

    public ApiRegistration(TokenOptions tokenOptions)
    {
        TokenOptions = tokenOptions ?? throw new System.ArgumentNullException(nameof(tokenOptions));
    }

    public TokenOptions TokenOptions { get; }

    public System.Action<IHttpClientBuilder>? ConfigureDiscoveryCacheClientBuilder { get; set; }

    public System.Action<IHttpClientBuilder>? ConfigureTokenRequestClientBuilder { get; set; }

    public System.Func<System.IServiceProvider, ITokenRequestClient>? TokenRequestClientFactory { get; set; }

    public ITokenRequestClient? TokenRequestClient { get; set; }

    private System.Type _tokenRequestClientType = typeof(__TokenRequestClient);
    public System.Type TokenRequestClientType
    {
        get => _tokenRequestClientType;
        set
        {
            _tokenRequestClientType = value ?? throw new System.ArgumentNullException(nameof(TokenRequestClientType));
            if (!_tokenRequestClientType.IsAssignableTo(typeof(ITokenRequestClient)))
            {
                throw new System.NotSupportedException(""TokenRequestClientType must inherit ITokenRequestClient"");
            }
        }
    }";

            singletonClientOptions = false;

            if (oAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
            {
                additionalRegistrations += @"

        services.AddHttpContextAccessor();";

                if (oAuthType is OAuthType.CachedTokenExchange)
                {
                    additionalRegistrations += @"

        if (registration.TokenCacheFactory is not null)
        {
            services.TryAddScoped<ITokenCache>(registration.TokenCacheFactory);
        }
        else if (registration.TokenCache is not null)
        {
            services.TryAddSingleton(registration.TokenCache);
        }
        else
        {
            services.TryAddScoped(typeof(ITokenCache), registration.TokenCacheType);
        }

        services.AddMemoryCache();";

                    registrationClassAdditionals += @"

    public System.Func<System.IServiceProvider, ITokenCache>? TokenCacheFactory { get; set; }

    public ITokenCache? TokenCache { get; set; }

    private System.Type _tokenCacheType = typeof(__TokenCache);
    public System.Type TokenCacheType
    {
        get => _tokenCacheType;
        set
        {
            _tokenCacheType = value ?? throw new System.ArgumentNullException(nameof(TokenCacheType));
            if (!_tokenCacheType.IsAssignableTo(typeof(ITokenCache)))
            {
                throw new System.NotSupportedException(""TokenCacheType must inherit ITokenCache"");
            }
        }
    }";
                }
            }
        }

        await File.WriteAllTextAsync(Path.Combine(path, "..", "Registrations.cs"), Constants.Header + $@"using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using {@namespace}.Clients;

namespace {@namespace};

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{modifier} sealed class ApiRegistration
{{
    public System.Func<System.IServiceProvider, ClientOptions>? OptionsFactory {{ get; set; }}

    public ClientOptions? Options {{ get; set; }}

    private System.Type _optionType = typeof(ClientOptions);
    public System.Type OptionType
    {{
        get => _optionType;
        set
        {{
            _optionType = value ?? throw new System.ArgumentNullException(nameof(OptionType));
            if (!typeof(ClientOptions).IsAssignableFrom(_optionType))
            {{
                throw new System.NotSupportedException(""OptionType must inherit ClientOptions"");
            }}
        }}
    }}

    private System.Action<System.IServiceProvider, System.Net.Http.HttpClient>? _configureClientWithServiceProvider;
    public System.Action<System.IServiceProvider, System.Net.Http.HttpClient>? ConfigureClientWithServiceProvider
    {{
        get => _configureClientWithServiceProvider;
        set
        {{
            if (value is not null && _configureClient is not null)
            {{
                throw new System.NotSupportedException(""Can't set both ConfigureClient and ConfigureClientWithServiceProvider at the same time. Pick one!"");
            }}

            _configureClientWithServiceProvider = value;
        }}
    }}

    public System.Action<System.Net.Http.HttpClient>? _configureClient;
    public System.Action<System.Net.Http.HttpClient>? ConfigureClient
    {{
        get => _configureClient;
        set
        {{
            if (value is not null && _configureClientWithServiceProvider is not null)
            {{
                throw new System.NotSupportedException(""Can't set both ConfigureClient and ConfigureClientWithServiceProvider at the same time. Pick one!"");
            }}

            _configureClient = value;
        }}
    }}

    public System.Action<IHttpClientBuilder>? ConfigureClientBuilder {{ get; set; }}{registrationClassAdditionals}
}}



[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{modifier} static class Registrations
{{
    public static class __ClientNames
    {{
{clientNames}
    }}

    public static IServiceCollection RegisterApiClients(this IServiceCollection services, ApiRegistration registration)
    {{
        if (registration.OptionsFactory is not null)
        {{
            services.TryAddScoped<ClientOptions>(registration.OptionsFactory);
        }}
        else if (registration.Options is not null)
        {{
            services.TryAddSingleton(registration.Options);
        }}{(singletonClientOptions ? @"
        else if (registration.OptionType == typeof(ClientOptions))
        {
            services.TryAddSingleton<ClientOptions>();
        }" : "")}
        else
        {{
            services.TryAddScoped(typeof(ClientOptions), registration.OptionType);
        }}

{addHttpClientRegistrations}{additionalRegistrations}

        return services;

        void Register<{(includeInterfaces ? "TClient, " : "")}TImplementation>(string httpClientName){(includeInterfaces ? @"
            where TClient : class" : "")}
            where TImplementation : class{(includeInterfaces ? ", TClient" : "")}
        {{
            var apiClientHttpBuilder = services.AddHttpClient<{(includeInterfaces ? "TClient, " : "")}TImplementation>(httpClientName);

            if (registration.ConfigureClientWithServiceProvider is not null)
            {{
                apiClientHttpBuilder.ConfigureHttpClient(registration.ConfigureClientWithServiceProvider);
            }}
            else if (registration.ConfigureClient is not null)
            {{
                apiClientHttpBuilder.ConfigureHttpClient(registration.ConfigureClient);
            }}

            if (registration.ConfigureClientBuilder is not null)
            {{
                registration.ConfigureClientBuilder(apiClientHttpBuilder);
            }}
        }}
    }}
}}
", token);
    }

    private static async Task GenerateClients(string path, string @namespace, string modifier, bool excludeObsolete, bool includeInterfaces, string clientModifier, Dictionary<string, List<(string apiPath, SwaggerPathBase path)>> clients, HashSet<string> usedComponents, CancellationToken token)
    {
        Logger.LogInformational("Generating Clients");

        int i = 0;
        foreach (var client in clients)
        {
            var name = client.Key + "Client";

            Logger.LogStatus(++i, clients.Count, name);

            var fileName = Path.Combine(path, name + ".cs");

            var body = GetBody();

            if (string.IsNullOrWhiteSpace(body))
            {
                Logger.LogVerbose(name + " has an empty body, skipping.");
                continue;
            }

            var template = Constants.Header + $@"using {@namespace}.Models;

namespace {@namespace}.Clients;

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{clientModifier} sealed class {name}{(includeInterfaces ? " : I" + name : "")}
{{
    private readonly System.Net.Http.HttpClient __my_http_client;
    private readonly ClientOptions __my_options;

    public {name}(System.Net.Http.HttpClient client, ClientOptions options)
    {{
        __my_http_client = client;
        __my_options = options;
    }}
{body}
}}
";
            if (includeInterfaces)
            {
                template += @$"
[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
{modifier} interface I{name}
{{{GetBodySignatures()}
}}
";
            }

            await File.WriteAllTextAsync(fileName, template, token);

            string GetBody()
            {
                StringBuilder builder = new();
                HashSet<string> methodNames = new();

                foreach (var item in client.Value)
                {
                    var body = item.path.GetBody(item.apiPath, methodNames, excludeObsolete);

                    foreach (var componennt in item.path.GetComponents())
                    {
                        usedComponents.Add(componennt);
                    }

                    builder.Append(body);
                }

                return TrimEnd(builder).ToString();
            }

            string GetBodySignatures()
            {
                StringBuilder builder = new();
                HashSet<string> methodNames = new();

                foreach (var item in client.Value)
                {
                    var body = item.path.GetBodySignature(item.apiPath, methodNames, excludeObsolete);

                    foreach (var componennt in item.path.GetComponents())
                    {
                        usedComponents.Add(componennt);
                    }

                    builder.Append(body);
                }

                return TrimEnd(builder).ToString();
            }

            static StringBuilder TrimEnd(StringBuilder sb)
            {
                if (sb.Length != 0)
                {
                    int i = sb.Length - 1;

                    for (; i >= 0; i--)
                    {
                        if (!char.IsWhiteSpace(sb[i]))
                        {
                            break;
                        }
                    }

                    if (i < sb.Length - 1)
                    {
                        sb.Length = i + 1;
                    }
                }

                return sb;
            }
        }

        Logger.BlankLine();
    }

    private static async Task GenerateQueryBuilder(string path, string @namespace, int stringBuilderPoolSize, CancellationToken token)
    {
        Logger.LogInformational("Generating QueryBuilder");
        const string withoutStringBuilders = @"private string _result = """";

    public void AddParameter(string? value, string valueExpression)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _result += '&' + valueExpression + '=' + value;
        }
    }

    public override string ToString()
    {
        if (_result.Length == 0)
        {
            return """";
        }

        return string.Concat(""?"", System.MemoryExtensions.AsSpan(_result, 1));
    }";

        const string withStringBuilders = @"private System.Text.StringBuilder _builder;
    public __QueryBuilder()
    {
        _builder = __StringBuilderPool.Acquire();
    }

    public void AddParameter(string? value, string valueExpression)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = _builder.Append('&').Append(valueExpression).Append('=').Append(value);
        }
    }

    public override string ToString()
    {
        try
        {
            if (_builder.Length > 0)
            {
                _builder[0] = '?';
            }

            return _builder.ToString();
        }
        finally
        {
            __StringBuilderPool.Release(_builder);
            _builder = null!; //Just making sure that we don't share an instance, worst case.
        }
    }";

        if (stringBuilderPoolSize > 0)
        {
            await GenerateStringBuilderPool(path, @namespace, stringBuilderPoolSize, token);
        }

        await File.WriteAllTextAsync(Path.Combine(path, "__QueryBuilder.cs"), Constants.Header + $@"namespace {@namespace}.Clients;

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal struct __QueryBuilder
{{
    {(stringBuilderPoolSize > 0 ? withStringBuilders : withoutStringBuilders)}

    public void AddParameter<T>(T? value, string valueExpression)
    {{
        switch (value)
        {{
            case null:
                break;

            case string stringValue:
                AddParameter(stringValue, valueExpression);
                break;

            case {@namespace}.Models.__ICanIterate valueWithParameters:
                AddParameters(valueWithParameters);
                break;

            case System.Collections.IEnumerable values:
                AddParameter(System.Linq.Enumerable.Cast<object>(values), valueExpression);
                break;

            case System.DateTime dateTime:
                AddParameter(dateTime.ToString(""o"", System.Globalization.CultureInfo.InvariantCulture), valueExpression);
                break;

            default:
                AddParameter(value?.ToString(), valueExpression);
                break;
        }}
    }}

    public void AddParameter<T>(System.Collections.Generic.List<T>? values, string valueExpression)
    {{
        if (values is null)
        {{
            return;
        }}

        AddParameter(System.Linq.Enumerable.AsEnumerable(values), valueExpression);
    }}

    public void AddParameter<T>(System.Collections.Generic.IEnumerable<T?>? values, string valueExpression)
    {{
        if (values is null)
        {{
            return;
        }}

        foreach (T? value in values)
        {{
            AddParameter(value, valueExpression);
        }}
    }}

    private void AddParameters({@namespace}.Models.__ICanIterate values)
    {{
        foreach (var (name, value) in values.IterateProperties())
        {{
            AddParameter(value, name);
        }}
    }}
}}", token);
    }

    private static Task GenerateStringBuilderPool(string path, string @namespace, int stringBuilderPoolSize, CancellationToken token)
    {
        return File.WriteAllTextAsync(Path.Combine(path, "__StringBuilderPool.cs"), Constants.Header + $@"namespace {@namespace}.Clients;

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class __StringBuilderPool
{{
    private static readonly System.Collections.Concurrent.ConcurrentQueue<System.Text.StringBuilder> s_pool = new();
    static __StringBuilderPool()
    {{
        s_pool.Enqueue(new());
        s_pool.Enqueue(new());
        s_pool.Enqueue(new());
        s_pool.Enqueue(new());
        s_pool.Enqueue(new());
    }}

    public static System.Text.StringBuilder Acquire()
    {{
        if (!s_pool.TryDequeue(out var builder))
        {{
            builder = new();
        }}

        return builder;
    }}

    public static void Release(System.Text.StringBuilder builder)
    {{
        if (s_pool.Count <= {stringBuilderPoolSize})
        {{
            //Possible small but insignificant race condition
            builder.Clear();
            s_pool.Enqueue(builder);
        }}
    }}
}}", token);
    }

    private static async Task GenerateClientOptions(string path, string @namespace, string modifier, bool includeOAuth, bool includeJsonSourceGenerators, CancellationToken token)
    {
        Logger.LogInformational("Generating ClientOptions");
        var staticCtor = "";
        if (includeJsonSourceGenerators)
        {
            staticCtor = $@"

        s_defaultOptions.TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine({@namespace.AsSafeString(replaceDots: true, replacement: "")}JsonSerializerContext.Default, new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());";
        }

#pragma warning disable CS0162 // Unreachable code detected
        string deserialize, asyncDeSerializeContent;
        if (Constants.GeneratingNetStandard)
        {
            asyncDeSerializeContent = "async ";
            deserialize = @"
    {
        using System.IO.Stream contentStream = await response.Content.ReadAsStreamAsync();
        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(contentStream, _options, token);
    }";
        }
        else
        {
            asyncDeSerializeContent = "";
            deserialize = " => System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<T>(response.Content, _options, token);";
        }
#pragma warning restore CS0162 // Unreachable code detected

        await File.WriteAllTextAsync(Path.Combine(path, "__ClientOptions.cs"), Constants.Header + $@"namespace {@namespace}.Clients;

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{modifier} class ClientOptions
{{
    private static readonly System.Text.Json.JsonSerializerOptions s_defaultOptions;
    static ClientOptions()
    {{
        s_defaultOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

        s_defaultOptions.PropertyNameCaseInsensitive = true;
        s_defaultOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        s_defaultOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
        s_defaultOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        s_defaultOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());{staticCtor}
    }}
    {(includeOAuth ? @"
    private readonly ITokenRequestClient _tokenRequestClient;
" : "")}
    private readonly System.Text.Json.JsonSerializerOptions? _options;

    public ClientOptions({(includeOAuth ? "ITokenRequestClient tokenRequestClient" : "")}) : this({(includeOAuth ? "tokenRequestClient, " : "")}s_defaultOptions) {{ }}

    public ClientOptions({(includeOAuth ? "ITokenRequestClient tokenRequestClient, " : "")}System.Text.Json.JsonSerializerOptions? options)
    {{{(includeOAuth ? @"
        _tokenRequestClient = tokenRequestClient;" : "")}
        _options = options;
    }}

    internal System.Threading.Tasks.Task<System.Net.Http.HttpRequestMessage> CreateRequest<T>(System.Net.Http.HttpMethod httpMethod, string path, T content, System.Threading.CancellationToken token)
    {{
        System.Net.Http.HttpRequestMessage request = new(httpMethod, path);

        if (content is not null)
        {{
            request.Content = CreateContent(content);
        }}

        return InterceptRequest(request, token);
    }}

    internal System.Threading.Tasks.Task<System.Net.Http.HttpRequestMessage> CreateRequest(System.Net.Http.HttpMethod httpMethod, string path, System.Threading.CancellationToken token)
    {{
        System.Net.Http.HttpRequestMessage request = new(httpMethod, path);
        return InterceptRequest(request, token);
    }}

    protected internal virtual System.Net.Http.HttpContent CreateContent<T>(T content) => new System.Net.Http.StringContent(SerializeContent(content), System.Text.Encoding.UTF8, ""application/json"");

    protected internal {(includeOAuth ? "async " : "")}virtual System.Threading.Tasks.Task<System.Net.Http.HttpRequestMessage> InterceptRequest(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken token)
    {{
        {(includeOAuth
        ? @"request.Headers.Authorization = await _tokenRequestClient.GetTokenAsync(token);
        return request;"
        : "return System.Threading.Tasks.Task.FromResult(request);")}
    }}

    protected internal virtual string SerializeContent<T>(T content) => System.Text.Json.JsonSerializer.Serialize(content, options: _options);

    protected internal virtual {asyncDeSerializeContent}System.Threading.Tasks.Task<T?> DeSerializeContent<T>(System.Net.Http.HttpResponseMessage response, System.Threading.CancellationToken token){deserialize}

    protected internal virtual System.Threading.Tasks.Task InterceptResponse(System.Net.Http.HttpResponseMessage result, System.Threading.CancellationToken token)
    {{
        result.EnsureSuccessStatusCode();
        return System.Threading.Tasks.Task.CompletedTask;
    }}
}}", token);
    }
}
