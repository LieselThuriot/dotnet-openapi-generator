using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace dotnet.openapi.generator.Cli;

public sealed class Options : CommandSettings
{
    [CommandArgument(0, "<name>")]
    [Description("Name of the project")]
    public string ProjectName { get; set; } = default!;

    [CommandArgument(1, "<document>")]
    [Description("Location of the JSON swagger document. Can be both an http location or a local one")]
    public string DocumentLocation { get; set; } = default!;

    [CommandOption("-n|--namespace")]
    [Description("(Default: Project name) The namespace used for the generated files")]
    public string? Namespace { get; set; }

    [CommandOption("-d|--directory")]
    [Description("(Default: Current Directory) The directory to place the files in")]
    public string? Directory { get; set; }

    [CommandOption("-m|--modifier")]
    [Description("The modifier for the generated files. Can be Public or Internal")]
    [DefaultValue(Modifier.Public)]
    public Modifier Modifier { get; set; }

    [CommandOption("-c|--clean-directory")]
    [Description("Delete folder before generating")]
    [DefaultValue(false)]
    public bool CleanDirectory { get; set; }

    [CommandOption("-f|--filter")]
    [Description("(Default: No filter) Only generate Clients that match the supplied regex filter")]
    public Regex? Filter { get; set; }

    [CommandOption("--client-modifier")]
    [Description("(Default: -m) The modifier for the generated clients; Useful when generating with interfaces. Can be Public or Internal")]
    public Modifier? ClientModifier { get; set; }

    [CommandOption("-s|--tree-shake")]
    [Description("Skip generating unused models")]
    [DefaultValue(false)]
    public bool TreeShaking { get; set; }

    [CommandOption("--json-constructor-attribute")]
    [Description("Json Constructor Attribute. Constructors are generated when the class contains required properties")]
    [DefaultValue("System.Text.Json.Serialization.JsonConstructor")]
    public string? JsonConstructorAttribute { get; set; }

    [CommandOption("--json-polymorphic-attribute")]
    [Description("Json Polymorphic Attribute. Marks the generated types as polymorphic using the specified attribute. {name} is used as a template placeholder")]
    [DefaultValue("System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = \"{name}\")")]
    public string? JsonPolymorphicAttribute { get; set; }

    [CommandOption("--json-derived-type-attribute")]
    [Description("Json Derived Type Attribute. Marks the derived types of the generated types using the specified attribute. {type} and {value} are used as template placeholders")]
    [DefaultValue("System.Text.Json.Serialization.JsonDerivedType(typeof({type}), typeDiscriminator: \"{value}\")")]
    public string? JsonDerivedTypeAttribute { get; set; }

    [CommandOption("--json-property-name-attribute")]
    [Description("Json Property Name Attribute. Some property names are not valid in C#. This will make sure serialization works out. {name} is used as a template placeholder")]
    [DefaultValue("System.Text.Json.Serialization.JsonPropertyName(\"{name}\")")]
    public string? JsonPropertyNameAttribute { get; set; }

#if NET7_0_OR_GREATER
    [CommandOption("-j|--json-source-generators")]
    [Description("Include dotnet 7.0+ Json Source Generators")]
    [DefaultValue(false)]
#endif
    public bool IncludeJsonSourceGenerators { get; set; }

#if NET7_0_OR_GREATER
    [CommandOption("-r|--required-properties")]
    [Description("Include C# 11 Required keywords")]
    [DefaultValue(false)]
#endif
    public bool SupportRequiredProperties { get; set; }

    [CommandOption("--stringbuilder-pool-size")]
    [Description("StringBuilder pool size for building query params. If 0, a simple string concat is used instead")]
    [DefaultValue(50)]
    public int StringBuilderPoolSize { get; set; }

    [CommandOption("--oauth-type")]
    [Description("Includes an OAuth Client. Can be ClientCredentials, ClientCredentialsWithCertificate, TokenExchange or CachedTokenExchange")]
    [DefaultValue(OAuthType.None)]
    public OAuthType OAuthType { get; set; }

    [CommandOption("-i|--interfaces")]
    [Description("Generate interfaces for the clients")]
    [DefaultValue(false)]
    public bool IncludeInterfaces { get; set; }

    [CommandOption("-p|--no-project")]
    [Description("Do not generate project")]
    [DefaultValue(false)]
    public bool ExcludeProject { get; set; }

    [CommandOption("-o|--no-obsolete")]
    [Description("Do not generate obsolete endpoints")]
    [DefaultValue(false)]
    public bool ExcludeObsolete { get; set; }

    [CommandOption("-a|--additional-document")]
    [Description("Location of additional swagger document, used to merge into the main one. Can be both an http location or a local one and can be used multiple times")]
    public IList<string>? AdditionalDocumentLocations { get; set; }

    [CommandOption("--include-options-params")]
    [Description("Generate key value option params on each method to pass to each HttpRequestMessage.Options")]
    [DefaultValue(false)]
    public bool GenerateRequestOptionsDictionary { get; set; }

    [CommandOption("-v|--verbose")]
    [Description("Verbose logging")]
    [DefaultValue(false)]
    public bool Verbose { get; set; }
}