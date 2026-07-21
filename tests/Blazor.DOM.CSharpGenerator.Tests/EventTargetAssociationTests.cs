using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class EventTargetAssociationTests
{
    [Fact]
    public void Resolve_UsesAuthoritativeConstraintsAndUnionsMergedMaps()
    {
        var firstMap = Map("FirstEvents");
        var secondMap = Map("Namespace.SecondEvents");
        var target = Target(
            "Target",
            [
                Declaration("Target", 0, ListenerPair("Target", "FirstEvents")),
                Declaration("Target", 1, ListenerPair("Target", "Namespace.SecondEvents")),
            ]);
        var symbols = new[] { firstMap, secondMap, target };
        var resolver = new TypeResolver(symbols);

        var association =
            new EventTargetAssociationResolver(resolver).Resolve(target);
        var emitted = new InterfaceEmitter(
            resolver,
            "1.0.0",
            "Blazor.DOM").Emit(target);

        Assert.NotNull(association);
        Assert.Equal(
            ["FirstEvents", "Namespace.SecondEvents"],
            association.EventMaps);
        Assert.Contains("IDomEventTargetProxy", emitted.Source);
        Assert.Contains(
            "[global::Microsoft.JSInterop.DomEventTarget(\"Target\", " +
            "\"FirstEvents\", \"Namespace.SecondEvents\")]",
            emitted.Source);
        Assert.DoesNotContain("DEFERRED", emitted.Source);
        Assert.All(emitted.MemberOutcomes, outcome =>
            Assert.Equal(MemberOutcomeStatus.Projected, outcome.Status));
        Assert.All(
            emitted.OverloadOutcomes ?? [],
            outcome =>
            {
                Assert.Equal(MemberOutcomeStatus.Projected, outcome.Status);
                Assert.All(outcome.ParameterOutcomes, parameter =>
                    Assert.Equal(MemberOutcomeStatus.Projected, parameter.Status));
            });
    }

    [Fact]
    public void Resolve_DoesNotTrustEventMapNameSuffix()
    {
        var fakeMap = Interface("FakeEventMap");
        var target = Target(
            "Target",
            [Declaration("Target", 0, ListenerPair("Target", "FakeEventMap"))]);
        var resolver = new TypeResolver([fakeMap, target]);

        var exception = Assert.Throws<TypeProjectionException>(() =>
            new EventTargetAssociationResolver(resolver).Resolve(target));

        Assert.Contains("does not resolve to an EventMap symbol", exception.Message);
    }

    private static IReadOnlyList<MemberModel> ListenerPair(
        string target,
        string map) =>
        [
            Listener(0, "addEventListener", target, map),
            Listener(1, "removeEventListener", target, map),
        ];

    private static MemberModel Listener(
        int ordinal,
        string operation,
        string target,
        string map)
    {
        var key = new TypeParameterModel(
            0,
            "K",
            new OperatorTypeNode(
                "KeyOfKeyword",
                new ReferenceTypeNode(map, map, [])),
            null);
        var payload = new IndexedAccessTypeNode(
            new ReferenceTypeNode(map, map, []),
            new ReferenceTypeNode("K", "K", []));
        var listener = new FunctionTypeNode(
            [],
            [
                Parameter("this", new ReferenceTypeNode(target, target, [])),
                Parameter("event", payload, ordinal: 1),
            ],
            new KeywordTypeNode("AnyKeyword"));
        var optionsType = operation == "addEventListener"
            ? "AddEventListenerOptions"
            : "EventListenerOptions";
        return new MemberModel(
            ordinal,
            "method",
            new NameNode("identifier", operation),
            false,
            false,
            false,
            [key],
            [
                Parameter("type", new ReferenceTypeNode("K", "K", [])),
                Parameter("listener", listener, ordinal: 1),
                Parameter(
                    "options",
                    new UnionTypeNode(
                    [
                        new KeywordTypeNode("BooleanKeyword"),
                        new ReferenceTypeNode(optionsType, optionsType, []),
                    ]),
                    optional: true,
                    ordinal: 2),
            ],
            null,
            new KeywordTypeNode("VoidKeyword"),
            new DocumentationModel("", [], false),
            Location());
    }

    private static ParameterModel Parameter(
        string name,
        TypeNode type,
        bool optional = false,
        int ordinal = 0) =>
        new(
            ordinal,
            name,
            optional,
            false,
            type,
            null,
            new DocumentationModel("", [], false),
            Location());

    private static SymbolModel Map(string name) =>
        Symbol(
            name,
            [new DeclarationModel(
                0,
                "interface",
                name,
                [],
                [],
                [],
                [],
                null,
                [],
                null,
                new DocumentationModel("", [], false),
                Location(),
                null,
                false,
                new EventMapModel(true, []),
                [])]);

    private static SymbolModel Interface(string name) =>
        Symbol(name, [Declaration(name, 0, [])]);

    private static SymbolModel Target(
        string name,
        IReadOnlyList<DeclarationModel> declarations) =>
        Symbol(name, declarations);

    private static SymbolModel Symbol(
        string name,
        IReadOnlyList<DeclarationModel> declarations) =>
        new(
            0,
            name,
            64,
            declarations,
            declarations.Count > 1,
            new SemanticModel(
                "matched",
                name,
                "definition",
                null,
                ["interface"],
                ["dom"],
                ["Window"],
                true,
                false,
                [],
                false,
                false,
                false,
                [],
                []));

    private static DeclarationModel Declaration(
        string name,
        int ordinal,
        IReadOnlyList<MemberModel> members) =>
        new(
            ordinal,
            "interface",
            name,
            [],
            [],
            [],
            members,
            null,
            [],
            null,
            new DocumentationModel("", [], false),
            Location(),
            null,
            false,
            new EventMapModel(false, []),
            []);

    private static LocationModel Location() =>
        new("fixture.d.ts", new(1, 1, 0), new(1, 2, 1));
}
