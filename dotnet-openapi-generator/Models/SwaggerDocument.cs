using dotnet.openapi.generator.Cli;
using Spectre.Console;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace dotnet.openapi.generator;

#if NET7_0_OR_GREATER
[System.Text.Json.Serialization.JsonSerializable(typeof(SwaggerDocument))]
internal sealed partial class SwaggerDocumentTypeInfo : System.Text.Json.Serialization.JsonSerializerContext;
#endif

internal sealed class SwaggerDocument
{
    public SwaggerComponents components { get; set; } = default!;
    public SwaggerPaths paths { get; set; } = default!;
    public SwaggerInfo? info { get; set; }

    public async Task Generate(ProgressContext ctx, Options options, CancellationToken token = default)
    {
        string path = options.Directory!;
        string @namespace = options.Namespace!;
        bool excludeObsolete = options.ExcludeObsolete;
        Regex? filter = options.Filter;
        bool includeInterfaces = options.IncludeInterfaces;
        string? jsonConstructorAttribute = options.JsonConstructorAttribute;
        string? jsonPolymorphicAttribute = options.JsonPolymorphicAttribute;
        string? jsonDerivedTypeAttribute = options.JsonDerivedTypeAttribute;
        int stringBuilderPoolSize = options.StringBuilderPoolSize;
        bool treeShaking = options.TreeShaking && (excludeObsolete || filter is not null);
        bool includeJsonSourceGenerators = options.IncludeJsonSourceGenerators;
        bool supportRequiredProperties = options.SupportRequiredProperties;
        string? jsonPropertyNameAttribute = options.JsonPropertyNameAttribute;
        bool includeOptionsDictionary = options.GenerateRequestOptionsDictionary;

        string modifierValue = options.Modifier.ToString().ToLowerInvariant();
        string clientModifierValue = options.ClientModifier?.ToString().ToLowerInvariant() ?? modifierValue;

        if (components.schemas is null)
        {
            components.BuildSchemas(GetFilteredPaths(paths, excludeObsolete, filter));
        }

        IEnumerable<string> usedComponents = await paths.Generate(ctx,
                                                                  path,
                                                                  @namespace,
                                                                  modifierValue,
                                                                  excludeObsolete,
                                                                  filter,
                                                                  includeInterfaces,
                                                                  clientModifierValue,
                                                                  stringBuilderPoolSize,
                                                                  options.OAuthType,
                                                                  includeJsonSourceGenerators,
                                                                  components.schemas!,
                                                                  includeOptionsDictionary,
                                                                  options.Verbose,
                                                                  token);

        await components.Generate(ctx,
                                  path,
                                  @namespace,
                                  modifierValue,
                                  clientModifierValue,
                                  usedComponents,
                                  treeShaking,
                                  jsonConstructorAttribute,
                                  jsonPolymorphicAttribute,
                                  jsonDerivedTypeAttribute,
                                  jsonPropertyNameAttribute,
                                  includeJsonSourceGenerators,
                                  supportRequiredProperties,
                                  options.Verbose,
                                  token);

        if (!options.ExcludeProject)
        {
            await GenerateProject(ctx, options, token);
        }

        if (options.OAuthType is not OAuthType.None)
        {
            await GenerateOAuth(ctx, options, token);
        }
    }

    private static SwaggerPaths GetFilteredPaths(SwaggerPaths paths, bool excludeObsolete, Regex? filter)
    {
        if (!excludeObsolete && filter is null)
        {
            return paths;
        }

        SwaggerPaths filteredPaths = [];
        foreach (var (key, value) in paths)
        {
            if (excludeObsolete && value.IterateMembers().Any(x => x.deprecated))
            {
                continue;
            }

            if (filter?.IsMatch(key.TrimStart('/')) == false)
            {
                continue;
            }

            filteredPaths.Add(key, value);
        }

        return filteredPaths;
    }

    public async Task GenerateProject(ProgressContext ctx, Options options, CancellationToken token = default)
    {
        var task = ctx.AddTask("Project File", maxValue: 1);

        string file = Path.Combine(options.Directory!, options.ProjectName + ".csproj");
        var netVersion = Constants.Version;
        string? additionalTags = info?.GetProjectTags();
        string additionalIncludes = "";

        if (options.OAuthType is not OAuthType.TokenExchange and not OAuthType.CachedTokenExchange)
        {
            additionalIncludes = $"<PackageReference Include=\"Microsoft.Extensions.Http\" Version=\"[{netVersion.Major}.*,)\" />";
        }
#if !NET10_0_OR_GREATER
        else
        {
            additionalIncludes = $"<PackageReference Include=\"Microsoft.Extensions.Http\" Version=\"[{netVersion.Major}.*,)\" />";
        }
#endif

        if (options.OAuthType is not OAuthType.None)
        {
            if (options.OAuthType is OAuthType.ClientCredentialsWithCertificate)
            {
                additionalIncludes += $"""

                        <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="[8.*,)" />
                        <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="[8.*,)" />
                    """;
            }

            if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
            {
#if NET8_0_OR_GREATER
                additionalIncludes += """

    <FrameworkReference Include="Microsoft.AspNetCore.App" />
""";
#else
                additionalIncludes += @"
    <PackageReference Include=""Microsoft.AspNetCore.Http"" Version=""[2.*,)"" />";
#endif

#if !NET10_0_OR_GREATER
                if (options.OAuthType is OAuthType.CachedTokenExchange)
                {
                    additionalIncludes += $"""

    <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="[{netVersion.Major}.*,)" />
""";
                }
#endif
            }
        }
#if !GENERATING_NETSTANDARD
        const
#endif
        string targetframework = "";
#if GENERATING_NETSTANDARD
        {
            additionalIncludes += $@"
    <PackageReference Include=""System.Text.Json"" Version=""[6.*,)"" />";
            targetframework = "standard";
        }
#endif

        await File.WriteAllTextAsync(file, $"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net{targetframework}{netVersion.Major}.{netVersion.Minor}</TargetFramework>
    <LangVersion>latest</LangVersion>{additionalTags}
  </PropertyGroup>

  <ItemGroup>
    {additionalIncludes}
  </ItemGroup>

</Project>

""", cancellationToken: token);

        task.Increment(1);
    }

    public static async Task GenerateOAuth(ProgressContext ctx, Options options, CancellationToken token = default)
    {
        var task = ctx.AddTask("OAuth Clients", maxValue: 1);

        string file = Path.Combine(options.Directory!, "Clients", "__TokenRequestClient.cs");
        string modifierValue = options.Modifier.ToString().ToLowerInvariant();

        string additionalHelpers = "";
        string additionalCtorParameters = "";
        if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            additionalCtorParameters += ", Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor";
            if (options.OAuthType is OAuthType.CachedTokenExchange)
            {
                additionalCtorParameters += ", ITokenCache tokenCache";

                additionalHelpers += $$"""


[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
{{modifierValue}} interface ITokenCache
{
    System.Threading.Tasks.Task<ApiAccessToken> GetOrCreateAsync(string currentToken, System.Func<System.Threading.Tasks.Task<ApiAccessToken>> factory);
}

[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal class __TokenCache : ITokenCache
{
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _tokenCache;

    public __TokenCache(Microsoft.Extensions.Caching.Memory.IMemoryCache tokenCache)
    {
        _tokenCache = tokenCache;
    }

    public System.Threading.Tasks.Task<ApiAccessToken> GetOrCreateAsync(string currentToken, System.Func<System.Threading.Tasks.Task<ApiAccessToken>> factory)
    {
        return Microsoft.Extensions.Caching.Memory.CacheExtensions.GetOrCreateAsync(_tokenCache, "{{options.Namespace}}.TokenExchange." + currentToken, async entry =>
        {
            var result = await factory();
            entry.AbsoluteExpirationRelativeToNow = result.GetExpiration();
            return result;
        })!;
    }
}
""";
            }
        }

        await File.WriteAllTextAsync(file, Constants.Header + $$"""
namespace {{options.Namespace}}.Clients;{{additionalHelpers}}

[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class __{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryCache
{
    private readonly string _authorityUrl;
    private readonly System.Net.Http.IHttpClientFactory _factory;
    private readonly System.Threading.SemaphoreSlim _lock = new(1,1);

    private __{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryDocumentResponse? _discoveryDocumentResponse;

    public __{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryCache(string authorityUrl, System.Net.Http.IHttpClientFactory factory)
    {
        _authorityUrl = authorityUrl ?? throw new System.ArgumentNullException(nameof(authorityUrl));
        _factory = factory;
    }

    public async System.Threading.Tasks.Task<__{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryDocumentResponse> GetAsync()
    {
        if (_discoveryDocumentResponse is not null)
        {
            return _discoveryDocumentResponse;
        }

        await _lock.WaitAsync();
        try
        {
            if (_discoveryDocumentResponse is not null)
            {
                return _discoveryDocumentResponse;
            }

            var client = _factory.CreateClient(Registrations.__ClientNames.DiscoveryCache);
            var res = await client.GetAsync(new System.Uri(new System.Uri(_authorityUrl), ".well-known/openid-configuration"));

            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();

            // TODO : Avoid going with STJ specifically
            _discoveryDocumentResponse = System.Text.Json.JsonSerializer.Deserialize<__{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryDocumentResponse>(json);

            return _discoveryDocumentResponse ?? throw new System.Exception("Could not resolve discovery document");
        }
        finally
        {
            _lock.Release();
        }
    }
}

[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
public interface ITokenRequestClient
{
    System.Threading.Tasks.Task<ApiAccessToken> GetTokenAsync(System.Threading.CancellationToken cancellationToken = default);
}

[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class __TokenRequestClient : ITokenRequestClient
{
    private readonly __{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryCache _discoveryCache;
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;
    private readonly TokenOptions _tokenOptions;
    {{GenerateFieldsBasedOnType(options)}}

    public __TokenRequestClient(__{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryCache discoveryCache, System.Net.Http.IHttpClientFactory httpClientFactory, TokenOptions tokenOptions{{additionalCtorParameters}})
    {
        _discoveryCache = discoveryCache;
        _httpClientFactory = httpClientFactory;
        _tokenOptions = tokenOptions;
        {{GeneratorCtorFieldsBasedOnType(options)}}
    }

    public System.Threading.Tasks.Task<ApiAccessToken> GetTokenAsync(System.Threading.CancellationToken cancellationToken)
    {
        {{GenerateGetTokenBodyBasedOnType(options)}}
    }

    private System.Exception CouldNotGetToken(__{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}TokenResponse? response)
    {
        if (response is null)
        {
            return new System.Exception("Token request failed");
        }

        return new System.Exception(response.ErrorDescription ?? response.Error ?? "Could not request token");
    }
}

[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{{modifierValue}} sealed class TokenOptions
{
    public TokenOptions(System.Uri authorityUrl, string clientId, string clientSecret, {{(options.OAuthType is OAuthType.ClientCredentialsWithCertificate ? "string audience, string clientCertificate, string clientCertificatePassword, string scopes = \"\", int expiration = 15" : "string scopes = \"\"")}})
    {
        AuthorityUrl = authorityUrl ?? throw new System.ArgumentNullException(nameof(authorityUrl));
        ClientId = clientId ?? throw new System.ArgumentNullException(nameof(clientId));
        ClientSecret = clientSecret ?? throw new System.ArgumentNullException(nameof(clientSecret));
        Scopes = scopes ?? "";{{(options.OAuthType is OAuthType.ClientCredentialsWithCertificate ? @"    
        Audience = audience ?? throw new System.ArgumentNullException(nameof(audience));
        Expiration = expiration > 0 ? expiration : throw new System.ArgumentException(nameof(expiration));
        ClientCertificate = clientCertificate ?? throw new System.ArgumentNullException(nameof(clientCertificate));
        ClientCertificatePassword = clientCertificatePassword ?? throw new System.ArgumentNullException(nameof(clientCertificatePassword));" : "")}}
    }

    public System.Uri AuthorityUrl { get; }
    public string ClientId { get; }
    public string ClientSecret { get; }
    public string Scopes { get; }{{(options.OAuthType is OAuthType.ClientCredentialsWithCertificate ? @"
    public string Audience { get; }
    public int Expiration { get; } = 15;
    public string ClientCertificate { get; }
    public string ClientCertificatePassword { get; }" : "")}}
}

[System.CodeDom.Compiler.GeneratedCode("dotnet -openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{{modifierValue}} sealed class ApiAccessToken
{
    public ApiAccessToken(string accessToken, string tokenType, int expiresIn)
    {
        AccessToken = accessToken;
        TokenType = tokenType;
        ExpiresIn = expiresIn;
        Creation = System.DateTime.UtcNow;
    }

    public static implicit operator ApiAccessToken(__{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}TokenResponse response) => new(response.AccessToken!, response.TokenType!, response.ExpiresIn.GetValueOrDefault());
    public static implicit operator System.Net.Http.Headers.AuthenticationHeaderValue(ApiAccessToken token) => new(token.TokenType, token.AccessToken);

    public string AccessToken { get; }
    public string TokenType { get; }
    public int ExpiresIn { get; }
    public System.DateTime Creation { get; }

    public bool IsValid() => (Creation + GetExpiration()) > System.DateTime.UtcNow;
    public System.TimeSpan GetExpiration() => System.TimeSpan.FromSeconds(ExpiresIn) - System.TimeSpan.FromMinutes(1);
}

[System.CodeDom.Compiler.GeneratedCode("dotnet -openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{{modifierValue}} sealed class __{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryDocumentResponse
{
    {{(options.JsonPropertyNameAttribute is not null ? "[" + options.JsonPropertyNameAttribute?.Replace("{name}", "token_endpoint") + "]" : "")}}public Uri? TokenEndpoint { get; set; }
}

[System.CodeDom.Compiler.GeneratedCode("dotnet -openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{{modifierValue}} sealed class __{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}TokenResponse
{
    {{(options.JsonPropertyNameAttribute is not null ? "[" + options.JsonPropertyNameAttribute?.Replace("{name}", "access_token") + "]" : "")}}public string? AccessToken { get; set; }
    {{(options.JsonPropertyNameAttribute is not null ? "[" + options.JsonPropertyNameAttribute?.Replace("{name}", "token_type") + "]" : "")}}public string? TokenType { get; set; }
    {{(options.JsonPropertyNameAttribute is not null ? "[" + options.JsonPropertyNameAttribute?.Replace("{name}", "expires_in") + "]" : "")}}public int? ExpiresIn { get; set; }
    public string? Error { get; set; }
    {{(options.JsonPropertyNameAttribute is not null ? "[" + options.JsonPropertyNameAttribute?.Replace("{name}", "error_description") + "]" : "")}}public string? ErrorDescription { get; set; }
}
""", cancellationToken: token);

        task.Increment(1);
    }

    private static string GenerateGetTokenBodyBasedOnType(Options options)
    {
        if (options.OAuthType is OAuthType.ClientCredentials or OAuthType.ClientCredentialsWithCertificate)
        {
            string result = $@"var currentAccessToken = _accessToken;

        if (currentAccessToken?.IsValid() is true)
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
            if (currentAccessToken?.IsValid() is true)
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

        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, discoveryDocumentResponse.TokenEndpoint!);

        var form = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string,string>>();

        form.Add(new System.Collections.Generic.KeyValuePair<string,string>(""grant_type"", ""client_credentials""));
        form.Add(new System.Collections.Generic.KeyValuePair<string,string>(""client_id"", ""ClientId""));

        if (!string.IsNullOrWhiteSpace(options.ClientSecret))
        {{
            form.Add(new System.Collections.Generic.KeyValuePair<string,string>(""client_secret"", options.ClientSecret));
        }}

        if (!string.IsNullOrWhiteSpace(options.Scopes))
        {{
            form.Add(new System.Collections.Generic.KeyValuePair<string,string>(""scope"", options.Scopes));
        }}
";

            if (options.OAuthType is OAuthType.ClientCredentialsWithCertificate)
            {
                result += $@"
        var clientAuthenticationToken = new System.IdentityModel.Tokens.Jwt.JwtPayload()
        {{
            {{ ""sub"", options.ClientId }},
            {{ ""iss"", options.ClientId }},
            {{ ""jti"", Microsoft.IdentityModel.Tokens.UniqueId.CreateRandomId() }},
            {{ ""exp"", System.DateTimeOffset.UtcNow.AddMinutes(options.Expiration).ToUnixTimeSeconds() }},
            {{ ""aud"", options.Audience }}
        }};

        var header = new System.IdentityModel.Tokens.Jwt.JwtHeader(_credentials);
        var secToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(header, clientAuthenticationToken);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

        string signedClientAuthenticationToken = handler.WriteToken(secToken);

        form.Add(new System.Collections.Generic.KeyValuePair<string,string>(""client_assertion_type"", ""urn:ietf:params:oauth:client-assertion-type:jwt-bearer""));
        form.Add(new System.Collections.Generic.KeyValuePair<string,string>(""client_assertion"", signedClientAuthenticationToken));
";
            }

            result += $@"
        request.Content = new System.Net.Http.FormUrlEncodedContent(form);

        var client = _httpClientFactory.CreateClient(Registrations.__ClientNames.TokenRequestClient);

        var responseMsg = await client.SendAsync(request, cancellationToken);

        if (!responseMsg.IsSuccessStatusCode)
        {{
            throw CouldNotGetToken(null);
        }}

        var content = await responseMsg.Content.ReadAsStringAsync(cancellationToken);

        // TODO : Avoid going with STJ specifically
        var response = System.Text.Json.JsonSerializer.Deserialize<__{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}TokenResponse>(content);

        if (response is null || !string.IsNullOrEmpty(response.Error))
        {{
            throw CouldNotGetToken(response);
        }}

        return response;";

            return result;
        }
        else if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            return $$"""
string? currentToken = GetAccessToken();

        if (currentToken is null)
        {
            return System.Threading.Tasks.Task.FromException<ApiAccessToken>(new("Current token not found"));
        }

        {{(options.OAuthType is OAuthType.CachedTokenExchange
        ? "return _tokenCache.GetOrCreateAsync(currentToken, () => Exchange(currentToken, cancellationToken));"
        : "return Exchange(currentToken, cancellationToken);")}}
    }

    private async System.Threading.Tasks.Task<ApiAccessToken> Exchange(string currentToken, System.Threading.CancellationToken cancellationToken)
    {
        var discoveryDocumentResponse = await _discoveryCache.GetAsync();

        var client = _httpClientFactory.CreateClient(Registrations.__ClientNames.TokenRequestClient);
        var parameters = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>()
        {
            new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:token-exchange"),
            new System.Collections.Generic.KeyValuePair<string, string>("subject_token_type", "urn:ietf:params:oauth:token-type:access_token"),
            new System.Collections.Generic.KeyValuePair<string, string>("subject_token", currentToken),
        };

        if (!string.IsNullOrEmpty(_tokenOptions.Scopes))
        {
            parameters.Add(new("scope", _tokenOptions.Scopes));
        }

        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, discoveryDocumentResponse.TokenEndpoint)
        {
            Content = new System.Net.Http.FormUrlEncodedContent(parameters)
        };

        var httpResponse = await client.SendAsync(request, cancellationToken);
    
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw CouldNotGetToken(null);
        }

        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        // TODO : Avoid going with STJ specifically
        var response = System.Text.Json.JsonSerializer.Deserialize<__{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}TokenResponse>(content);

        if (response is null || !string.IsNullOrEmpty(response.Error))
        {
            throw CouldNotGetToken(response);
        }

        return response;
    }

    private string? GetAccessToken()
    {
        if (_httpContextAccessor.HttpContext is not null && _httpContextAccessor.HttpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return authorizationHeader.ToString()["Bearer ".Length..];
        }

        return null;
""";
        }
        else
        {
            throw NotSupported(options);
        }
    }

    private static string GeneratorCtorFieldsBasedOnType(Options options)
    {
        if (options.OAuthType is OAuthType.ClientCredentials or OAuthType.ClientCredentialsWithCertificate)
        {
            string result = "_readLock = new(1, 1);";

            if (options.OAuthType is OAuthType.ClientCredentialsWithCertificate)
            {
                result += @"
        byte[] privateKeyBinary = System.Convert.FromBase64String(tokenOptions.ClientCertificate);
#pragma warning disable SYSLIB0057 // Type or member is obsolete
        var signingCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(privateKeyBinary, tokenOptions.ClientCertificatePassword, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet | System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
#pragma warning restore SYSLIB0057 // Type or member is obsolete
        var certificateKey = new Microsoft.IdentityModel.Tokens.X509SecurityKey(signingCertificate);
        _credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(certificateKey, ""RS256"");";
            }

            return result;
        }
        else if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            string result = "_httpContextAccessor = httpContextAccessor;";

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
        if (options.OAuthType is OAuthType.ClientCredentials or OAuthType.ClientCredentialsWithCertificate)
        {
            string result = @"private readonly System.Threading.SemaphoreSlim _readLock;
    private ApiAccessToken _accessToken;";

            if (options.OAuthType is OAuthType.ClientCredentialsWithCertificate)
            {
                result += @"
    private readonly Microsoft.IdentityModel.Tokens.SigningCredentials _credentials;";
            }

            return result;
        }
        else if (options.OAuthType is OAuthType.TokenExchange or OAuthType.CachedTokenExchange)
        {
            string result = "private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;";

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

    private static Exception NotSupported(Options options) => new NotSupportedException(options.OAuthType + " is an unsupported value");
}