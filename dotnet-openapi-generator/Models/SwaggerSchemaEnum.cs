﻿using System.Text;

namespace dotnet.openapi.generator;

internal class SwaggerSchemaEnum : List<object>
{
    public string GetBody(SwaggerSchemaFlaggedEnum? flaggedEnum, List<string>? enumNames)
    {
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

                if (value is not string)
                {
                    name += " = " + value;
                }
            }
            else
            {
                name = value.ToString() ?? "";
            }

            name = name.AsSafeString();

            if (char.IsDigit(name[0]))
            {
                name = "_" + name;
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

        return builder.ToString().TrimEnd('\n', '\r', ',');
    }
}
