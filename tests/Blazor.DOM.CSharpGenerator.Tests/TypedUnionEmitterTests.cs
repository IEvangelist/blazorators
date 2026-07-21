using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class TypedUnionEmitterTests
{
    [Fact]
    public void NamedMixedUnion_EmitsDiscriminatorFactoriesAndTypedAccess()
    {
        var blob = MakeSymbol("Blob", "interface", null);
        var alias = MakeSymbol(
            "StringOrBlob",
            "typedef",
            new UnionTypeNode(
            [
                new KeywordTypeNode("StringKeyword"),
                new ReferenceTypeNode("Blob", "Blob", []),
            ]));

        var source = new AliasEmitter(
            new TypeResolver([blob, alias]),
            "1.0.0",
            "Blazor.DOM").Emit(alias);

        Assert.Contains("public enum ArmKind : byte", source);
        Assert.Contains("FromString(string value)", source);
        Assert.Contains("FromBlob(IBlob value)", source);
        Assert.Contains("TryGetBlob([MaybeNullWhen(false)] out IBlob value)", source);
        Assert.Contains("IEquatable<StringOrBlob>", source);
        Assert.DoesNotContain("public object", source);
        Assert.DoesNotContain("implicit operator", source);
    }

    [Fact]
    public void NestedUnion_FlattensInSourceOrderAndDeduplicatesEquivalentArms()
    {
        var alias = MakeSymbol(
            "Nested",
            "typedef",
            new UnionTypeNode(
            [
                new KeywordTypeNode("StringKeyword"),
                new ParenthesizedTypeNode(new UnionTypeNode(
                [
                    new KeywordTypeNode("BooleanKeyword"),
                    new KeywordTypeNode("StringKeyword"),
                ])),
                new KeywordTypeNode("UndefinedKeyword"),
            ]));

        var source = new AliasEmitter(
            new TypeResolver([alias]),
            "1.0.0",
            "Blazor.DOM").Emit(alias);

        Assert.True(source.IndexOf("String = 1", StringComparison.Ordinal)
            < source.IndexOf("Boolean = 2", StringComparison.Ordinal));
        Assert.Contains("Undefined = 3", source);
        Assert.Equal(1, Count(source, "FromString(string value)"));
    }

    [Fact]
    public void NullAndUndefined_AreNotCollapsedTogether()
    {
        var alias = MakeSymbol(
            "MaybeText",
            "typedef",
            new UnionTypeNode(
            [
                new KeywordTypeNode("StringKeyword"),
                new KeywordTypeNode("NullKeyword"),
                new KeywordTypeNode("UndefinedKeyword"),
            ]));

        var source = new AliasEmitter(
            new TypeResolver([alias]),
            "1.0.0",
            "Blazor.DOM").Emit(alias);

        Assert.Contains("FromNull()", source);
        Assert.Contains("FromUndefined()", source);
        Assert.DoesNotContain("Nullable typedef alias", source);
    }

    [Fact]
    public void SameClrArmsWithoutRuntimeDiscriminator_DeferWithArmProvenance()
    {
        var alias = MakeSymbol(
            "TextKinds",
            "typedef",
            new UnionTypeNode(
            [
                new ReferenceTypeNode("DOMString", "DOMString", []),
                new ReferenceTypeNode("USVString", "USVString", []),
            ]));

        var error = Assert.Throws<GenericDeferralException>(() =>
            new AliasEmitter(
                new TypeResolver([alias]),
                "1.0.0",
                "Blazor.DOM").Emit(alias));

        Assert.Equal("typed-union-arm-discriminator", error.Phase);
        Assert.Contains("TextKinds/typeAlias/arm[1]", error.Provenance);
    }

    private static SymbolModel MakeSymbol(
        string name,
        string classification,
        TypeNode? type)
    {
        var kind = classification == "typedef" ? "typeAlias" : "interface";
        var declaration = new DeclarationModel(
            0,
            kind,
            name,
            [],
            [],
            [],
            [],
            type,
            [],
            null,
            new DocumentationModel("", [], false),
            new LocationModel("fixture", new(1, 1, 0), new(1, 2, 1)),
            null,
            false,
            new EventMapModel(false, []),
            []);
        return new SymbolModel(
            0,
            name,
            0,
            [declaration],
            false,
            new SemanticModel(
                "matched",
                name,
                "definition",
                null,
                [classification],
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
    }

    private static int Count(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;
}
