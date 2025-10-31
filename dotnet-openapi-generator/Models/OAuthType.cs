namespace dotnet.openapi.generator;

public enum OAuthType
{
    None,
    ClientCredentials,
    ClientCredentialsWithCertificate,
    TokenExchange,
    CachedTokenExchange
}

public enum ClientCredentialStyle
{
    AuthorizationHeader,
    PostBody
};