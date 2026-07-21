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
