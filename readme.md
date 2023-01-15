# openapi-generator for C#

Generates simple C# code based on the OpenAPI specification.

OAuth clients can be generated to easily set up calling a secured api.

Constructors will be added to initialize required or non-nullable properties defined in your schema.

If you your schema contains `allOf` properties and thus supports `Inheritance`,
the classes will correctly inherit from each other and implement the base constructors.
Since `allOf` technically is an array in the swagger document, you may get weird results in the generated code if there's more than one present, 
as this is not supported in the .NET framework.

## Installation

Major and Minor version numbers always dictate the dotnet version being used.

### dotnet 5.0 generator installation
```bash
dotnet tool install dotnet-openapi-generator -g --version 5.0.0-preview.6
```

### dotnet 6.0 generator installation
```bash
dotnet tool install dotnet-openapi-generator -g --version 6.0.0-preview.6
```

### dotnet 7.0 generator installation
```bash
dotnet tool install dotnet-openapi-generator -g --version 7.0.0-preview.6
```


## Getting started

```bash
C:\Git > openapi-generator --help
openapi-generator 7.0.0-preview.6
Steven Thuriot

  -n, --namespace                 (Default: Project name) The namespace used for the generated files

  -d, --directory                 (Default: Current Directory) The directory to place the files in

  -m, --modifier                  (Default: Public) The modifier for the generated files. Can be Public or Internal

  -c, --clean-directory           (Default: false) Delete folder before generating

  -f, --filter                    (Default: No filter) Only generate Clients that match the supplied regex filter

  --client-modifier               (Default: -m) The modifier for the generated clients; Useful when generating with
                                  interfaces. Can be Public or Internal

  -s, --tree-shake                (Default: false) Skip generating unused models

  --json-constructor-attribute    (Default: System.Text.Json.Serialization.JsonConstructor) Json Constructor Attribute.
                                  Constructors are generated when the class contains required properties.

  -j, --json-source-generators    (Default: false) Include dotnet 7.0+ Json Source Generators

  --stringbuilder-pool-size       (Default: 50) StringBuilder pool size for building query params. If 0, a simple string
                                  concat is used instead.

  --oauth-type                    (Default: None) Includes an OAuth Client. Can be ClientCredentials, TokenExchange or
                                  CachedTokenExchange

  -i, --interfaces                (Default: false) Generate interfaces for the clients

  -p, --no-project                (Default: false) Do not generate project

  -o, --no-obsolete               (Default: false) Do not generate obsolete endpoints

  -a, --additional-document       Location of additional swagger document, used to merge into the main one. Can be both
                                  an http location or a local one and can be used multiple times.

  --help                          Display this help screen.

  --version                       Display version information.

  value pos. 0                    Required. Name of the project

  value pos. 1                    Required. Location of the swagger document. Can be both an http location or a local
                                  one
```

## Registration

In your `Program.cs`, add the following or one of its overloads:

```csharp
ApiRegistration registration = new();
builder.Services.RegisterApiClients(registration);
```

In case you included `--oauth-type` during generation, you will have to supply some token information to the `ApiRegistration` constructor:

```csharp
TokenOptions options = new(authorityUrl, clientId, clientSecret, scopes);
ApiRegistration registration = new(options);
builder.Services.RegisterApiClients(registration);
```