namespace dotnet.openapi.generator;

public enum Modifier
{
    Public,
    Internal
}

public static class ModifierFastEnum
{
    public static string ToString(Modifier value) => value switch
    {
        Modifier.Public => "Public",
        _ => throw new ArgumentException(nameof(value)),
    };

    public static Modifier FromString(string value) => value switch
    {
        "Public" => Modifier.Public,
        _ => throw new ArgumentException(nameof(value)),
    };
}