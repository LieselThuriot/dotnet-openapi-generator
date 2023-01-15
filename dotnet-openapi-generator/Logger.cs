using static System.Console;
using static System.ConsoleColor;
using static System.Environment;

namespace dotnet.openapi.generator;

internal static class Logger
{
    internal static bool Verbose { get; set; }

    public static void LogStatus(int current, int max, string message)
    {
        LogStatus(current + "/" + max + ": " + message);
    }

    public static void LogStatus(string message)
    {
        BlankLine();
        Log(message);
    }

    public static void BlankLine()
    {
        if (CursorLeft > 0)
        {
            Log("\r", new string(' ', CursorLeft), "\r");
        }
    }

    public static void LogInformational(string message)
    {
        Log(message, NewLine);
    }

    public static void LogVerbose(Exception exception)
    {
        if (Verbose)
        {
            if (CursorLeft > 0)
            {
                Log(NewLine);
            }

            LogColored(Red, "[VERBOSE] Exception: ", exception.Message, NewLine);
        }
    }

    public static void LogVerbose(string message)
    {
        if (Verbose)
        {
            if (CursorLeft > 0)
            {
                Log(NewLine);
            }

            LogColored(Gray, "[VERBOSE] ", message, NewLine);
        }
    }

    public static void LogWarning(string message)
    {
        LogColored(Yellow, "[WARNING] ", message, NewLine);
    }

    public static void LogError(string message)
    {
        LogColored(Red, "[ERROR] ", message, NewLine);
    }

    private static void LogColored(ConsoleColor color, params string[] messages)
    {
        var oldColor = ForegroundColor;
        try
        {
            ForegroundColor = color;
            Log(messages);
        }
        finally
        {
            ForegroundColor = oldColor;
        }
    }

    private static void Log(params string[] messages)
    {
        Array.ForEach(messages, Log);
    }

    private static void Log(string message)
    {
        Write(message);
    }
}
