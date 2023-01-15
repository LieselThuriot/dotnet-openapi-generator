using System.Text.RegularExpressions;

namespace dotnet.openapi.generator;

#if NET7_0_OR_GREATER
[System.Text.Json.Serialization.JsonSerializable(typeof(SwaggerDocument))]
internal partial class SwaggerDocumentTypeInfo : System.Text.Json.Serialization.JsonSerializerContext { }
#endif

internal class SwaggerDocument
{
    public SwaggerComponents components { get; set; } = default!;
    public SwaggerPaths paths { get; set; } = default!;
    public SwaggerInfo? info { get; set; }

    public async Task Generate(Options options, CancellationToken token = default)
    {
        string path = options.Directory!;
        string @namespace = options.Namespace!;
        bool excludeObsolete = options.ExcludeObsolete;
        Regex? filter = options.Filter;
        bool includeInterfaces = options.IncludeInterfaces;
        string? jsonConstructorAttribute = options.JsonConstructorAttribute;
        int stringBuilderPoolSize = options.StringBuilderPoolSize;
        bool treeShaking = options.TreeShaking && (excludeObsolete || filter is not null);
        bool includeJsonSourceGenerators = options.IncludeJsonSourceGenerators;

        string modifierValue = options.Modifier.ToString().ToLowerInvariant();
        string clientModifierValue = options.ClientModifier?.ToString().ToLowerInvariant() ?? modifierValue;

        IEnumerable<string> usedComponents = await paths.Generate(path, @namespace, modifierValue, excludeObsolete, filter, includeInterfaces, clientModifierValue, stringBuilderPoolSize, options.OAuthType, includeJsonSourceGenerators, token);

        await components.Generate(path, @namespace, modifierValue, usedComponents, treeShaking, jsonConstructorAttribute, includeJsonSourceGenerators, token);

        if (!options.ExcludeProject)
        {
            await GenerateProject(options);
        }

        if (options.OAuthType is not OAuthType.None)
        {
            await GenerateOAuth(options);
        }
    }

    public Task GenerateProject(Options options, CancellationToken token = default)
    {
        Logger.LogInformational("Generating CSPROJ");
        var file = Path.Combine(options.Directory!, options.ProjectName + ".csproj");
        var netVersion = Constants.Version;
        var additionalTags = info?.GetProjectTags();
        var additionalIncludes = "";

        if (options.OAuthType is not OAuthType.None)
        {
            additionalIncludes += @"
    <PackageReference Include=""IdentityModel"" Version=""6.0.0"" />";

            if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
            {
                additionalIncludes += @"
    <PackageReference Include=""Microsoft.AspNetCore.Http"" Version=""2.2.2"" />";

                if (options.OAuthType is OAuthType.CachedTokenExchange)
                {
                    additionalIncludes += $@"
    <PackageReference Include=""Microsoft.Extensions.Caching.Memory"" Version=""[{netVersion.Major}.{netVersion.Minor}.{netVersion.Build},)"" />";
                }
            }
        }

        return File.WriteAllTextAsync(file, @$"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net{netVersion.Major}.{netVersion.Minor}</TargetFramework>
	<LangVersion>latest</LangVersion>{additionalTags}
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Http"" Version=""[{netVersion.Major}.{netVersion.Minor}.{netVersion.Build},)"" />{additionalIncludes}
  </ItemGroup>

</Project>
", cancellationToken: token);
    }

    public Task GenerateOAuth(Options options, CancellationToken token = default)
    {
        Logger.LogInformational("Generating OAuth Clients");

        var file = Path.Combine(options.Directory!, "Clients", "__TokenRequestClient.cs");
        string modifierValue = options.Modifier.ToString().ToLowerInvariant();

        var additionalHelpers = "";
        var additionalCtorParameters = "";
        if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            additionalCtorParameters += ", Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor";
            if (options.OAuthType is OAuthType.CachedTokenExchange)
            {
                additionalCtorParameters += ", ITokenCache tokenCache";

                additionalHelpers += $@"

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
{modifierValue} interface ITokenCache
{{
    System.Threading.Tasks.Task<ApiAccessToken> GetOrCreateAsync(string currentToken, System.Func<System.Threading.Tasks.Task<ApiAccessToken>> factory);
}}

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal class __TokenCache : ITokenCache
{{
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _tokenCache;

    public __TokenCache(Microsoft.Extensions.Caching.Memory.IMemoryCache tokenCache)
    {{
        _tokenCache = tokenCache;
    }}

    public System.Threading.Tasks.Task<ApiAccessToken> GetOrCreateAsync(string currentToken, System.Func<System.Threading.Tasks.Task<ApiAccessToken>> factory)
    {{
        return Microsoft.Extensions.Caching.Memory.CacheExtensions.GetOrCreateAsync(_tokenCache, ""{options.Namespace}.TokenExchange."" + currentToken, async entry =>
        {{
            var result = await factory();
            entry.AbsoluteExpirationRelativeToNow = result.GetExpiration();
            return result;
        }})!;
    }}
}}";
            }
        }

        return File.WriteAllTextAsync(file, Constants.Header + @$"namespace {options.Namespace}.Clients;{additionalHelpers}

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class __{options.Namespace.AsSafeString(replaceDots: true).Replace("_", "")}DiscoveryCache
{{
    private readonly IdentityModel.Client.DiscoveryCache _cache;

    public __{options.Namespace.AsSafeString(replaceDots: true).Replace("_", "")}DiscoveryCache(string authorityUrl, System.Net.Http.IHttpClientFactory factory)
    {{
        _cache  = new(authorityUrl, () => factory.CreateClient(""{options.Namespace.AsSafeString(replaceDots: true).Replace("_", "")}DiscoveryCache""));
    }}

    public System.Threading.Tasks.Task<IdentityModel.Client.DiscoveryDocumentResponse> GetAsync() => _cache.GetAsync();
}}

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
public interface ITokenRequestClient
{{
    System.Threading.Tasks.Task<ApiAccessToken> GetTokenAsync(System.Threading.CancellationToken cancellationToken = default);
}}

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class __TokenRequestClient : ITokenRequestClient
{{
    private readonly __{options.Namespace.AsSafeString(replaceDots: true).Replace("_", "")}DiscoveryCache _discoveryCache;
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly TokenOptions _tokenOptions;
    {GenerateFieldsBasedOnType(options)}

    public __TokenRequestClient(__{options.Namespace.AsSafeString(replaceDots: true).Replace("_", "")}DiscoveryCache discoveryCache, System.Net.Http.IHttpClientFactory httpClientFactory, TokenOptions tokenOptions{additionalCtorParameters})
    {{
        _discoveryCache = discoveryCache;
        _httpClientFactory = httpClientFactory;
        _tokenOptions = tokenOptions;
        {GeneratorCtorFieldsBasedOnType(options)}
    }}

    public System.Threading.Tasks.Task<ApiAccessToken> GetTokenAsync(System.Threading.CancellationToken cancellationToken)
    {{
        {GenerateGetTokenBodyBasedOnType(options)}
    }}

    private System.Exception CouldNotGetToken(IdentityModel.Client.TokenResponse response)
    {{
        if (response.ErrorType == IdentityModel.Client.ResponseErrorType.Exception)
        {{
            return response.Exception;
        }}

        return new System.Exception(""Could not request token"");
    }}
}}

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{modifierValue} sealed class TokenOptions
{{
    public TokenOptions(System.Uri authorityUrl, string clientId, string clientSecret, string scopes = """")
    {{
        AuthorityUrl = authorityUrl ?? throw new System.ArgumentNullException(nameof(authorityUrl));
        ClientId = clientId ?? throw new System.ArgumentNullException(nameof(clientId));
        ClientSecret = clientSecret ?? throw new System.ArgumentNullException(nameof(clientSecret));
        Scopes = scopes ?? """";
    }}

    public System.Uri AuthorityUrl {{ get; }}
    public string ClientId {{ get; }}
    public string ClientSecret {{ get; }}
    public string Scopes {{ get; }}
}}

[System.CodeDom.Compiler.GeneratedCode(""dotnet-openapi-generator"", ""{Constants.ProductVersion}"")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{modifierValue} sealed class ApiAccessToken
{{
    public ApiAccessToken(string accessToken, string tokenType, int expiresIn)
    {{
        AccessToken = accessToken;
        TokenType = tokenType;
        ExpiresIn = expiresIn;
        Creation = System.DateTime.UtcNow;
    }}

    public static implicit operator ApiAccessToken(IdentityModel.Client.TokenResponse response) => new(response.AccessToken, response.TokenType, response.ExpiresIn);
    public static implicit operator System.Net.Http.Headers.AuthenticationHeaderValue(ApiAccessToken token) => new(token.TokenType, token.AccessToken);

    public string AccessToken {{ get; }}
    public string TokenType {{ get; }}
    public int ExpiresIn {{ get; }}
    public System.DateTime Creation {{ get; }}

    public bool IsValid() => (Creation + GetExpiration()) > System.DateTime.UtcNow;
    public System.TimeSpan GetExpiration() => System.TimeSpan.FromSeconds(ExpiresIn) - System.TimeSpan.FromMinutes(1);
}}", cancellationToken: token);
    }

    private static string GenerateGetTokenBodyBasedOnType(Options options)
    {
        if (options.OAuthType is OAuthType.ClientCredentials)
        {
            return $@"var currentAccessToken = _accessToken;

        if (currentAccessToken?.IsValid() == true)
        {{
            return System.Threading.Tasks.Task.FromResult(currentAccessToken);
        }}

        return GetTokenLockedAsync(cancellationToken);
    }}

    private async System.Threading.Tasks.Task<ApiAccessToken> GetTokenLockedAsync(System.Threading.CancellationToken cancellationToken)
    {{
        try
        {{
            await _readLock.WaitAsync();

            // Check again, access token might already be refreshed.
            var currentAccessToken = _accessToken;
            if (currentAccessToken?.IsValid() == true)
            {{
                return currentAccessToken;
            }}

            return (_accessToken = await GetNewTokenAsync(cancellationToken));
        }}
        finally
        {{
            _readLock.Release();
        }}
    }}

    private async System.Threading.Tasks.Task<ApiAccessToken> GetNewTokenAsync(System.Threading.CancellationToken cancellationToken)
    {{
        var discoveryDocumentResponse = await _discoveryCache.GetAsync();

        var options = _tokenOptions;

        var tokenClient = new IdentityModel.Client.TokenClient(_httpClientFactory.CreateClient(""{options.Namespace.AsSafeString(replaceDots: true).Replace("_", "")}TokenRequestClient""), new IdentityModel.Client.TokenClientOptions
        {{
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            Address = discoveryDocumentResponse.TokenEndpoint,
            ClientCredentialStyle = IdentityModel.Client.ClientCredentialStyle.PostBody
        }});

        var response = await tokenClient.RequestClientCredentialsTokenAsync(options.Scopes, cancellationToken: cancellationToken);

        if (response.ErrorType != IdentityModel.Client.ResponseErrorType.None)
        {{
            throw CouldNotGetToken(response);
        }}

        return response;";
        }
        else if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            return $@"string? currentToken = GetAccessToken();

        if (currentToken is null)
        {{
            return System.Threading.Tasks.Task.FromException<ApiAccessToken>(new(""Current token not found""));
        }}

        {(options.OAuthType is OAuthType.CachedTokenExchange
        ? "return _tokenCache.GetOrCreateAsync(currentToken, () => Exchange(currentToken, cancellationToken));"
        : "return Exchange(currentToken, cancellationToken);")}
    }}

    private async System.Threading.Tasks.Task<ApiAccessToken> Exchange(string currentToken, System.Threading.CancellationToken cancellationToken)
    {{
        var discoveryDocumentResponse = await _discoveryCache.GetAsync();

        var tokenClient = new IdentityModel.Client.TokenClient(_httpClientFactory.CreateClient(""{options.Namespace.AsSafeString(replaceDots: true).Replace("_", "")}TokenRequestClient""), new IdentityModel.Client.TokenClientOptions
        {{
            Address = discoveryDocumentResponse.TokenEndpoint,
            ClientId = _tokenOptions.ClientId,
            ClientSecret = _tokenOptions.ClientSecret,
            ClientCredentialStyle = IdentityModel.Client.ClientCredentialStyle.PostBody,
            Parameters = new()
            {{
                {{ IdentityModel.OidcConstants.TokenRequest.SubjectTokenType, IdentityModel.OidcConstants.TokenTypeIdentifiers.AccessToken }},
                {{ IdentityModel.OidcConstants.TokenRequest.SubjectToken, currentToken }},
                {{ IdentityModel.OidcConstants.TokenRequest.Scope, _tokenOptions.Scopes }}
            }}
        }});

        var response = await tokenClient.RequestTokenAsync(IdentityModel.OidcConstants.GrantTypes.TokenExchange, cancellationToken: cancellationToken);

        if (response.ErrorType != IdentityModel.Client.ResponseErrorType.None)
        {{
            throw CouldNotGetToken(response);
        }}

        return response;
    }}

    private string? GetAccessToken()
    {{
        if (_httpContext.Request.Headers.TryGetValue(""Authorization"", out var authorizationHeader))
        {{
            return authorizationHeader.ToString()[""Bearer "".Length..];
        }}

        return null;";
        }
        else
        {
            throw NotSupported(options);
        }
    }

    private static string GeneratorCtorFieldsBasedOnType(Options options)
    {
        if (options.OAuthType is OAuthType.ClientCredentials)
        {
            return "_readLock = new(1, 1);";
        }
        else if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            var result = "_httpContext = httpContextAccessor.HttpContext;";

            if (options.OAuthType is OAuthType.CachedTokenExchange)
            {
                result += @"
        _tokenCache = tokenCache;";
            }

            return result;
        }
        else
        {
            throw NotSupported(options);
        }
    }

    private static string GenerateFieldsBasedOnType(Options options)
    {
        if (options.OAuthType is OAuthType.ClientCredentials)
        {
            return @"private readonly System.Threading.SemaphoreSlim _readLock;
    private ApiAccessToken _accessToken;";
        }
        else if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            var result = "private readonly Microsoft.AspNetCore.Http.HttpContext _httpContext;";

            if (options.OAuthType is OAuthType.CachedTokenExchange)
            {
                result += @"
    private readonly ITokenCache _tokenCache;";
            }

            return result;
        }
        else
        {
            throw NotSupported(options);
        }
    }

    private static Exception NotSupported(Options options)
    {
        return new NotSupportedException(options.OAuthType + " is an unsupported value");
    }
}