// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Xunit;
using Xunit.Abstractions;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for multi-extends interface merging (Phase B1).
///
/// <para>
/// TypeScript allows an interface to extend multiple base interfaces:
/// <code>interface DocumentFragment extends Node, NonElementParentNode, ParentNode { ... }</code>
/// The generator must capture all bases, parse the dependent declarations
/// from the same source, and merge their member sets into the derived
/// type so the emitted C# faithfully exposes inherited properties and
/// methods. Without the merge, consumers see the derived type as
/// missing every inherited member.
/// </para>
/// </summary>
public class MultiExtendsTests
{
    private readonly ITestOutputHelper _output;

    public MultiExtendsTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DocumentFragment_MergesMembersFromEveryBase()
    {
        var reader = TypeDeclarationReader.Default;
        var parser = new TypeDeclarationParser(reader);
        Assert.True(reader.TryGetDeclaration("DocumentFragment", out var text)
            && !string.IsNullOrWhiteSpace(text));

        var obj = parser.ToObject(text!);
        Assert.NotNull(obj);
        _output.WriteLine($"ExtendsTypeName='{obj!.ExtendsTypeName}'");
        _output.WriteLine($"Properties: {string.Join(", ", obj.Properties.Keys)}");
        _output.WriteLine($"Methods: {string.Join(", ", obj.Methods.Keys)}");

        // Primary base must be the first identifier in the extends list,
        // not the comma-trailing token the naive regex would produce.
        Assert.Equal("Node", obj.ExtendsTypeName);

        // Members from DocumentFragment itself.
        Assert.Contains("ownerDocument", obj.Properties.Keys);

        // Members from ParentNode (a base that's neither first nor last
        // in the extends list).
        Assert.Contains("childElementCount", obj.Properties.Keys);
        Assert.Contains("children", obj.Properties.Keys);
        Assert.Contains("firstElementChild", obj.Properties.Keys);
        Assert.Contains("lastElementChild", obj.Properties.Keys);
        Assert.Contains("append", obj.Methods.Keys);

        // getElementById is declared on both NonElementParentNode and
        // DocumentFragment itself; the derived type wins.
        Assert.Contains("getElementById", obj.Methods.Keys);
        var getElementById = obj.Methods["getElementById"];
        Assert.Contains("HTMLElement", getElementById.RawReturnTypeName);
    }
}
