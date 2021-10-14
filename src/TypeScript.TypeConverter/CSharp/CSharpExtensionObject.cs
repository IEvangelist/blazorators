// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace TypeScript.TypeConverter.CSharp;

public record CSharpExtensionObject(string RawTypeName)
{
    private List<CSharpMethod>? _methods = null!;
    private List<CSharpProperty>? _properties = null!;
    private List<CSharpObject>? _dependentTypes = null!;

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

    public List<CSharpObject>? DependentTypes
    {
        get => _dependentTypes ??= new();
        init => _dependentTypes = value;
    }

    public int MemberCount => Properties!.Count + Methods!.Count;
}
