using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.IR;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class HostFactoryTransformerTests
{
    [Fact]
    public void Constructor_UsesHostSpecificDispatchWithExactPath()
    {
        var symbol = Symbol();
        var source =
            """
            #nullable enable
            namespace Blazor.DOM;

            public partial interface IWidgetFactory
            {
                IWidget Prototype { get; set; }

                IWidget Create(string name);
            }
            """;

        var server = new HostFactoryTransformer(DomHostKind.Server)
            .Transform(symbol, source, "Widget");
        var wasm = new HostFactoryTransformer(DomHostKind.WebAssembly)
            .Transform(symbol, source, "Widget");

        Assert.Contains("ValueTask<IWidget> CreateAsync(", server.Source);
        Assert.Contains("DomDispatch.ConstructAsync<IWidget>", server.Source);
        Assert.Contains("IWidget Create(string name)", wasm.Source);
        Assert.Contains("WasmDomDispatch.Construct<IWidget>", wasm.Source);
        Assert.Contains("WidgetFactoryDomProxy", server.Source);
        Assert.Equal(
            server.Operations.Select(operation => operation.LogicalIdentity),
            wasm.Operations.Select(operation => operation.LogicalIdentity));
    }

    private static SymbolModel Symbol()
    {
        var prototype = new MemberModel(
            0,
            "property",
            new NameNode("identifier", "prototype"),
            false,
            false,
            false,
            [],
            [],
            new ReferenceTypeNode("Widget", "Widget", []),
            null,
            Documentation(),
            Location());
        var constructor = new MemberModel(
            1,
            "constructSignature",
            null,
            false,
            false,
            false,
            [],
            [
                new ParameterModel(
                    0,
                    "name",
                    false,
                    false,
                    new KeywordTypeNode("StringKeyword"),
                    null,
                    Documentation(),
                    Location())
            ],
            null,
            new ReferenceTypeNode("Widget", "Widget", []),
            Documentation(),
            Location());
        return new SymbolModel(
            0,
            "Widget",
            0,
            [
                new DeclarationModel(
                    0,
                    "globalVariable",
                    "Widget",
                    [],
                    [],
                    [],
                    [],
                    new TypeLiteralTypeNode([prototype, constructor]),
                    [],
                    null,
                    Documentation(),
                    Location(),
                    "var",
                    true,
                    new EventMapModel(false, []),
                    [])
            ],
            false,
            new SemanticModel(
                "matched",
                "Widget",
                "definition",
                null,
                ["interface"],
                [],
                ["Window"],
                true,
                false,
                ["Widget"],
                false,
                false,
                false,
                [],
                []));
    }

    private static DocumentationModel Documentation() => new("", [], false);
    private static LocationModel Location() =>
        new("test", new(1, 1, 0), new(1, 1, 0));
}
