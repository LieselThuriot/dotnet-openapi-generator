using System.Security;
using System.Text.RegularExpressions;

namespace dotnet.openapi.generator;

public class SwaggerInfo
{
    public string? title { get; set; }
    public string? description { get; set; }
    public string? version { get; set; }

    public string GetProjectTags()
    {
        string tags = "";

        if (!string.IsNullOrEmpty(version))
        {
            var match = Regexes.Version().Match(version);

            if (match.Success)
            {
                var versionTag = match.Groups["major"].Value;
                void AppendVersion(Group group, int? alternative)
                {
                    if (group.Success)
                    {
                        versionTag += "." + group.Value;
                    }
                    else if (alternative.HasValue)
                    {
                        versionTag += "." + alternative.GetValueOrDefault();
                    }
                }

                AppendVersion(match.Groups["minor"], 0);
                AppendVersion(match.Groups["build"], 0);
                AppendVersion(match.Groups["private"], null);

                tags += $@"
	<AssemblyVersion>1.0.0</AssemblyVersion>
	<Version>{versionTag}</Version>";
            }
        }

        if (!string.IsNullOrEmpty(title))
        {
            var escaped = SecurityElement.Escape(title);
            tags += $@"
	<Title>{escaped}</Title>
	<Product>{escaped}</Product>";
        }

        if (!string.IsNullOrEmpty(description))
        {
            tags += $@"
	<Description>{SecurityElement.Escape(description)}</Description>";
        }

        return tags;
    }
}