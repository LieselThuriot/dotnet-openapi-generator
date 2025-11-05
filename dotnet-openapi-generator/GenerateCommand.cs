using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace dotnet.openapi.generator;

public sealed class GenerateCommand : AsyncCommand<Options>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Options settings, CancellationToken cancellationToken)
    {
        try
        {
            AnsiConsole.MarkupLine("[underline][bold]openapi-generator[/] v" + Constants.ProductVersion + "[/]");
            AnsiConsole.WriteLine();

            var sw = Stopwatch.StartNew();

            var result = await AnsiConsole.Progress()
                                          .Columns([
                                              new TaskDescriptionColumn(),
                                              new ProgressBarColumn(),
                                              new PercentageColumn(),
                                              new SpinnerColumn(),
                                              new ElapsedMsColumn()
                                          ])
                                          .StartAsync(ctx => Generate(settings, ctx, sw, cancellationToken));

            AnsiConsole.WriteLine("Done in " + sw.ElapsedMilliseconds + "ms total");
            return result;
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine("[bold red]General error during generation[/]");

            if (settings.Verbose)
            {
                AnsiConsole.WriteException(e);
            }

            return -1;
        }
    }

    private async Task<int> Generate(Options settings, ProgressContext ctx, Stopwatch sw, CancellationToken cancellationToken)
    {
        var document = await GetDocument(ctx, settings, sw, cancellationToken);

        if (document is null)
        {
            AnsiConsole.MarkupLine("[bold red]Could not resolve swagger document[/]");
            return -1;
        }

        await document.Generate(ctx, settings, cancellationToken);

        return 0;
    }

    private static void Prepare(Options options)
    {
        options.Directory ??= Directory.GetCurrentDirectory();
        if (!Path.IsPathRooted(options.Directory.TrimStart('\\', '/')))
        {
            options.Directory = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), options.Directory)).FullName;

            if (options.Verbose)
            {
                AnsiConsole.WriteLine("Path isn't rooted, created rooted path: " + options.Directory);
            }
        }

        if (options.CleanDirectory)
        {
            DirectoryInfo directoryInfo = new(options.Directory);
            if (directoryInfo.Exists)
            {
                if (options.Verbose)
                {
                    AnsiConsole.WriteLine("Cleaning up directory " + directoryInfo.FullName);
                }

                directoryInfo.Delete(true);
                directoryInfo.Create();
            }
        }
        else
        {
            Directory.CreateDirectory(options.Directory);
        }

        options.Namespace ??= options.ProjectName.AsSafeString(replaceDots: false);
    }

    internal async Task<SwaggerDocument?> GetDocument(ProgressContext ctx, Options options, Stopwatch sw, CancellationToken cancellationToken)
    {
        var task = ctx.AddTask("Retrieving documents", maxValue: 1 + (options.AdditionalDocumentLocations?.Count ?? 0));

        Prepare(options);

        using HttpClient client = new();

        var document = await GetDocument(options.DocumentLocation, client, options.Verbose, cancellationToken);
        task.Increment(1);

        if (options.AdditionalDocumentLocations is not null)
        {
            foreach (var additionalLocation in options.AdditionalDocumentLocations)
            {
                var additionalDocument = await GetDocument(additionalLocation, client, options.Verbose, cancellationToken);
                document = Merge(document, additionalDocument);
                task.Increment(1);
            }
        }

#if NET7_0_OR_GREATER
        return System.Text.Json.JsonSerializer.Deserialize(document, SwaggerDocumentTypeInfo.Default.SwaggerDocument);
#else
        return System.Text.Json.JsonSerializer.Deserialize<SwaggerDocument>(document);
#endif
    }

    private static async Task<string> GetDocument(string documentLocation, HttpClient client, bool verbose, CancellationToken cancellationToken)
    {
        string? result = null;
        if (documentLocation.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            result = await GetHttpDocument(client, documentLocation, verbose, cancellationToken);
        }
        else if (File.Exists(documentLocation))
        {
            result = await GetLocalDocument(documentLocation, verbose, cancellationToken);
        }

        if (result is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]Could not resolve document {documentLocation}[/]");
            throw new ApplicationException("Error resolving document");
        }

        if (documentLocation.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
            documentLocation.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            result.SkipWhile(char.IsWhiteSpace).FirstOrDefault() != '{')
        {
            try
            {
                result = ConvertYamlToJson(result, verbose);
            }
            catch (Exception e)
            {
                AnsiConsole.MarkupLine("[bold red]Assumed document was yaml but could not convert it.[/] Continuing as normal.");

                if (verbose)
                {
                    AnsiConsole.WriteException(e);
                }
            }
        }

        return result;
    }

    private static string ConvertYamlToJson(string result, bool verbose)
    {
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                                         .WithAttemptingUnquotedStringTypeDeserialization()
                                         .Build();

        var yamlObject = deserializer.Deserialize(result);

        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                                       .JsonCompatible()
                                       .Build();

        var json = serializer.Serialize(yamlObject);


        if (verbose)
        {
            AnsiConsole.WriteLine("Converted document to json");
        }

        return json;
    }

    private static Task<string> GetLocalDocument(string documentLocation, bool verbose, CancellationToken cancellationToken)
    {
        if (verbose)
        {
            AnsiConsole.WriteLine("Resolving local document");
        }

        return File.ReadAllTextAsync(documentLocation, cancellationToken);
    }

    private static async Task<string> GetHttpDocument(HttpClient client, string documentLocation, bool verbose, CancellationToken cancellationToken)
    {
        if (verbose)
        {
            AnsiConsole.WriteLine("Resolving online document");
        }

        using var result = await client.GetAsync(documentLocation, cancellationToken);

        result.EnsureSuccessStatusCode();

        return await result.Content.ReadAsStringAsync(cancellationToken);
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
