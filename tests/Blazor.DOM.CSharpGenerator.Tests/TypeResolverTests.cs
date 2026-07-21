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
    public void Project_NeverKeyword_UsesUninhabitedMarker()
    {
        var resolver = EmptyResolver();
        var node = new KeywordTypeNode("NeverKeyword");
        var projection = resolver.Project(node, "test");

        Assert.Equal(
            "global::Blazor.DOM.StandardTypes.TypeScriptNever",
            projection.CSharpType);
        var definition = Assert.Single(resolver.SynthesizedTypes);
        Assert.Equal("Never", definition.Kind);
        Assert.Contains("private TypeScriptNever()", definition.Source);
    }

    [Fact]
    public void Project_StandardError_UsesExactLiveContract()
    {
        var resolver = EmptyResolver();

        var projection = resolver.Project(
            new ReferenceTypeNode("Error", "Error", []),
            "fixture/error");

        Assert.Equal(
            "global::Blazor.DOM.StandardTypes.ITypeScriptError",
            projection.CSharpType);
        Assert.Equal("js-reference", projection.Transport?.Kind);
        Assert.DoesNotContain("object", projection.CSharpType);
        var definition = Assert.Single(resolver.SynthesizedTypes);
        Assert.Equal("Standard", definition.Kind);
        Assert.Contains("string Name { get; }", definition.Source);
        Assert.Contains("string Message { get; }", definition.Source);
        Assert.Contains("string? Stack { get; }", definition.Source);
    }

    [Fact]
    public void Project_QualifiedUserError_RemainsDistinct()
    {
        var symbol = new SymbolModel(
            0,
            "Fixture.Error",
            0,
            [],
            false,
            new SemanticModel(
                "matched",
                "Fixture.Error",
                "definition",
                null,
                ["interface"],
                [],
                [],
                false,
                false,
                [],
                false,
                false,
                false,
                [],
                []));
        var resolver = new TypeResolver([symbol]);

        var projection = resolver.Project(
            new ReferenceTypeNode("Error", "Fixture.Error", []),
            "fixture/qualified-error");

        Assert.Equal(
            "global::Blazor.DOM.Namespaces.Fixture.IError",
            projection.CSharpType);
        Assert.Empty(resolver.SynthesizedTypes);
    }

    [Fact]
    public void Project_StandardExclude_EvaluatesFiniteDomainExactly()
    {
        var keyFormat = new SymbolModel(
            0,
            "KeyFormat",
            0,
            [
                new DeclarationModel(
                    0,
                    "typeAlias",
                    "KeyFormat",
                    [],
                    [],
                    [],
                    [],
                    new UnionTypeNode([
                        new LiteralTypeNode("StringLiteral", "\"jwk\""),
                        new LiteralTypeNode("StringLiteral", "\"pkcs8\""),
                        new LiteralTypeNode("StringLiteral", "\"raw\""),
                        new LiteralTypeNode("StringLiteral", "\"spki\""),
                    ]),
                    [],
                    null,
                    new DocumentationModel("", [], false),
                    new LocationModel("test", new(0, 0, 0), new(0, 0, 0)),
                    null,
                    false,
                    new EventMapModel(false, []),
                    [])
            ],
            false,
            new SemanticModel(
                "matched",
                "KeyFormat",
                "typedef",
                null,
                ["typedef"],
                [],
                [],
                false,
                false,
                [],
                false,
                false,
                false,
                [],
                []));
        var resolver = new TypeResolver([keyFormat]);

        var projection = resolver.Project(
            new ReferenceTypeNode(
                "Exclude",
                "Exclude",
                [
                    new ReferenceTypeNode("KeyFormat", "KeyFormat", []),
                    new LiteralTypeNode("StringLiteral", "\"jwk\""),
                ]),
            "fixture/exclude");

        Assert.Equal("json-value", projection.Transport?.Kind);
        var source = Assert.Single(resolver.SynthesizedTypes).Source;
        Assert.DoesNotContain("Value = \"jwk\"", source);
        Assert.Contains("Value = \"pkcs8\"", source);
        Assert.Contains("Value = \"raw\"", source);
        Assert.Contains("Value = \"spki\"", source);
    }

    [Fact]
    public void Project_QualifiedUserExclude_RemainsDistinct()
    {
        var declaration = new DeclarationModel(
            0,
            "interface",
            "Exclude",
            [],
            [new TypeParameterModel(0, "T", null, null), new TypeParameterModel(1, "U", null, null)],
            [],
            [],
            null,
            [],
            null,
            new DocumentationModel("", [], false),
            new LocationModel("test", new(0, 0, 0), new(0, 0, 0)),
            null,
            false,
            new EventMapModel(false, []),
            []);
        var symbol = new SymbolModel(
            0,
            "Fixture.Exclude",
            0,
            [declaration],
            false,
            new SemanticModel(
                "matched",
                "Fixture.Exclude",
                "definition",
                null,
                ["interface"],
                [],
                [],
                false,
                false,
                [],
                false,
                false,
                false,
                [],
                []));
        var resolver = new TypeResolver([symbol]);

        var projection = resolver.Project(
            new ReferenceTypeNode(
                "Exclude",
                "Fixture.Exclude",
                [new KeywordTypeNode("StringKeyword"), new KeywordTypeNode("NumberKeyword")]),
            "fixture/qualified-exclude");

        Assert.Equal(
            "global::Blazor.DOM.Namespaces.Fixture.IExclude<string, double>",
            projection.CSharpType);
        Assert.Empty(resolver.SynthesizedTypes);
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
    public void Project_EmptyTemplateLiteral_UsesFiniteValueType()
    {
        var resolver = EmptyResolver();
        var node = new TemplateLiteralTypeNode([]);
        var projection = resolver.Project(node, "test/templateLiteral");
        Assert.Equal("finite-template-string", projection.ProviderNote);
        Assert.NotEqual("string", projection.RenderedType);
        Assert.Single(resolver.SynthesizedTypes);
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
