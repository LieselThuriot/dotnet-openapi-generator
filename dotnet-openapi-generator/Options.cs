using CommandLine;
using System.Text.RegularExpressions;

namespace dotnet.openapi.generator;

public class Options
{
    [Value(0, Required = true, HelpText = "Name of the project")]
    public string ProjectName { get; set; } = default!;

    [Value(1, Required = true, HelpText = "Location of the swagger document. Can be both an http location or a local one")]
    public string DocumentLocation { get; set; } = default!;

    [Option('n', "namespace", Required = false, HelpText = "(Default: Project name) The namespace used for the generated files")]
    public string? Namespace { get; set; }

    [Option('d', "directory", Required = false, HelpText = "(Default: Current Directory) The directory to place the files in")]
    public string? Directory { get; set; }

    [Option('m', "modifier", Required = false, HelpText = "The modifier for the generated files. Can be Public or Internal", Default = Modifier.Public)]
    public Modifier Modifier { get; set; }

    [Option('c', "clean-directory", Required = false, HelpText = "Delete folder before generating", Default = false)]
    public bool CleanDirectory { get; set; }

    [Option('f', "filter", Required = false, HelpText = "(Default: No filter) Only generate Clients that match the supplied regex filter")]
    public Regex? Filter { get; set; }

    [Option("client-modifier", Required = false, HelpText = "(Default: -m) The modifier for the generated clients; Useful when generating with interfaces. Can be Public or Internal")]
    public Modifier? ClientModifier { get; set; }

    [Option('s', "tree-shake", Required = false, HelpText = "Skip generating unused models", Default = false)]
    public bool TreeShaking { get; set; }

    [Option("json-constructor-attribute", Required = false, HelpText = "Json Constructor Attribute. Constructors are generated when the class contains required properties", Default = "System.Text.Json.Serialization.JsonConstructor")]
    public string? JsonConstructorAttribute { get; set; }

#if NET7_0_OR_GREATER
    [Option('j', "json-source-generators", Required = false, HelpText = "Include dotnet 7.0+ Json Source Generators", Default = false)]
#endif
    public bool IncludeJsonSourceGenerators { get; set; }

#if NET7_0_OR_GREATER
    [Option('r', "required-properties", Required = false, HelpText = "Include C# 11 Required keywords", Default = false)]
#endif
    public bool SupportRequiredProperties { get; set; }

    [Option("stringbuilder-pool-size", Required = false, HelpText = "StringBuilder pool size for building query params. If 0, a simple string concat is used instead", Default = 50)]
    public int StringBuilderPoolSize { get; set; }

    [Option("oauth-type", Required = false, HelpText = "Includes an OAuth Client. Can be ClientCredentials, TokenExchange or CachedTokenExchange", Default = OAuthType.None)]
    public OAuthType OAuthType { get; set; }

    [Option("oauth-client-credential-style", Required = false, HelpText = "When including an OAuth Client, we can either pass values in the body or as a basic auth header. Can be PostBody or AuthorizationHeader", Default = ClientCredentialStyle.PostBody)]
    public ClientCredentialStyle ClientCredentialStyle { get; set; }

    [Option('i', "interfaces", Required = false, HelpText = "Generate interfaces for the clients", Default = false)]
    public bool IncludeInterfaces { get; set; }

    [Option('p', "no-project", Required = false, HelpText = "Do not generate project", Default = false)]
    public bool ExcludeProject { get; set; }

    [Option('o', "no-obsolete", Required = false, HelpText = "Do not generate obsolete endpoints", Default = false)]
    public bool ExcludeObsolete { get; set; }

    [Option('a', "additional-document", HelpText = "Location of additional swagger document, used to merge into the main one. Can be both an http location or a local one and can be used multiple times")]
    public IList<string>? AdditionalDocumentLocations { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Verbose logging", Default = false)]
    public bool Verbose { get; set; }

    private void Prepare()
    {
        Logger.Verbose = Verbose;

        Directory ??= System.IO.Directory.GetCurrentDirectory();

        if (!Path.IsPathRooted(Directory))
        {
            Directory = new DirectoryInfo(Path.Combine(System.IO.Directory.GetCurrentDirectory(), Directory)).FullName;
            Logger.LogVerbose("Path isn't rooted, created rooted path: " + Directory);
        }

        if (CleanDirectory)
        {
            DirectoryInfo directoryInfo = new(Directory);
            if (directoryInfo.Exists)
            {
                Logger.LogVerbose("Cleaning up directory");
                directoryInfo.Delete(true);
                directoryInfo.Create();
            }
        }
        else
        {
            System.IO.Directory.CreateDirectory(Directory);
        }

        Namespace ??= ProjectName.AsSafeString(replaceDots: false);
    }

    internal async Task<SwaggerDocument?> GetDocument()
    {
        Prepare();

        var document = await GetDocument(DocumentLocation);

        if (AdditionalDocumentLocations is not null)
        {
            foreach (var additionalLocation in AdditionalDocumentLocations)
            {
                var additionalDocument = await GetDocument(additionalLocation);
                document = Merge(document, additionalDocument);
            }
        }

#if NET7_0_OR_GREATER
        return System.Text.Json.JsonSerializer.Deserialize(document, SwaggerDocumentTypeInfo.Default.SwaggerDocument);
#else
        return System.Text.Json.JsonSerializer.Deserialize<SwaggerDocument>(document);
#endif
    }

    private static Task<string> GetDocument(string documentLocation)
    {
        if (documentLocation.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return GetHttpDocument(documentLocation);
        }
        else if (File.Exists(documentLocation))
        {
            return GetLocalDocument(documentLocation);
        }

        Logger.LogError("Could not resolve document " + documentLocation);
        return Task.FromException<string>(new("Error resolving document"));
    }

    private static Task<string> GetLocalDocument(string documentLocation)
    {
        Logger.LogVerbose("Resolving local document");
        return File.ReadAllTextAsync(documentLocation);
    }

    private static async Task<string> GetHttpDocument(string documentLocation)
    {
        Logger.LogVerbose("Resolving online document");

        using HttpClient client = new();
        using var result = await client.GetAsync(documentLocation);

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadAsStringAsync();
    }

    private static string Merge(string originalJson, string newContent)
    {
        //System.Text.Json doesn't have merge support yet and it's a giant hassle to implement myself.
        var originalObject = Newtonsoft.Json.Linq.JObject.Parse(originalJson);
        var newObject = Newtonsoft.Json.Linq.JObject.Parse(newContent);

        newObject.Merge(originalObject);

        return newObject.ToString();
    }
}