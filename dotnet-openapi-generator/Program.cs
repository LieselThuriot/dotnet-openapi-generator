using dotnet.openapi.generator.Cli;
using Spectre.Console.Cli;

CommandApp<GenerateCommand> app = new();
await app.RunAsync(args);