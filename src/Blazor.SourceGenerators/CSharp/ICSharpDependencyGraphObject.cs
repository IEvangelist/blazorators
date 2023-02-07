// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

internal interface ICSharpDependencyGraphObject
{
    Dictionary<string, CSharpObject> DependentTypes { get; }

    IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes { get; }
}
