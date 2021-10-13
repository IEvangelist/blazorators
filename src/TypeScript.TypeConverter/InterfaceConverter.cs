// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace TypeScript.TypeConverter;

class InterfaceConverter
{
    private readonly Regex _interfaceTypeName = new("(?:interface )(?'TypeName'\\S+)");
    private readonly Regex _extendsTypeName = new("(?:extends )(?'TypeName'\\S+)");

    internal string ToCSharpSourceText(string typeScriptInterfaceDefinition)
    {
        CSharpObject? csharpObject = null;

        var lineTokens = new StringTokenizer(typeScriptInterfaceDefinition, new[] { '\n' });
        foreach (var (index, segment) in lineTokens.Select((s, i) =>  (i, s)))
        {
            if (index == 0)
            {
                var typeName = _interfaceTypeName.GetMatchGroupValue(segment.Value, "TypeName");
                var subclass = _extendsTypeName.GetMatchGroupValue(segment.Value, "TypeName");

                if (typeName is not null)
                {
                    csharpObject = new(typeName, subclass);
                    continue;
                }
                else
                {
                    break;
                }
            }

            if (csharpObject is not null)
            {
                var line = segment.Value.Trim();
                if (line is { Length: > 3 })
                {
                    var memberLineTokenizer = new StringTokenizer(line, new[] { ':', ' ', ';' });
                    var memberDefinition = memberLineTokenizer.Where(t => t.Length > 0).ToArray();
                    if (memberDefinition is { Length: 2 })
                    {
                        var memberName = memberDefinition[0];
                        var isNullable = memberName.Value.EndsWith('?');
                        var memberType = memberDefinition[1];

                        csharpObject.Members[
                            isNullable ? memberName.Value[0..^1] : memberName.Value] =
                            (isNullable, memberType.Value);
                    }
                }
            }
        }

        return csharpObject is not null
            ? csharpObject.ToString()
            : throw new ApplicationException("Unable to parse TypeScript type definition.");
    }
}
