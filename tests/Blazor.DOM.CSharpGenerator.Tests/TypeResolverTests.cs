// TypeResolver tests: verifies correct projections and hard-errors on unsupported types.

using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class TypeResolverTests
{
    private static TypeResolver EmptyResolver() => new([]);

    private static TypeResolver WithSymbol(string name) =>
        new([new SymbolModel(0, name, 0, [],  false,
            new SemanticModel("matched", name, "definition", null, ["interface"],
                [], [], false, false, [], false, false, false, [], []))]);

    // ── Keyword types ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("VoidKeyword", "void")]
    [InlineData("BooleanKeyword", "bool")]
    [InlineData("NumberKeyword", "double")]
    [InlineData("StringKeyword", "string")]
    [InlineData("AnyKeyword", "object")]
    [InlineData("UnknownKeyword", "object")]
    [InlineData("ObjectKeyword", "object")]
    public void Project_Keyword_MapsCorrectly(string kwName, string expectedCsType)
    {
        var resolver = EmptyResolver();
        var node = new KeywordTypeNode(kwName);
        var result = resolver.Project(node, "test");
        Assert.Equal(expectedCsType, result.CSharpType);
    }

    [Fact]
    public void Project_NeverKeyword_ThrowsTypeProjectionException()
    {
        var resolver = EmptyResolver();
        var node = new KeywordTypeNode("NeverKeyword");
        Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test"));
    }

    // ── Primitives must NOT degrade to object ──────────────────────────────────

    [Fact]
    public void Project_KnownReference_DoesNotReturnObject()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("GLenum", "GLenum", []);
        var result = resolver.Project(node, "test");
        Assert.Equal("uint", result.CSharpType);
        Assert.NotEqual("object", result.CSharpType);
    }

    [Fact]
    public void Project_PromiseVoid_ReturnsValueTask()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("Promise", "Promise<void>",
            [new KeywordTypeNode("VoidKeyword")]);
        var result = resolver.Project(node, "test");
        Assert.Equal("ValueTask", result.CSharpType);
    }

    [Fact]
    public void Project_PromiseString_ReturnsValueTaskString()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("Promise", "Promise<string>",
            [new KeywordTypeNode("StringKeyword")]);
        var result = resolver.Project(node, "test");
        Assert.Equal("ValueTask<string>", result.CSharpType);
    }

    // ── Nullable unions ────────────────────────────────────────────────────────

    [Fact]
    public void Project_TOrNullUnion_ReturnsNullable()
    {
        var resolver = EmptyResolver();
        var node = new UnionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new KeywordTypeNode("NullKeyword"),
        ]);
        var result = resolver.Project(node, "test");
        Assert.True(result.IsNullable);
        Assert.Equal("string", result.CSharpType);
    }

    // ── Hard errors on unsupported shapes ─────────────────────────────────────

    [Fact]
    public void Project_IntersectionType_DefersUnprovenComposition()
    {
        var resolver = EmptyResolver();
        var node = new IntersectionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new KeywordTypeNode("BooleanKeyword"),
        ]);
        var error = Assert.Throws<GenericDeferralException>(
            () => resolver.Project(node, "test/intersection"));
        Assert.Equal("intersection-composition", error.Phase);
    }

    [Fact]
    public void Project_EmptyTypeLiteral_DefersAnonymousShape()
    {
        var resolver = EmptyResolver();
        var node = new TypeLiteralTypeNode([]);
        var error = Assert.Throws<GenericDeferralException>(
            () => resolver.Project(node, "test/typeLiteral"));
        Assert.Equal("anonymous-structural-members", error.Phase);
    }

    [Fact]
    public void Project_TemplateLiteral_ThrowsTypeProjectionException()
    {
        var resolver = EmptyResolver();
        var node = new TemplateLiteralTypeNode([]);
        Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test/templateLiteral"));
    }

    [Fact]
    public void Project_MixedUnion_ThrowsTypeProjectionException()
    {
        // A union of string and a reference that isn't null -> unsupported
        var resolver = EmptyResolver();
        var node = new UnionTypeNode([
            new KeywordTypeNode("StringKeyword"),
            new ReferenceTypeNode("SomeOtherType", null, []),
        ]);
        // SomeOtherType isn't in the symbol index -> will throw
        Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test/mixed"));
    }

    [Fact]
    public void Project_UnresolvedReference_ThrowsTypeProjectionException()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("NoSuchType", null, []);
        var ex = Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test"));
        Assert.Contains("NoSuchType", ex.Message);
        // Must NOT return "object"
        Assert.DoesNotContain("object", ex.Message.Replace("TypeProjection", ""));
    }

    // ── EventHandler deferred ──────────────────────────────────────────────────

    [Fact]
    public void Project_EventHandler_ThrowsDeferredProjectionException()
    {
        var resolver = EmptyResolver();
        var node = new ReferenceTypeNode("EventHandler", null, []);
        var ex = Assert.Throws<TypeProjectionException>(() => resolver.Project(node, "test"));
        Assert.Contains("deferred", ex.Message.ToLowerInvariant());
    }

    // ── Array types ────────────────────────────────────────────────────────────

    [Fact]
    public void Project_ArrayOfString_ReturnsStringArray()
    {
        var resolver = EmptyResolver();
        var node = new ArrayTypeNode(new KeywordTypeNode("StringKeyword"));
        var result = resolver.Project(node, "test");
        Assert.Equal("string[]", result.CSharpType);
        Assert.True(result.IsCollection);
    }

    // ── Known symbol references ────────────────────────────────────────────────

    [Fact]
    public void Project_KnownSymbolReference_ReturnsCSharpTypeName()
    {
        var resolver = WithSymbol("AbortSignal");
        var node = new ReferenceTypeNode("AbortSignal", "AbortSignal", []);
        var result = resolver.Project(node, "test");
        // Interface-classified symbols are emitted as I-prefixed partial interfaces.
        Assert.Equal("IAbortSignal", result.CSharpType);
    }
}
