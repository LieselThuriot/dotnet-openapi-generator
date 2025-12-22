using Spectre.Console;

namespace dotnet.openapi.generator;

internal static class ErrorContext
{
    private static readonly List<string> _errors = [];

    public static void AddErrors(params IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            AddError(error);
        }
    }

    public static void AddError(string error)
    {
        AddErrorWithMarkup("[bold red]" + error + "[/]");
    }

    public static void AddErrorWithMarkup(params IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            AddErrorWithMarkup(error);
        }
    }

    public static void AddErrorWithMarkup(string error)
    {
        _errors.Add(error);
    }

    public static void PrintErrors()
    {
        if (_errors.Count is 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[underline bold red]Errors/Warnings encountered during generation:[/]");
        AnsiConsole.WriteLine();

        foreach (string error in _errors)
        {
            AnsiConsole.MarkupLine(error);
        }
    }
}
