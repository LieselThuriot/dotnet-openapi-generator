using dotnet.openapi.generator;
using Spectre.Console.Cli;

CommandApp<GenerateCommand> app = new();
await app.RunAsync(args);