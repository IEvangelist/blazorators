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

    [Fact]
    public void AnonymousPromiseUnion_ProducesTypedSynthesizedValue()
    {
        var blob = MakeSymbol("Blob", "interface", null);
        var resolver = new TypeResolver([blob]);
        var union = new UnionTypeNode(
        [
            new KeywordTypeNode("StringKeyword")
            {
                Transport = JsonTransport("string"),
            },
            new ReferenceTypeNode("Blob", "Blob", [])
            {
                Transport = ReferenceTransport("Blob"),
            },
        ]);

        var projection = resolver.Project(
            new ReferenceTypeNode("Promise", "Promise", [union])
            {
                Transport = new TransportModel(
                    "unsupported",
                    false,
                    "Promise<string | Blob>",
                    false,
                    false,
                    "Union has incompatible transports."),
            },
            "ClipboardItemData");

        Assert.StartsWith(
            "global::Microsoft.JSInterop.IBrowserPromise<" +
            "global::Blazor.DOM.AdvancedTypes.",
            projection.RenderedType);
        var synthesized = Assert.Single(
            resolver.SynthesizedTypes,
            type => type.Kind == "Union");
        Assert.Contains("FromString(string value)", synthesized.Source);
        Assert.Contains("FromBlob(IBlob value)", synthesized.Source);
        Assert.Contains("IDomUnionValue", synthesized.Source);
        Assert.DoesNotContain("public object", synthesized.Source);
    }

    [Fact]
    public void AnonymousUnion_NestsInArrayAndUsesDeterministicIdentity()
    {
        var union = new UnionTypeNode(
        [
            new KeywordTypeNode("StringKeyword"),
            new KeywordTypeNode("BooleanKeyword"),
        ]);
        var first = new TypeResolver([]).Project(
            new ArrayTypeNode(union),
            "Owner/member[2]/parameter[0]");
        var second = new TypeResolver([]).Project(
            new ArrayTypeNode(union),
            "Owner/member[2]/parameter[0]");

        Assert.Equal(first.CanonicalType, second.CanonicalType);
        Assert.EndsWith("[]", first.RenderedType);
        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes.OwnerItemsStringOrBooleanUnion[]",
            first.RenderedType);
    }

    [Fact]
    public void CompleteBooleanLiteralUnion_CollapsesButNumericLiteralsDoNotWiden()
    {
        var resolver = new TypeResolver([]);
        var boolean = resolver.Project(
            new UnionTypeNode(
            [
                new LiteralTypeNode("TrueLiteral", "true"),
                new LiteralTypeNode("FalseLiteral", "false"),
            ]),
            "Owner/bool");
        var numeric = resolver.Project(
            new UnionTypeNode(
            [
                new LiteralTypeNode("NumericLiteral", "1"),
                new LiteralTypeNode("NumericLiteral", "2"),
            ]),
            "Owner/number");

        Assert.Equal("bool", boolean.RenderedType);
        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes.OwnerNumberOneOrTwoNumericValue",
            numeric.RenderedType);
    }

    [Fact]
    public void GenericAnonymousUnion_DeclaresFactoriesWithoutConversions()
    {
        var resolver = new TypeResolver([]);
        var generic = resolver.CreateGenericDeclaration(
        [
            new TypeParameterModel(0, "T", null, null),
            new TypeParameterModel(1, "U", null, null),
        ],
        "Owner/member");
        var projection = resolver.Project(
            new UnionTypeNode(
            [
                new ReferenceTypeNode("T", null, []),
                new ReferenceTypeNode("U", null, []),
            ]),
            "Owner/member/return",
            generic.Scope);

        Assert.Contains("<T, U>", projection.RenderedType);
        var source = Assert.Single(
            resolver.SynthesizedTypes,
            type => type.Kind == "Union").Source;
        Assert.Contains(
            "struct OwnerMemberResultTOrUUnion<T, U>",
            source);
        Assert.Contains("<T, U>", source);
        Assert.Contains("FromT(T value)", source);
        Assert.Contains("FromU(U value)", source);
        Assert.DoesNotContain("implicit operator", source);
    }

    [Fact]
    public void EquivalentAnonymousUnionShapes_KeepContextualSemanticNames()
    {
        var resolver = new TypeResolver([]);
        var union = new UnionTypeNode(
        [
            new KeywordTypeNode("StringKeyword"),
            new KeywordTypeNode("BooleanKeyword"),
        ]);

        var first = resolver.Project(union, "FirstHost/value");
        var second = resolver.Project(union, "SecondHost/value");

        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes.FirstHostValueStringOrBooleanUnion",
            first.RenderedType);
        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes.SecondHostValueStringOrBooleanUnion",
            second.RenderedType);
        Assert.Equal(2, resolver.SynthesizedTypes.Count);
    }

    [Fact]
    public void ArrayUnionArms_UseElementTypesInsteadOfOrdinals()
    {
        var resolver = new TypeResolver([]);
        var projection = resolver.Project(
            new UnionTypeNode(
            [
                new KeywordTypeNode("StringKeyword"),
                new ArrayTypeNode(new KeywordTypeNode("StringKeyword")),
                new KeywordTypeNode("NumberKeyword"),
                new KeywordTypeNode("NullKeyword"),
                new ArrayTypeNode(new UnionTypeNode(
                [
                    new KeywordTypeNode("NumberKeyword"),
                    new KeywordTypeNode("NullKeyword"),
                ])),
                new KeywordTypeNode("UndefinedKeyword"),
            ]),
            "PropertyIndexedKeyframes/indexSignature/value");

        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes.PropertyIndexedKeyframesIndexValueUnion",
            projection.RenderedType);
        var source = Assert.Single(
            resolver.SynthesizedTypes,
            type => type.Kind == "Union").Source;
        Assert.Contains("StringArray = 2", source);
        Assert.Contains("NullableNumberArray = 5", source);
        Assert.Contains("FromStringArray(string[] value)", source);
        Assert.Contains("FromNullableNumberArray(double?[] value)", source);
        Assert.DoesNotMatch(@"\bArm\d+\b", source);
    }

    [Fact]
    public void SpecialAndSameNamedLiteralArms_RemainSemanticAndDistinct()
    {
        var resolver = new TypeResolver([]);
        var projection = resolver.Project(
            new UnionTypeNode(
            [
                new LiteralTypeNode("StringLiteral", "\"null\""),
                new KeywordTypeNode("NullKeyword"),
                new KeywordTypeNode("NumberKeyword"),
            ]),
            "Owner/value");

        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes.OwnerValueNullStringOrNullOrNumberUnion",
            projection.RenderedType);
        var source = Assert.Single(
            resolver.SynthesizedTypes,
            type => type.Kind == "Union").Source;
        Assert.Contains("NullString = 1", source);
        Assert.Contains("Null = 2", source);
        Assert.Contains("FromNullString(", source);
        Assert.Contains("FromNull()", source);
        Assert.DoesNotMatch(@"\bArm\d+\b", source);
    }

    [Fact]
    public void InterfaceOnlyUnion_EmitsHonestUnclassifiedReferenceState()
    {
        var first = MakeSymbol("First", "interface", null);
        var second = MakeSymbol("Second", "interface", null);
        var resolver = new TypeResolver([first, second]);
        var projection = resolver.Project(
            new UnionTypeNode(
            [
                new ReferenceTypeNode("First", "First", [])
                {
                    Transport = ReferenceTransport("First"),
                },
                new ReferenceTypeNode("Second", "Second", [])
                {
                    Transport = ReferenceTransport("Second"),
                },
            ]),
            "Owner/member");
        var source = Assert.Single(resolver.SynthesizedTypes).Source;

        Assert.NotEqual("object", projection.RenderedType);
        Assert.Contains("UnclassifiedReference", source);
        Assert.Contains("TakeUnclassifiedAsFirst", source);
        Assert.Contains("TakeUnclassifiedAsSecond", source);
        Assert.Contains("FromFirst", source);
        Assert.Contains("FromSecond", source);
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

    private static TransportModel JsonTransport(string source)
        => new("json-value", false, source, false, true, null);

    private static TransportModel ReferenceTransport(string source)
        => new("js-reference", false, source, false, false, null);
}
