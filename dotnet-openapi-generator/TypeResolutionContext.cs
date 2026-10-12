namespace dotnet.openapi.generator;

internal static class TypeResolutionContext
{
    private static readonly AsyncLocal<string?> s_numberType = new();

    public static string NumberType => s_numberType.Value ?? "double";

    public static IDisposable UseNumberType(string numberType)
    {
        string? previousNumberType = s_numberType.Value;
        s_numberType.Value = numberType;

        return new NumberTypeScope(previousNumberType);
    }

    private sealed class NumberTypeScope(string? previousNumberType) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                s_numberType.Value = previousNumberType;
                _disposed = true;
            }
        }
    }
}
