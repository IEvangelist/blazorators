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

    /// <summary>
    /// Phase B2: generic interface name must be normalized.
    /// <c>interface CustomEventInit&lt;T = any&gt; extends EventInit { ... }</c>
    /// previously kept the literal <c>"CustomEventInit&lt;T"</c> token
    /// because the single-token regex greedy-matched non-whitespace. The
    /// parser must surface <c>TypeName == "CustomEventInit"</c> so the
    /// dependent-type registry and downstream emitters can find it.
    /// </summary>
    [Fact]
    public void Generic_Interface_Has_Normalized_Type_Name()
    {
        var reader = TypeDeclarationReader.Default;
        var parser = new TypeDeclarationParser(reader);
        Assert.True(reader.TryGetDeclaration("CustomEventInit", out var text)
            && !string.IsNullOrWhiteSpace(text));

        var obj = parser.ToObject(text!);
        Assert.NotNull(obj);
        _output.WriteLine($"TypeName='{obj!.TypeName}' Extends='{obj.ExtendsTypeName}'");

        Assert.Equal("CustomEventInit", obj.TypeName);
        Assert.Equal("EventInit", obj.ExtendsTypeName);

        // Own member from the generic interface body.
        Assert.Contains("detail", obj.Properties.Keys);

        // Inherited from EventInit (single base, but the type-name fix
        // shouldn't regress the existing single-extends merge).
        Assert.Contains("bubbles", obj.Properties.Keys);
    }

    /// <summary>
    /// Phase B2: depth-aware extends-list parsing. A base like
    /// <c>Map&lt;K, V&gt;</c> contains a comma inside its type-argument
    /// list; splitting the extends clause on every comma would produce
    /// <c>["Map&lt;K", "V&gt;"]</c>. Test the helper indirectly through
    /// the parser by parsing an interface whose extends list mixes
    /// generic and identifier bases. None of <c>lib.dom.d.ts</c>'s
    /// interfaces extends a generic with commas in its type args, so we
    /// use a synthetic interface fed directly through
    /// <see cref="TypeDeclarationParser.ToObject(string)"/>.
    /// </summary>
    [Fact]
    public void Generic_Base_In_Extends_List_Is_Treated_As_Single_Token()
    {
        var reader = TypeDeclarationReader.Default;
        var parser = new TypeDeclarationParser(reader);

        const string synthetic =
            "interface Demo extends Map<string, number>, EventInit {\n" +
            "    own: string;\n" +
            "}";

        var obj = parser.ToObject(synthetic);
        Assert.NotNull(obj);
        _output.WriteLine($"TypeName='{obj!.TypeName}' Extends='{obj.ExtendsTypeName}'");

        Assert.Equal("Demo", obj.TypeName);

        // Map<string, number> should be treated as a single base name.
        // We can't resolve it (not in lib.dom.d.ts as a parseable
        // interface in our reader), so we silently skip the merge --
        // but we MUST NOT have split it across the comma.
        Assert.Equal("Map<string, number>", obj.ExtendsTypeName);

        // EventInit is the second base and must still resolve + merge.
        Assert.Contains("bubbles", obj.Properties.Keys);
        Assert.Contains("own", obj.Properties.Keys);
    }
}
