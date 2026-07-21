using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class HostContractTransformerTests
{
    [Fact]
    public void Server_UsesCancellableValueTasksAndExactNames()
    {
        var symbol = CreateSymbol();
        var logical = EmitLogical(symbol);

        var result = new HostContractTransformer(DomHostKind.Server)
            .Transform(symbol, logical);

        Assert.Contains(
            "ValueTask<string> GetTitleAsync(global::System.Threading.CancellationToken",
            result.Source);
        Assert.Contains("GetPropertyAsync<string>", result.Source);
        Assert.Contains("\"title\"", result.Source);
        Assert.Contains("ValueTask<IChild> GetChildAsync(", result.Source);
        Assert.Contains("InvokeAsync<IChild>", result.Source);
        Assert.Contains("ValueTask<string> ReadAsync(", result.Source);
        Assert.Contains("CancellationToken cancellationToken = default", result.Source);
        Assert.Contains("sealed class HostDomProxy", result.Source);
        Assert.Collection(
            result.Operations,
            operation => Assert.Equal("property-get", operation.Kind),
            operation => Assert.Equal("method", operation.Kind),
            operation => Assert.Equal("method", operation.Kind));
    }

    [Fact]
    public void WebAssembly_UsesSyncNonPromiseAndAsyncPromise()
    {
        var symbol = CreateSymbol();
        var logical = EmitLogical(symbol);

        var result = new HostContractTransformer(DomHostKind.WebAssembly)
            .Transform(symbol, logical);

        Assert.Contains("string Title", result.Source);
        Assert.Contains("WasmDomDispatch.GetProperty<string>", result.Source);
        Assert.Contains("IChild GetChild()", result.Source);
        Assert.Contains("WasmDomDispatch.Invoke<IChild>", result.Source);
        Assert.Contains("ValueTask<string> ReadAsync(", result.Source);
        Assert.Contains("DomDispatch.InvokeAsync<string>", result.Source);
        Assert.Contains("sealed class HostDomProxy", result.Source);
    }

    [Fact]
    public void ServerAndWasm_HaveExactLogicalParity()
    {
        var symbol = CreateSymbol();
        var logical = EmitLogical(symbol);
        var serverResult = new HostContractTransformer(DomHostKind.Server)
            .Transform(symbol, logical);
        var wasmResult = new HostContractTransformer(DomHostKind.WebAssembly)
            .Transform(symbol, logical);
        var server = Manifest(DomHostKind.Server, serverResult.Operations);
        var wasm = Manifest(DomHostKind.WebAssembly, wasmResult.Operations);

        Assert.True(HostParityReport.Compare(server, wasm).Exact);
    }

    private static HostApiManifest Manifest(
        DomHostKind host,
        IReadOnlyList<HostApiOperation> operations) =>
        new(1, "test", host, 1, [], ["Host"], operations, []);

    private static string EmitLogical(SymbolModel symbol) =>
        new InterfaceEmitter(
            new TypeResolver(
            [
                symbol,
                EmptyInterface("Child", 1),
            ]),
            "test",
            "Blazor.DOM").Emit(symbol).Source;

    private static SymbolModel CreateSymbol()
    {
        var title = new KeywordTypeNode("StringKeyword")
        {
            CheckerType = "string",
            Transport = Transport("json-value", "string"),
        };
        var child = new ReferenceTypeNode("Child", "Child", [])
        {
            CheckerType = "Child",
            Transport = Transport("js-reference", "Child"),
        };
        var promise = new ReferenceTypeNode(
            "Promise",
            "Promise",
            [title])
        {
            CheckerType = "Promise<string>",
            Transport = Transport("json-value", "string"),
        };
        return new SymbolModel(
            0,
            "Host",
            0,
            [
                new DeclarationModel(
                    0,
                    "interface",
                    "Host",
                    [],
                    [],
                    [],
                    [
                        Member(0, "property", "title", title, null),
                        Member(1, "method", "get-child", null, child),
                        Member(2, "method", "read", null, promise),
                    ],
                    null,
                    [],
                    null,
                    Documentation(),
                    Location(),
                    null,
                    false,
                    new EventMapModel(false, []),
                    [])
            ],
            false,
            Semantic("Host"));
    }

    private static MemberModel Member(
        int ordinal,
        string kind,
        string name,
        TypeNode? type,
        TypeNode? returnType) =>
        new(
            ordinal,
            kind,
            new NameNode("identifier", name),
            false,
            kind == "property",
            false,
            [],
            [],
            type,
            returnType,
            Documentation(),
            Location());

    private static SymbolModel EmptyInterface(string name, int ordinal) =>
        new(
            ordinal,
            name,
            0,
            [
                new DeclarationModel(
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
                    Documentation(),
                    Location(),
                    null,
                    false,
                    new EventMapModel(false, []),
                    [])
            ],
            false,
            Semantic(name));

    private static SemanticModel Semantic(string name) =>
        new(
            "matched",
            name,
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
            []);

    private static TransportModel Transport(string kind, string source) =>
        new(kind, false, source, false, kind == "json-value", null);

    private static DocumentationModel Documentation() => new("", [], false);

    private static LocationModel Location() =>
        new("test", new(1, 1, 0), new(1, 1, 0));
}
