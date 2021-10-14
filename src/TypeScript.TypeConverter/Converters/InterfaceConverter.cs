// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using TypeScript.TypeConverter.CSharp;
using TypeScript.TypeConverter.Extensions;

namespace TypeScript.TypeConverter.Converters;

class InterfaceConverter
{
    private readonly Regex _interfaceTypeName = new("(?:interface )(?'TypeName'\\S+)");
    private readonly Regex _extendsTypeName = new("(?:extends )(?'TypeName'\\S+)");

    internal CSharpExtensionObject? ToExtensionObject(string? typeScriptTypeDeclaration)
    {
        CSharpExtensionObject? extensionObject = null;

        return extensionObject;
    }

    internal string ToCSharpSourceText(string typeScriptInterfaceDefinition)
    {
        /*

        NOTES:

        There are several kinds of possible TypeScript type definitions we should try to handle.

        For example:

        interface Geolocation {
            clearWatch(watchId: number): void;
            getCurrentPosition(
                successCallback: PositionCallback,
                errorCallback?: PositionErrorCallback | null,
                options?: PositionOptions): void;
            watchPosition(
                successCallback: PositionCallback,
                errorCallback?: PositionErrorCallback | null,
                options?: PositionOptions): number;
        }

        This interface defines three methods. The only "pure" method is the `clearWatch`. It's considered "pure"
        because it doesn't require any additional types, and can be called directly from JavaScript interop.

        The `getCurrentPosition` on the other hand, is a bit more involved. It defines callbacks. In order for .NET objects
        to satisfy JavaScript callbacks, we need an object reference and the corresponding `JSInvokable` method / method name.

        A bit of JavaScript would also have to be generated from this.
         */

        CSharpObject? csharpObject = null;

        var lineTokens = new StringTokenizer(typeScriptInterfaceDefinition, new[] { '\n' });
        foreach (var (index, segment) in lineTokens.Select((s, i) => (i, s)))
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

                        CSharpProperty member = new(memberName.Value, memberType.Value, isNullable);
                        csharpObject.Properties[isNullable ? memberName.Value[0..^1] : memberName.Value] = member;
                    }
                }
            }
        }

        return csharpObject is not null
            ? csharpObject.ToString()
            : throw new ApplicationException("Unable to parse TypeScript type definition.");
    }
}
