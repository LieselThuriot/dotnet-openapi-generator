using Spectre.Console;
using System.Text.RegularExpressions;

namespace dotnet.openapi.generator;

#if NET7_0_OR_GREATER
[System.Text.Json.Serialization.JsonSerializable(typeof(SwaggerDocument))]
internal partial class SwaggerDocumentTypeInfo : System.Text.Json.Serialization.JsonSerializerContext;
#endif

internal class SwaggerDocument
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
                                                                  components.schemas,
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
            await GenerateProject(ctx, options);
        }

        if (options.OAuthType is not OAuthType.None)
        {
            await GenerateOAuth(ctx, options);
        }
    }

    public async Task GenerateProject(ProgressContext ctx, Options options, CancellationToken token = default)
    {
        var task = ctx.AddTask("Generating CSPROJ", maxValue: 1);

        var file = Path.Combine(options.Directory!, options.ProjectName + ".csproj");
        var netVersion = Constants.Version;
        var additionalTags = info?.GetProjectTags();
        var additionalIncludes = "";

        if (options.OAuthType is not OAuthType.None)
        {
            additionalIncludes += """

    <PackageReference Include="IdentityModel" Version="[7.*,)" />
""";

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
                if (options.OAuthType is OAuthType.CachedTokenExchange)
                {
                    additionalIncludes += $"""

    <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="[{netVersion.Major}.*,)" />
""";
                }
            }
        }

        string targetframework = "";
#if GENERATING_NETSTANDARD
        {
            additionalIncludes += $@"
    <PackageReference Include=""System.Text.Json"" Version=""[6.*,)"" />";
            targetframework = "standard";
        }
#endif

        await  File.WriteAllTextAsync(file, $"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net{targetframework}{netVersion.Major}.{netVersion.Minor}</TargetFramework>
    <LangVersion>latest</LangVersion>{additionalTags}
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" Version="[{netVersion.Major}.*,)" />{additionalIncludes}
  </ItemGroup>

</Project>

""", cancellationToken: token);

        task.Increment(1);
    }

    public async Task GenerateOAuth(ProgressContext ctx, Options options, CancellationToken token = default)
    {
        var task = ctx.AddTask("Generating OAuth Clients", maxValue: 1);

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
    private readonly IdentityModel.Client.DiscoveryCache _cache;

    public __{{options.Namespace.AsSafeString(replaceDots: true, replacement: "")}}DiscoveryCache(string authorityUrl, System.Net.Http.IHttpClientFactory factory)
    {
        _cache  = new(authorityUrl, () => factory.CreateClient(Registrations.__ClientNames.DiscoveryCache));
    }

    public System.Threading.Tasks.Task<IdentityModel.Client.DiscoveryDocumentResponse> GetAsync() => _cache.GetAsync();
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

    private System.Exception CouldNotGetToken(IdentityModel.Client.TokenResponse response)
    {
        if (response.ErrorType == IdentityModel.Client.ResponseErrorType.Exception)
        {
            return response.Exception ?? new(response.Error ?? "Unknown Error");
        }

        return new System.Exception("Could not request token");
    }
}

[System.CodeDom.Compiler.GeneratedCode("dotnet-openapi-generator", "{{Constants.ProductVersion}}")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
{{modifierValue}} sealed class TokenOptions
{
    public TokenOptions(System.Uri authorityUrl, string clientId, string clientSecret, {{ (options.OAuthType is OAuthType.ClientCredentialsWithCertificate ? "string audience, string clientCertificate, string clientCertificatePassword, string scopes = \"\", int expiration = 15" : "string scopes = \"\"")}})
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
    public string Scopes { get; }{{ (options.OAuthType is OAuthType.ClientCredentialsWithCertificate ? @"
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

    public static implicit operator ApiAccessToken(IdentityModel.Client.TokenResponse response) => new(response.AccessToken!, response.TokenType!, response.ExpiresIn);
    public static implicit operator System.Net.Http.Headers.AuthenticationHeaderValue(ApiAccessToken token) => new(token.TokenType, token.AccessToken);

    public string AccessToken { get; }
    public string TokenType { get; }
    public int ExpiresIn { get; }
    public System.DateTime Creation { get; }

    public bool IsValid() => (Creation + GetExpiration()) > System.DateTime.UtcNow;
    public System.TimeSpan GetExpiration() => System.TimeSpan.FromSeconds(ExpiresIn) - System.TimeSpan.FromMinutes(1);
}
""", cancellationToken: token);

        task.Increment(1);
    }

    private static string GenerateGetTokenBodyBasedOnType(Options options)
    {
        if (options.OAuthType is OAuthType.ClientCredentials or OAuthType.ClientCredentialsWithCertificate)
        {
            var result = $@"var currentAccessToken = _accessToken;

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

        var parameters = new IdentityModel.Client.Parameters(
        [
            System.Collections.Generic.KeyValuePair.Create(""client_assertion_type"", ""urn:ietf:params:oauth:client-assertion-type:jwt-bearer""),
            System.Collections.Generic.KeyValuePair.Create(""client_assertion"", signedClientAuthenticationToken),
        ]);
";
            }

            result += $@"
        var tokenClient = new IdentityModel.Client.TokenClient(_httpClientFactory.CreateClient(Registrations.__ClientNames.TokenRequestClient), new IdentityModel.Client.TokenClientOptions
        {{
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            Address = discoveryDocumentResponse.TokenEndpoint!,
            ClientCredentialStyle = IdentityModel.Client.ClientCredentialStyle.{options.ClientCredentialStyle}";

            if (options.OAuthType is OAuthType.ClientCredentialsWithCertificate)
            {
                result += @",
            Parameters = parameters";
            }

            result += @"
        });

        var response = await tokenClient.RequestClientCredentialsTokenAsync(options.Scopes, cancellationToken: cancellationToken);

        if (response.ErrorType != IdentityModel.Client.ResponseErrorType.None)
        {
            throw CouldNotGetToken(response);
        }

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

        var tokenClient = new IdentityModel.Client.TokenClient(_httpClientFactory.CreateClient(Registrations.__ClientNames.TokenRequestClient), new IdentityModel.Client.TokenClientOptions
        {
            Address = discoveryDocumentResponse.TokenEndpoint!,
            ClientId = _tokenOptions.ClientId,
            ClientSecret = _tokenOptions.ClientSecret,
            ClientCredentialStyle = IdentityModel.Client.ClientCredentialStyle.{{options.ClientCredentialStyle}},
            Parameters = new()
            {
                { IdentityModel.OidcConstants.TokenRequest.SubjectTokenType, IdentityModel.OidcConstants.TokenTypeIdentifiers.AccessToken },
                { IdentityModel.OidcConstants.TokenRequest.SubjectToken, currentToken },
                { IdentityModel.OidcConstants.TokenRequest.Scope, _tokenOptions.Scopes }
            }
        });

        var response = await tokenClient.RequestTokenAsync(IdentityModel.OidcConstants.GrantTypes.TokenExchange, cancellationToken: cancellationToken);

        if (response.ErrorType != IdentityModel.Client.ResponseErrorType.None)
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
            var result = "_readLock = new(1, 1);";

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
            var result = "_httpContextAccessor = httpContextAccessor;";

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
            var result =  @"private readonly System.Threading.SemaphoreSlim _readLock;
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
            var result = "private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;";

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