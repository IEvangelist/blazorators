using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class AdvancedTypeProjectionTests
{
    [Fact]
    public void KeyofAlias_UsesFiniteMergedInheritedDomain()
    {
        var symbols = new[]
        {
            Interface("BaseMap", [Property(0, "base-key", String())]),
            Interface(
                "FiniteMap",
                [Property(0, "own", Number())],
                [new HeritageClauseModel(
                    "extends",
                    [new HeritageReferenceTypeNode("BaseMap", "BaseMap", [])])]),
            Alias(
                "FiniteKeys",
                new OperatorTypeNode(
                    "KeyOfKeyword",
                    new ReferenceTypeNode("FiniteMap", "FiniteMap", []))),
        };
        var resolver = new TypeResolver(symbols);

        var source = new AliasEmitter(resolver, "1.0.0", "Blazor.DOM")
            .Emit(symbols[2]);

        Assert.Contains("public enum FiniteKeys", source);
        Assert.Contains("[EnumMember(Value = \"base-key\")]", source);
        Assert.Contains("[EnumMember(Value = \"own\")]", source);
        Assert.DoesNotContain("string Value", source);
    }

    [Fact]
    public void IndexedAccess_ReducesFiniteOptionalAndGenericKeyDomains()
    {
        var map = Interface(
            "FiniteMap",
            [
                Property(0, "first", String()),
                Property(1, "second", String()) with { Optional = true },
            ]);
        var resolver = new TypeResolver([map]);
        var objectType = new ReferenceTypeNode("FiniteMap", "FiniteMap", []);

        var direct = resolver.Project(
            new IndexedAccessTypeNode(
                objectType,
                new LiteralTypeNode("StringLiteral", "\"first\"")),
            "fixture/direct");
        Assert.Equal("string", direct.RenderedType);
        Assert.Equal("statically-reduced-indexed-access", direct.ProviderNote);

        var scope = GenericScope.Create(
            [new TypeParameterModel(
                0,
                "K",
                new OperatorTypeNode("KeyOfKeyword", objectType),
                null)],
            "Fixture");
        var generic = resolver.Project(
            new IndexedAccessTypeNode(
                objectType,
                new ReferenceTypeNode("K", "Fixture.K", [])),
            "fixture/generic",
            scope);
        Assert.Equal("string?", generic.RenderedType);
    }

    [Fact]
    public void IndexedAccessAndOperators_FailClosedWithNamedPhases()
    {
        var dynamicMap = Interface(
            "DynamicMap",
            [new MemberModel(
                0,
                "indexSignature",
                null,
                false,
                true,
                false,
                [],
                [Parameter(0, "key", String())],
                String(),
                null,
                Documentation(),
                Location())]);
        var resolver = new TypeResolver([dynamicMap]);

        var dynamicKey = Assert.Throws<GenericDeferralException>(() =>
            resolver.Project(
                new OperatorTypeNode(
                    "KeyOfKeyword",
                    new ReferenceTypeNode("DynamicMap", "DynamicMap", [])),
                "fixture/keyof"));
        Assert.Equal("dynamic-key-domain", dynamicKey.Phase);

        var unique = Assert.Throws<GenericDeferralException>(() =>
            resolver.Project(
                new OperatorTypeNode("UniqueKeyword", String()),
                "fixture/unique"));
        Assert.Equal("unique-symbol-types", unique.Phase);

        var readOnly = resolver.Project(
            new OperatorTypeNode(
                "ReadonlyKeyword",
                new ArrayTypeNode(String())),
            "fixture/readonly");
        Assert.Equal("IReadOnlyList<string>", readOnly.RenderedType);
    }

    [Fact]
    public void Corpus_KeyofValueTypeMap_EmitsQualifiedEnum()
    {
        var root = FindRepositoryRoot();
        var ir = IrLoader.Load(Path.Combine(root, "data", "Blazor.DOM"));
        var resolver = new TypeResolver(
            ir.TypescriptSymbols,
            EmitterOverridesLoader.Load(Path.Combine(root, "data", "Blazor.DOM")));
        var symbol = Assert.Single(
            ir.TypescriptSymbols,
            symbol => symbol.Name == "WebAssembly.ValueType");

        var source = new AliasEmitter(resolver, "1.0.0", "Blazor.DOM").Emit(symbol);

        Assert.Contains(
            "namespace Blazor.DOM.Namespaces.WebAssembly;",
            source);
        Assert.Contains("public enum ValueType", source);
        Assert.Contains("[EnumMember(Value = \"i32\")]", source);
        Assert.Contains("[EnumMember(Value = \"externref\")]", source);
    }

    [Fact]
    public void Corpus_HeadersInitUnionUsesValidSynthesizedMemberNames()
    {
        var root = FindRepositoryRoot();
        var data = Path.Combine(root, "data", "Blazor.DOM");
        var ir = IrLoader.Load(data);
        var resolver = new TypeResolver(
            ir.TypescriptSymbols,
            EmitterOverridesLoader.Load(data));
        var symbol = Assert.Single(
            ir.TypescriptSymbols,
            symbol => symbol.Name == "HeadersInit");

        var source = new AliasEmitter(resolver, "1.0.0", "Blazor.DOM").Emit(symbol);

        Assert.Contains("FromStringAndStringTupleArray(", source);
        Assert.Contains("FromStringAndStringReadOnlyDictionary(", source);
        Assert.Contains("TryGetHeaders(", source);
        Assert.DoesNotContain(
            "implicit operator HeadersInit(IReadOnlyDictionary",
            source);
        Assert.DoesNotContain("public object", source);
        Assert.DoesNotContain("implicit operator", source);
    }

    [Fact]
    public void Tuple_EmitsJsonArrayConverterWithLabelsOptionalAndRest()
    {
        var resolver = new TypeResolver([]);
        var tuple = new TupleTypeNode(
        [
            new NamedTupleMemberTypeNode(
                "name",
                false,
                false,
                Json(String())),
            new NamedTupleMemberTypeNode(
                "count",
                true,
                false,
                Json(Number())),
            new NamedTupleMemberTypeNode(
                "remaining",
                false,
                true,
                new ArrayTypeNode(Json(String()))
                {
                    Transport = JsonTransport("string[]"),
                }),
        ])
        {
            Transport = JsonTransport("[name: string, count?: number, ...remaining: string[]]"),
        };

        var projection = resolver.Project(tuple, "Fixture/tuple");
        var definition = Assert.Single(resolver.SynthesizedTypes);

        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes." +
            "FixtureTupleNameAndCountAndRemainingTuple",
            projection.RenderedType);
        Assert.True(projection.IsCollection);
        Assert.Contains("[JsonConverter(typeof(", definition.Source);
        Assert.Contains("required string Name { get; init; }", definition.Source);
        Assert.Contains("double? Count { get; init; } = default;", definition.Source);
        Assert.Contains(
            "IReadOnlyList<string>? Remaining { get; init; } = default;",
            definition.Source);
        Assert.Contains("writer.WriteStartArray();", definition.Source);
        Assert.DoesNotContain("ValueTuple", definition.Source);
        Assert.DoesNotContain("object[]", definition.Source);
    }

    [Fact]
    public void Tuple_NonJsonTransportEmitsTypedReferenceView()
    {
        var resolver = new TypeResolver([]);
        var tuple = new TupleTypeNode([String(), Number()])
        {
            Transport = UnsupportedTransport(
                "[string, number]",
                "fixture contains an unsupported transport"),
        };

        var projection = resolver.Project(tuple, "Fixture/unsupported-tuple");
        var definition = Assert.Single(resolver.SynthesizedTypes);

        Assert.Equal("js-reference", projection.Transport?.Kind);
        Assert.Equal(
            "global::Blazor.DOM.AdvancedTypes." +
            "FixtureUnsupportedTupleStringAndNumberReferenceTuple",
            projection.RenderedType);
        Assert.Contains("public partial interface", definition.Source);
        Assert.Contains("string GetItem1();", definition.Source);
        Assert.Contains("double GetItem2();", definition.Source);
        Assert.DoesNotContain("object", definition.Source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TypeLiteral_EmitsDeterministicJsonRecordAndRejectsClrCollisions()
    {
        var resolver = new TypeResolver([]);
        var literal = new TypeLiteralTypeNode(
        [
            Property(0, "display-name", Json(String())) with
            {
                Readonly = true,
                Documentation = new DocumentationModel("Display label.", [], false),
            },
            Property(1, "count", Json(Number())) with { Optional = true },
        ])
        {
            Transport = JsonTransport("{ display-name: string; count?: number }"),
        };

        var first = resolver.Project(literal, "Fixture/options");
        var second = resolver.Project(literal, "Fixture/options");
        var definition = Assert.Single(resolver.SynthesizedTypes);

        Assert.Equal(first.CanonicalType, second.CanonicalType);
        Assert.Equal(
            "FixtureOptionsDisplayNameStringAndCountNumberRecord",
            definition.Name);
        Assert.Contains(
            "public sealed record FixtureOptionsDisplayNameStringAndCountNumberRecord",
            definition.Source);
        Assert.Contains("[JsonPropertyName(\"display-name\")]", definition.Source);
        Assert.Contains("required string DisplayName { get; init; }", definition.Source);
        Assert.Contains("double? Count { get; init; } = default;", definition.Source);
        Assert.Contains("/// Display label.", definition.Source);

        var collision = new TypeLiteralTypeNode(
        [
            Property(0, "item-name", Json(String())),
            Property(1, "item_name", Json(String())),
        ])
        {
            Transport = JsonTransport("{ item-name: string; item_name: string }"),
        };
        var error = Assert.Throws<GenericDeferralException>(
            () => resolver.Project(collision, "Fixture/collision"));
        Assert.Equal("synthesized-identity-collision", error.Phase);
    }

    [Fact]
    public void Intersection_MergesJsonPropertiesAndDefersBrandedOrCollidingArms()
    {
        var resolver = new TypeResolver([]);
        var left = new TypeLiteralTypeNode(
            [Property(0, "name", Json(String()))])
        {
            Transport = JsonTransport("{ name: string }"),
        };
        var right = new TypeLiteralTypeNode(
            [Property(0, "count", Json(Number()))])
        {
            Transport = JsonTransport("{ count: number }"),
        };
        var intersection = new IntersectionTypeNode([left, right])
        {
            Transport = JsonTransport(
                "{ name: string } & { count: number }"),
        };

        resolver.Project(intersection, "Fixture/intersection");
        var definition = Assert.Single(resolver.SynthesizedTypes);
        Assert.Contains("required string Name", definition.Source);
        Assert.Contains("required double Count", definition.Source);

        var branded = new IntersectionTypeNode([String(), left]);
        var brandedError = Assert.Throws<GenericDeferralException>(
            () => resolver.Project(branded, "Fixture/branded"));
        Assert.Equal("branded-intersection", brandedError.Phase);

        var collision = new IntersectionTypeNode(
        [
            left,
            new TypeLiteralTypeNode(
                [Property(0, "name", Json(Number()))])
            {
                Transport = JsonTransport("{ name: number }"),
            },
        ])
        {
            Transport = JsonTransport(
                "{ name: string } & { name: number }"),
        };
        var collisionError = Assert.Throws<GenericDeferralException>(
            () => resolver.Project(collision, "Fixture/intersection-collision"));
        Assert.Equal("intersection-member-collision", collisionError.Phase);
    }

    [Fact]
    public void ReferenceIntersectionAlias_UsesFactoryInsteadOfInterfaceConversion()
    {
        var left = Interface("LeftContract", []);
        var right = Interface("RightContract", []);
        var alias = Alias(
            "CompositeContract",
            new IntersectionTypeNode(
            [
                new ReferenceTypeNode("LeftContract", "LeftContract", []),
                new ReferenceTypeNode("RightContract", "RightContract", []),
            ])
            {
                CheckerType = "LeftContract & RightContract",
            });

        var source = new AliasEmitter(
            new TypeResolver([left, right, alias]),
            "1.0.0",
            "Blazor.DOM").Emit(alias);

        Assert.Contains(
            "public static CompositeContract From(",
            source);
        Assert.DoesNotContain("implicit operator", source);
        Assert.Contains(
            "AdvancedTypes.CompositeContractLeftContractRightContractIntersection",
            source);
    }

    [Fact]
    public void TemplateLiteral_ExpandsFiniteDomainWithoutWidening()
    {
        var resolver = new TypeResolver([]);
        var finite = new TemplateLiteralTypeNode(
            "prefix-",
            [
                new TemplateLiteralSpanModel(
                    new UnionTypeNode(
                    [
                        new LiteralTypeNode("StringLiteral", "\"alpha\""),
                        new LiteralTypeNode("StringLiteral", "\"beta\""),
                    ]),
                    "-suffix"),
            ])
        {
            Transport = JsonTransport(
                "`prefix-${\"alpha\" | \"beta\"}-suffix`"),
        };

        var projection = resolver.Project(finite, "Fixture/template");
        var definition = Assert.Single(resolver.SynthesizedTypes);

        Assert.Equal("finite-template-string", projection.ProviderNote);
        Assert.Equal(ClrTypeKind.Value, projection.Identity.Kind);
        Assert.Equal(
            "FixtureTemplatePrefixAlphaSuffixOrPrefixBetaSuffixString",
            definition.Name);
        Assert.Contains(
            "public enum FixtureTemplatePrefixAlphaSuffixOrPrefixBetaSuffixString",
            definition.Source);
        Assert.Contains(
            "[EnumMember(Value = \"prefix-alpha-suffix\")]",
            definition.Source);
        Assert.Contains(
            "[EnumMember(Value = \"prefix-beta-suffix\")]",
            definition.Source);
        Assert.NotEqual("string", projection.RenderedType);
    }

    [Fact]
    public void TemplateLiteral_MapsUnrestrictedStringAndValidatesPatterns()
    {
        var resolver = new TypeResolver([]);
        var unrestricted = new TemplateLiteralTypeNode(
            "",
            [new TemplateLiteralSpanModel(Json(String()), "")])
        {
            CheckerType = "string",
            Transport = JsonTransport("`${string}`"),
        };
        Assert.Equal(
            "string",
            resolver.Project(unrestricted, "Fixture/unrestricted").RenderedType);

        var constrained = new TemplateLiteralTypeNode(
            "section-",
            [new TemplateLiteralSpanModel(Json(String()), "")])
        {
            CheckerType = "`section-${string}`",
            Transport = JsonTransport("`section-${string}`"),
        };
        var projection = resolver.Project(constrained, "Fixture/constrained");
        var definition = Assert.Single(resolver.SynthesizedTypes);
        Assert.NotEqual("string", projection.RenderedType);
        Assert.Contains("TryParse", definition.Source);
        Assert.Contains("^section-", definition.Source);
        Assert.Contains("JsonConverter", definition.Source);
    }

    [Fact]
    public void FiniteTemplateAlias_ReusesStringEnumPolicy()
    {
        var template = new TemplateLiteralTypeNode(
            "mode-",
            [
                new TemplateLiteralSpanModel(
                    new UnionTypeNode(
                    [
                        new LiteralTypeNode("StringLiteral", "\"one\""),
                        new LiteralTypeNode("StringLiteral", "\"two\""),
                    ]),
                    ""),
            ])
        {
            Transport = JsonTransport("`mode-${\"one\" | \"two\"}`"),
        };
        var symbol = Alias("TemplateMode", template);
        var source = new AliasEmitter(
            new TypeResolver([symbol]),
            "1.0.0",
            "Blazor.DOM").Emit(symbol);

        Assert.Contains("public enum TemplateMode", source);
        Assert.Contains("[EnumMember(Value = \"mode-one\")]", source);
        Assert.Contains("[EnumMember(Value = \"mode-two\")]", source);
        Assert.DoesNotContain("string Value", source);
    }

    [Fact]
    public void ImportQueryParenthesizedAndPrefixUnaryFormsReduceExactly()
    {
        var target = Interface("Module.Target", []);
        var resolver = new TypeResolver([target]);
        var import = new ImportTypeNode(
            new LiteralTypeNode("StringLiteral", "\"module\""),
            "Module.Target",
            [],
            false,
            null)
        {
            Transport = UnsupportedTransport(
                "import(\"module\").Target",
                "Import syntax requires symbol resolution."),
        };

        var imported = resolver.Project(import, "Fixture/import");
        Assert.Equal(
            "global::Blazor.DOM.Namespaces.Module.ITarget",
            imported.RenderedType);
        Assert.Equal("resolved-import-type", imported.ProviderNote);

        var parenthesizedQuery = new ParenthesizedTypeNode(
            new QueryTypeNode(Json(String())));
        Assert.Equal(
            "string",
            resolver.Project(parenthesizedQuery, "Fixture/query").RenderedType);

        var negative = resolver.Project(
            new LiteralTypeNode("PrefixUnaryExpression", "-1"),
            "Fixture/negative");
        Assert.Equal("double", negative.RenderedType);
        Assert.Equal("literal-number:-1", negative.ProviderNote);

        var typeOfImport = import with { IsTypeOf = true };
        var error = Assert.Throws<GenericDeferralException>(
            () => resolver.Project(typeOfImport, "Fixture/typeof-import"));
        Assert.Equal("import-type-factory", error.Phase);
    }

    private static SymbolModel Interface(
        string name,
        IReadOnlyList<MemberModel> members,
        IReadOnlyList<HeritageClauseModel>? heritage = null)
        => new(
            0,
            name,
            0,
            [Declaration("interface", name, members, heritage)],
            false,
            Semantic("interface"));

    private static SymbolModel Alias(string name, TypeNode type)
        => new(
            0,
            name,
            0,
            [Declaration("typeAlias", name, [], type: type)],
            false,
            Semantic("typedef"));

    private static DeclarationModel Declaration(
        string kind,
        string name,
        IReadOnlyList<MemberModel> members,
        IReadOnlyList<HeritageClauseModel>? heritage = null,
        TypeNode? type = null)
        => new(
            0,
            kind,
            name,
            [],
            [],
            heritage ?? [],
            members,
            type,
            [],
            null,
            Documentation(),
            Location(),
            null,
            false,
            new EventMapModel(false, []),
            []);

    private static MemberModel Property(
        int ordinal,
        string name,
        TypeNode type)
        => new(
            ordinal,
            "property",
            new NameNode("identifier", name),
            false,
            false,
            false,
            [],
            [],
            type,
            null,
            Documentation(),
            Location());

    private static ParameterModel Parameter(
        int ordinal,
        string name,
        TypeNode type)
        => new(
            ordinal,
            name,
            false,
            false,
            type,
            null,
            Documentation(),
            Location());

    private static KeywordTypeNode String() => new("StringKeyword");
    private static KeywordTypeNode Number() => new("NumberKeyword");

    private static T Json<T>(T node) where T : TypeNode
        => (T)(node with
        {
            Transport = JsonTransport(node.CheckerType ?? node.Kind),
        });

    private static TransportModel JsonTransport(string sourceType) => new(
        "json-value",
        false,
        sourceType,
        false,
        true,
        null);

    private static TransportModel UnsupportedTransport(
        string sourceType,
        string reason) => new(
        "unsupported",
        false,
        sourceType,
        false,
        false,
        reason);
    private static DocumentationModel Documentation() => new("", [], false);
    private static LocationModel Location() => new(
        "fixture.ts",
        new PositionModel(1, 1, 0),
        new PositionModel(1, 2, 1));

    private static SemanticModel Semantic(string classification) => new(
        "matched",
        null,
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
        []);

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(directory, "blazorators.sln")))
            directory = Directory.GetParent(directory)!.FullName;
        return directory;
    }
}
