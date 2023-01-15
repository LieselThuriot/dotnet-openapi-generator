using CommandLine;
using dotnet.openapi.generator;
using System.Diagnostics;

await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async o =>
            {
				try
                {
                    Logger.LogInformational("openapi-generator v" + Constants.ProductVersion);
                    Logger.LogInformational("----");

                    var sw = Stopwatch.StartNew();
                    Logger.LogStatus("Retrieving documents...");

                    SwaggerDocument? document = await o.GetDocument();

                    Logger.BlankLine();
                    Logger.LogInformational("Retrieved documents in " + sw.ElapsedMilliseconds + "ms");
                    Logger.LogInformational("----");

                    if (document is null)
                    {
                        Logger.LogError("Could not resolve swagger document");
                        return;
                    }

                    await document.Generate(o);

                    Logger.LogInformational("----");
                    Logger.LogInformational("Done in " + sw.ElapsedMilliseconds + "ms total");
                }
				catch (Exception e)
                {
                    Logger.LogInformational("----");
                    Logger.LogError("General error during generation");
                    Logger.LogVerbose(e);
                }
            });