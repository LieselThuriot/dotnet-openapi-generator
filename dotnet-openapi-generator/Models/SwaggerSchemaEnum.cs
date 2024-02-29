using System.Text;
using System.Xml;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaEnum : List<object>
{
    public IEnumerable<(string name, string safeName)> IterateValues(SwaggerSchemaFlaggedEnum? flaggedEnum, List<string>? enumNames)
    {
        HashSet<string> unique = new(Count);

        string name, safeName;

        for (int i = 0; i < Count; i++)
        {
            var valueObject = this[i];

            if (valueObject is null)
            {
                //Some documents that define the enum as nullable, also include the null value.
                //This is handled differently in dotnet and should be skipped here.
                continue;
            }

            if (enumNames is not null)
            {
                name = enumNames[i];
            }
            else
            {
                name = valueObject.ToString() ?? "";
            }

            safeName = name.AsSafeString().AsSafeCSharpName("@", "_");

            if (!unique.Add(safeName))
            {
                continue;
            }

            yield return (name, safeName);
        }
    }

    record struct FastEnumValue(string Name, string Value);
    public string GetBody(string enumName, SwaggerSchemaFlaggedEnum? flaggedEnum, List<string>? enumNames, string modifier)
    {
        List<FastEnumValue> fastEnumValues = [];

        HashSet<string> unique = new(Count);

        StringBuilder builder = new();

        var flagCount = 0;
        for (int i = 0; i < Count; i++)
        {
            string name;
            var value = this[i];

            if (value is null)
            {
                //Some documents that define the enum as nullable, also include the null value.
                //This is handled differently in dotnet and should be skipped here.
                continue;
            }

            if (enumNames is not null)
            {
                name = enumNames[i];
            }
            else
            {
                name = value.ToString() ?? "";
            }

            var safeName = name.AsSafeString().AsSafeCSharpName("@", "_");
            fastEnumValues.Add(new(safeName, name));

            if (safeName.TrimStart('@') != name.Split(" = ")[0])
            {
                Logger.Break();
                Logger.LogWarning($"Enum \'{enumName}\' has a value that's not supported by default in dotnet: \'{name}\' --> \'{safeName}\'.");
                Logger.LogWarning("\tThis has been marked with an EnumMember attribute.");
                Logger.LogWarning("\tSystem.Text.Json and Newtonsoft.Json support has been added out of the box.");
                Logger.LogWarning("\tPlease manually add the needed serialization support to your ClientOptions when using any other libraries.");
                Logger.Break();
                
                name = $@"[System.Runtime.Serialization.EnumMember(Value = ""{name}"")]{safeName}";
            }
            else
            {
                name = safeName;
            }

            if (enumNames is not null && value is not string)
            {
                name += " = " + value;
            }

            if (!unique.Add(name))
            {
                continue;
            }

            if (flaggedEnum is null || !name.Contains(flaggedEnum.separatingStrings))
            {
                builder.Append('\t').Append(name);

                if (flaggedEnum is not null)
                {
                    builder.Append(" = ");

                    if (i == 0)
                    {
                        builder.Append(0);
                    }
                    else
                    {
                        builder.Append("1 << ").Append(flagCount++);
                    }
                }

                builder.Append(',').AppendLine();
            }
        }

        AppendFastEnumHelpers(builder, enumName, modifier, fastEnumValues);
        AppendConverter(builder, enumName, modifier);

        return builder.ToString().TrimEnd('\n', '\r', ',');
    }

    private void AppendConverter(StringBuilder builder, string enumName, string modifier)
    {
        builder.Append('}')
            .AppendLine()
            .AppendLine()
            .Append(modifier).Append(" class ").Append(enumName).Append("EnumConverter : System.Text.Json.Serialization.JsonConverter<").Append(enumName).Append('>').AppendLine()
            .AppendLine("{")
            .Append("    public override ").Append(enumName).AppendLine(" Read(ref System.Text.Json.Utf8JsonReader reader, System.Type typeToConvert, System.Text.Json.JsonSerializerOptions options)")
            .AppendLine("    {")
            .Append("        return ").Append(enumName).AppendLine("FastEnum.FromString(reader.GetString());")
            .AppendLine("    }")
            .AppendLine()
            .Append("    public override void Write(System.Text.Json.Utf8JsonWriter writer, ").Append(enumName).AppendLine(" value, System.Text.Json.JsonSerializerOptions options)")
            .AppendLine("    {")
            .Append("        writer.WriteStringValue(").Append(enumName).AppendLine("FastEnum.ToString(value));")
            .AppendLine("    }");
    }

    private void AppendFastEnumHelpers(StringBuilder builder, string enumName, string modifier, IEnumerable<FastEnumValue> fastEnumValues)
    {
        builder.Append('}')
            .AppendLine()
            .AppendLine();

        builder.Append(modifier).Append(" static class ").Append(enumName).AppendLine("FastEnum")
               .AppendLine("{")
               .Append("     public static string ToString(").Append(enumName).AppendLine(" value) => value switch")
               .AppendLine("     {");

        foreach (var item in fastEnumValues)
        {
            builder.Append("         ").Append(enumName).Append('.').Append(item.Name).Append(" => \"").Append(item.Value).AppendLine("\",");
        }

        builder.AppendLine("         _ => throw new System.NotSupportedException(value + \" is not a supported Enum value\")")
               .AppendLine("     };")
               .AppendLine()
               .Append("     public static ").Append(enumName).AppendLine(" FromString(string? value) => value switch")
               .AppendLine("     {");


        foreach (var item in fastEnumValues)
        {
            builder.Append("         \"").Append(item.Value).Append("\" => ").Append(enumName).Append('.').Append(item.Name).Append(',').AppendLine();
        }

        builder.AppendLine("         _ => throw new System.NotSupportedException((value ?? \"NULL\") + \" is not a supported Enum value\")")
               .AppendLine("     };");
    }
}
