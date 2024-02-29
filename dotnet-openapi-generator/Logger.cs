using static System.Console;
using static System.ConsoleColor;
using static System.Environment;

namespace dotnet.openapi.generator;

internal static class Logger
{
    internal static bool Verbose { get; set; }

    internal static readonly bool s_canBeFancy;

    static Logger()
    {
        try
        {
            _ = CursorLeft; //This crashes in some consoles, e.g. Powershell ISE
            s_canBeFancy = true;
        }
        catch
        {
            s_canBeFancy = false;
        }
    }


    public static void LogStatus(int current, int max, string message)
    {
        LogStatus(current + "/" + max + ": " + message);
    }

    public static void LogStatus(string message)
    {
        if (!Verbose)
        {
            BlankLine();
        }
        else if (s_canBeFancy)
        {
            Log(NewLine);
        }

        Log(message);
    }

    public static void Break() => Log(NewLine);

    public static void BlankLine()
    {
        if (s_canBeFancy)
        {
            if (CursorLeft > 0)
            {
                SetCursorPosition(0, CursorTop);
                Write(new string(' ', WindowWidth));
                SetCursorPosition(0, CursorTop);
            }
        }
        else
        {
            Log(NewLine);
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
            if (!s_canBeFancy || CursorLeft > 0)
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
            if (!s_canBeFancy || CursorLeft > 0)
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
        if (s_canBeFancy)
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
        else
        {
            Log(messages);
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
