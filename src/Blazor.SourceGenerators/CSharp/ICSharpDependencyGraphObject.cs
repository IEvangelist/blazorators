// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

internal interface ICSharpDependencyGraphObject
{
    IImmutableSet<DependentType> AllDependentTypes { get; }
    Dictionary<string, CSharpObject> DependentTypes { get; }
}

internal readonly record struct DependentType(string TypeName, CSharpObject Object);

internal class DependentTypeComparer : IEqualityComparer<DependentType>
{
    internal static readonly DependentTypeComparer Default = new();

    public bool Equals(DependentType x, DependentType y)
    {
        return x.TypeName == y.TypeName;
    }

    public int GetHashCode(DependentType obj)
    {
        return obj.TypeName.GetHashCode();
    }
}