// Accounting ledger tests: verifies exact symbol accounting and validation.

using Blazor.DOM.CSharpGenerator.Accounting;
using Blazor.DOM.CSharpGenerator.IR;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class AccountingLedgerTests
{
    private static SymbolModel MakeSymbol(int ordinal, string name, string declKind = "interface")
    {
        var decl = new DeclarationModel(
            Ordinal: 0, Kind: declKind, Name: name,
            Modifiers: [], TypeParameters: [], Heritage: [], Members: [],
            Type: null, Parameters: [], ReturnType: null,
            Documentation: new DocumentationModel("", [], false),
            Location: new LocationModel("test", new(1,1,0), new(1,10,9)),
            VariableKind: null, ConstructorObject: false,
            EventMap: new EventMapModel(false, []),
            NamespaceMembers: []);

        return new SymbolModel(
            Ordinal: ordinal, Name: name, SymbolFlags: 64,
            Declarations: [decl], IsDeclarationMerged: false,
            Semantic: new SemanticModel(
                Status: "matched", WebIdlName: name, BindingKind: "definition",
                WebIdlMemberName: null, Classifications: ["interface"],
                Specifications: [], Exposures: [],
                ExposedOnWindow: false, ExposedOnWorker: false, GlobalNames: [],
                Serializable: false, Transferable: false, SecureContext: false,
                ExtendedAttributes: [], Bindings: []));
    }

    [Fact]
    public void Validate_ReturnsValid_WhenAllSymbolsAccountedFor()
    {
        var ledger = new AccountingLedger();
        var sym1 = MakeSymbol(0, "Alpha");
        var sym2 = MakeSymbol(1, "Beta");
        ledger.RecordProjected(sym1, "Alpha.g.cs");
        ledger.RecordExcluded(sym2, "Worker-only");

        var result = ledger.Validate(expectedCount: 2);
        Assert.True(result.IsValid);
        Assert.Equal(2, result.ActualCount);
        Assert.Empty(result.Duplicates);
    }

    [Fact]
    public void Validate_ReturnsInvalid_WhenCountMismatch()
    {
        var ledger = new AccountingLedger();
        ledger.RecordProjected(MakeSymbol(0, "Alpha"), "Alpha.g.cs");

        var result = ledger.Validate(expectedCount: 3);
        Assert.False(result.IsValid);
        Assert.Equal(1, result.ActualCount);
        Assert.Equal(3, result.ExpectedCount);
    }

    [Fact]
    public void BuildManifest_IncludesAllOutcomeCategories()
    {
        var ledger = new AccountingLedger();
        ledger.RecordProjected(MakeSymbol(0, "Alpha"), "Alpha.g.cs");
        ledger.RecordExcluded(MakeSymbol(1, "Beta"), "Worker-only");
        ledger.RecordDeferred(MakeSymbol(2, "Gamma"), "events", "event map");
        ledger.RecordFailed(MakeSymbol(3, "Delta"), "unsupported type");

        var manifest = ledger.BuildManifest("1.0.0",
            CreateDummyManifest());

        Assert.Equal(1, manifest.Accounting.Projected);
        Assert.Equal(1, manifest.Accounting.Excluded);
        Assert.Equal(1, manifest.Accounting.Deferred);
        Assert.Equal(1, manifest.Accounting.GenerationFailed);
        Assert.Equal(4, manifest.Accounting.TotalSymbols);
    }

    private static ManifestModel CreateDummyManifest() => new(
        SchemaVersion: 1,
        GenerationProfile: new("Window", ["Window"], true),
        Files: new(
            new("typescript-symbols.jsonl", "jsonl", "dummy", 0, new string('0', 64)),
            new("webidl-symbols.jsonl", "jsonl", "dummy", 0, new string('0', 64)),
            new("coverage.json", "json", "dummy", 1, new string('0', 64))),
        Counts: new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        Provenance: new(
            new("test", "0.0.0", null),
            new("typescript", "5.0.0", "MIT", new string('0', 64), []),
            new("@webref/idl", "0.0.0", "MIT"),
            new("webidl2", "0.0.0", "W3C"),
            new("/dev/null", new string('0', 64), 0)));
}
