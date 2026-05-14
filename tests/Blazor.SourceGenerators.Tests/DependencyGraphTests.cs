// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class DependencyGraphTests
{
    [Fact]
    public void AllDependentTypes_TerminatesWhenGraphContainsCycle()
    {
        // Construct a cyclic dependency: Node ↔ Document.
        // The real lib.dom.d.ts type graph has cycles like this; previously
        // 'Flatten<T>' and recursive 'AllDependentTypes' would stack overflow.
        var node = new CSharpObject(TypeName: "Node", ExtendsTypeName: null);
        var document = new CSharpObject(TypeName: "Document", ExtendsTypeName: null);
        node.DependentTypes["Document"] = document;
        document.DependentTypes["Node"] = node;

        var deps = node.AllDependentTypes;

        Assert.Contains(deps, t => t.TypeName == "Node");
        Assert.Contains(deps, t => t.TypeName == "Document");
    }

    [Fact]
    public void Flatten_TerminatesWhenGraphContainsCycle()
    {
        var node = new CSharpObject(TypeName: "Node", ExtendsTypeName: null);
        var document = new CSharpObject(TypeName: "Document", ExtendsTypeName: null);
        node.DependentTypes["Document"] = document;
        document.DependentTypes["Node"] = node;

        var visited = node.DependentTypes
            .Flatten(
                childSelector: pair => pair.Value.DependentTypes,
                keySelector: pair => pair.Key,
                keyComparer: StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key)
            .ToList();

        Assert.Contains("Document", visited);
        Assert.Contains("Node", visited);
    }

    [Fact]
    public void AllDependentTypes_ReturnsSameInstanceOnRepeatedAccess()
    {
        // Memoization guard — AllDependentTypes was recomputed on every access,
        // which is quadratic in graph depth.
        var leaf = new CSharpObject(TypeName: "Leaf", ExtendsTypeName: null);
        var root = new CSharpObject(TypeName: "Root", ExtendsTypeName: null);
        root.DependentTypes["Leaf"] = leaf;

        var first = root.AllDependentTypes;
        var second = root.AllDependentTypes;

        Assert.Same(first, second);
    }
}
