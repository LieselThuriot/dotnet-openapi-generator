namespace dotnet.openapi.generator;

public enum OAuthType
{
    None,
    ClientCredentials,
    TokenExchange,
    CachedTokenExchange
}

public enum ClientCredentialStyle
{
    AuthorizationHeader,
    PostBody
};