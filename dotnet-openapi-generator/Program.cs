using CommandLine;
using dotnet.openapi.generator;
using System.Diagnostics;

using static dotnet.openapi.generator.Logger;

await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async o =>
            {
				try
                {
                    LogInformational("openapi-generator v" + Constants.ProductVersion);
                    LogInformational("----");

                    var sw = Stopwatch.StartNew();
                    LogStatus("Retrieving documents...");

                    SwaggerDocument? document = await o.GetDocument();

                    BlankLine();
                    LogInformational("Retrieved documents in " + sw.ElapsedMilliseconds + "ms");
                    LogInformational("----");

                    if (document is null)
                    {
                        LogError("Could not resolve swagger document");
                        return;
                    }

                    await document.Generate(o);

                    LogInformational("----");
                    LogInformational("Done in " + sw.ElapsedMilliseconds + "ms total");
                }
				catch (Exception e)
                {
                    LogInformational("----");
                    LogError("General error during generation");
                    LogVerbose(e);
                }
            });