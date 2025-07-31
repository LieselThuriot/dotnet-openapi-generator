using System.Text.RegularExpressions;

namespace dotnet.openapi.generator;

internal static partial class Regexes
{
    [GeneratedRegex(@"[`\[\], \+\/\\\{\}\-\<\>]", RegexOptions.Compiled, 1000)] public static partial Regex SafeString();
    [GeneratedRegex(@"[`\[\], \+\/\\\{\}\-\<\>\.]", RegexOptions.Compiled, 1000)] public static partial Regex SafeStringWithoutDots();
    [GeneratedRegex(@"_{2,}", RegexOptions.Compiled, 1000)] public static partial Regex MultiUnderscore();
    [GeneratedRegex(@"^(?<genericType>.+)`\d+(?<typeinfo>\[\[(?<type>.+?), .+?, Version=\d+.\d+.\d+.\d+, Culture=.+?, PublicKeyToken=.+?\]\])$", RegexOptions.Compiled, 1000)] public static partial Regex FullnameType();
    [GeneratedRegex(@"(System\.Collections\.Generic\.List<|System\.Collections\.Generic\.Dictionary<string, )(?<actualComponent>\w+)>", RegexOptions.Compiled, 1000)] public static partial Regex FindActualComponent();
    [GeneratedRegex(@"^\W*(v(ersion)?)?\W*(?<major>\d+)(\.(?<minor>\d+)(\.(?<build>\d+)(\.(?<private>\d+))?)?)?\W*$", RegexOptions.Compiled, 1000)] public static partial Regex Version();
}

#if !NET7_0_OR_GREATER
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class GeneratedRegexAttribute : Attribute
{
#pragma warning disable RCS1163 // Unused parameter.
    public GeneratedRegexAttribute(string pattern, RegexOptions options, int matchTimeoutMilliseconds)
#pragma warning restore RCS1163 // Unused parameter.
    {
    }
}

static partial class Regexes
{
    private static readonly Regex s_SafeString = new(@"[`\[\], \+\/\\\{\}\-]", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
    public static partial Regex SafeString() => s_SafeString;
    private static readonly Regex s_SafeStringWithoutDots = new(@"[`\[\], \+\/\\\{\}\-\.]", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
    public static partial Regex SafeStringWithoutDots() => s_SafeStringWithoutDots;
    private static readonly Regex s_MultiUnderscore = new(@"_{2,}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
    public static partial Regex MultiUnderscore() => s_MultiUnderscore;
    private static readonly Regex s_FullnameType = new(@"^(?<genericType>.+)`\d+(?<typeinfo>\[\[(?<type>.+?), .+?, Version=\d+.\d+.\d+.\d+, Culture=.+?, PublicKeyToken=.+?\]\])$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
    public static partial Regex FullnameType() => s_FullnameType;
    private static readonly Regex s_FindActualComponent = new(@"(System\.Collections\.Generic\.List<|System\.Collections\.Generic\.Dictionary<string, )(?<actualComponent>\w+)>", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
    public static partial Regex FindActualComponent() => s_FindActualComponent;
    private static readonly Regex s_Version = new(@"^\W*(v(ersion)?)?\W*(?<major>\d+)(\.(?<minor>\d+)(\.(?<build>\d+)(\.(?<private>\d+))?)?)?\W*$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
    public static partial Regex Version() => s_Version;
}
#endif