// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using TypeScript.TypeConverter.Extensions;

namespace TypeScript.TypeConverter.CSharp;

public record CSharpExtensionObject(string RawTypeName)
{
    private List<CSharpMethod>? _methods = null!;
    private List<CSharpProperty>? _properties = null!;
    private Dictionary<string, CSharpObject>? _dependentTypes = null!;

    public List<CSharpProperty>? Properties
    {
        get => _properties ??= new();
        init => _properties = value;
    }

    public List<CSharpMethod>? Methods
    {
        get => _methods ??= new();
        init => _methods = value;
    }

    public Dictionary<string, CSharpObject>? DependentTypes
    {
        get => _dependentTypes ??= new(StringComparer.OrdinalIgnoreCase);
        init => _dependentTypes = value;
    }

    public int MemberCount => Properties!.Count + Methods!.Count;

    public string ToStaticPartialClassString()
    {
        StringBuilder builder = new("namespace Microsoft.JSInterop;");

        builder.Append("\r\n\r\n");
        builder.Append("namespace Microsoft.JSInterop.Extensions;");

        builder.Append("\r\n\r\n");
        var typeName = RawTypeName.EndsWith("Extensions") ? RawTypeName : $"{RawTypeName}Extensions";
        builder.Append($"public class {typeName}\r\n");
        builder.Append("{\r\n");

        foreach (var method in Methods ?? Enumerable.Empty<CSharpMethod>())
        {
            var methodName = method.RawName.CapitalizeFirstLetter();
            builder.Append($"    public static ValueTask {methodName}Async(\r\n");
        }

        builder.Append("}\r\n");
        return builder.ToString();
    }
}
