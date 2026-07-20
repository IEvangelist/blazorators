// IrLoader tests: hash validation, record count validation, happy path.

using Blazor.DOM.CSharpGenerator.IR;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class IrLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public IrLoaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_ThrowsIrValidationException_WhenManifestMissing()
    {
        var ex = Assert.Throws<IrValidationException>(() => IrLoader.Load(_tempDir));
        Assert.Contains("manifest.json", ex.Message);
    }

    [Fact]
    public void Load_ThrowsIrValidationException_WhenTsHashMismatch()
    {
        // Write a valid-looking manifest with wrong hash
        var tsLine = MakeSymbolLine("TestSym", "enum", "matched");
        var wrongHash = new string('0', 64);
        WriteManifest(tsRecords: 1, tsHash: wrongHash, tsContent: tsLine,
                      webIdlRecords: 0, webIdlContent: "");

        var ex = Assert.Throws<IrValidationException>(() => IrLoader.Load(_tempDir));
        Assert.Contains("SHA-256 mismatch", ex.Message);
    }

    [Fact]
    public void Load_ThrowsIrValidationException_WhenRecordCountMismatch()
    {
        var tsLine = MakeSymbolLine("TestSym", "enum", "matched");
        var tsContent = tsLine + "\n";
        var tsBytes = Encoding.UTF8.GetBytes(tsContent);
        var correctHash = IrLoader.ComputeSha256Hex(tsBytes);
        // Manifest says 2 records but file has 1 (hash is correct so hash check passes)
        WriteManifest(tsRecords: 2, tsHash: correctHash, tsBytes: tsBytes,
                      webIdlRecords: 0, webIdlContent: "");

        var ex = Assert.Throws<IrValidationException>(() => IrLoader.Load(_tempDir));
        Assert.Contains("Record count mismatch", ex.Message);
    }

    [Fact]
    public void Load_Succeeds_WithValidFixture()
    {
        var tsLine = MakeSymbolLine("AlignSetting", "typeAlias", "matched");
        var webIdlLine = MakeWebIdlLine("AlignSetting");
        WriteManifest(1, tsLine + "\n", 1, webIdlLine + "\n");

        var bundle = IrLoader.Load(_tempDir);

        Assert.Single(bundle.TypescriptSymbols);
        Assert.Equal("AlignSetting", bundle.TypescriptSymbols[0].Name);
        Assert.Single(bundle.WebIdlSymbols);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string MakeSymbolLine(string name, string declKind, string status)
    {
        var escaped = name.Replace("\"", "\\\"");
        return
            "{\"ordinal\":0,\"name\":\"" + escaped + "\",\"symbolFlags\":524288," +
            "\"declarations\":[{\"ordinal\":0,\"kind\":\"" + declKind + "\",\"name\":\"" + escaped + "\"," +
            "\"modifiers\":[],\"typeParameters\":[],\"heritage\":[],\"members\":[]," +
            "\"type\":{\"syntaxKind\":\"UnionType\",\"checkerType\":\"" + escaped + "\",\"kind\":\"union\"," +
            "\"types\":[{\"syntaxKind\":\"LiteralType\",\"checkerType\":\"\\\"center\\\"\",\"kind\":\"literal\"," +
            "\"literalKind\":\"StringLiteral\",\"text\":\"\\\"center\\\"\"}]}," +
            "\"parameters\":[],\"returnType\":null," +
            "\"documentation\":{\"text\":\"\",\"tags\":[],\"deprecated\":false}," +
            "\"location\":{\"source\":\"lib.dom.d.ts\",\"start\":{\"line\":1,\"column\":1,\"offset\":0}," +
            "\"end\":{\"line\":1,\"column\":10,\"offset\":9}}," +
            "\"variableKind\":null,\"constructorObject\":false," +
            "\"eventMap\":{\"isEventMap\":false,\"keys\":[]},\"namespaceMembers\":[]}]," +
            "\"isDeclarationMerged\":false," +
            "\"semantic\":{\"status\":\"" + status + "\",\"webIdlName\":\"" + escaped + "\"," +
            "\"bindingKind\":\"definition\",\"webIdlMemberName\":null," +
            "\"classifications\":[\"enum\"],\"specifications\":[\"test\"],\"exposures\":[]," +
            "\"exposedOnWindow\":false,\"exposedOnWorker\":false,\"globalNames\":[]," +
            "\"serializable\":false,\"transferable\":false,\"secureContext\":false," +
            "\"extendedAttributes\":[],\"bindings\":[]}}";
    }

    private static string MakeWebIdlLine(string name)
    {
        var escaped = name.Replace("\"", "\\\"");
        return
            "{\"ordinal\":0,\"name\":\"" + escaped + "\"," +
            "\"classifications\":[\"enum\"],\"specifications\":[\"test\"]," +
            "\"exposures\":[],\"globalNames\":[],\"serializable\":false,\"transferable\":false," +
            "\"secureContext\":false,\"extendedAttributes\":[],\"inheritance\":[]," +
            "\"includedMixins\":[],\"declarations\":[]}";
    }

    private void WriteManifest(int tsRecords, string tsContent, int webIdlRecords, string webIdlContent)
    {
        var tsBytes = Encoding.UTF8.GetBytes(tsContent);
        var tsHash = IrLoader.ComputeSha256Hex(tsBytes);
        WriteManifest(tsRecords, tsHash, tsBytes, webIdlRecords, webIdlContent);
    }

    private void WriteManifest(
        int tsRecords, string tsHash, string tsContent,
        int webIdlRecords, string webIdlContent)
    {
        WriteManifest(tsRecords, tsHash, Encoding.UTF8.GetBytes(tsContent), webIdlRecords, webIdlContent);
    }

    private void WriteManifest(
        int tsRecords, string tsHash, byte[] tsBytes,
        int webIdlRecords, string webIdlContent)
    {
        // Use WriteAllBytes so BOM is never added and the hash is deterministic.
        File.WriteAllBytes(Path.Combine(_tempDir, "typescript-symbols.jsonl"), tsBytes);

        var webIdlBytes = Encoding.UTF8.GetBytes(webIdlContent);
        var webIdlHash = IrLoader.ComputeSha256Hex(webIdlBytes);
        File.WriteAllBytes(Path.Combine(_tempDir, "webidl-symbols.jsonl"), webIdlBytes);

        var covContent = "{}";
        var covBytes = Encoding.UTF8.GetBytes(covContent);
        var covHash = IrLoader.ComputeSha256Hex(covBytes);
        File.WriteAllBytes(Path.Combine(_tempDir, "coverage.json"), covBytes);

        var manifest =
            "{\n" +
            "  \"schemaVersion\": 1,\n" +
            "  \"generationProfile\": { \"name\": \"Window\", \"includedExposures\": [\"Window\"], \"preservesAllExposureMetadata\": true },\n" +
            "  \"files\": {\n" +
            $"    \"typescriptSymbols\": {{ \"path\": \"typescript-symbols.jsonl\", \"format\": \"jsonl\", \"schema\": \"dummy\", \"records\": {tsRecords}, \"sha256\": \"{tsHash}\" }},\n" +
            $"    \"webIdlSymbols\": {{ \"path\": \"webidl-symbols.jsonl\", \"format\": \"jsonl\", \"schema\": \"dummy\", \"records\": {webIdlRecords}, \"sha256\": \"{webIdlHash}\" }},\n" +
            $"    \"coverage\": {{ \"path\": \"coverage.json\", \"format\": \"json\", \"schema\": \"dummy\", \"records\": 1, \"sha256\": \"{covHash}\" }}\n" +
            "  },\n" +
            $"  \"counts\": {{ \"typescriptSymbols\": {tsRecords}, \"typescriptDeclarations\": {tsRecords}, \"typescriptMembers\": 0, \"webIdlSpecifications\": 1, \"webIdlSymbols\": {webIdlRecords}, \"webIdlMembers\": 0, \"webIdlArguments\": 0, \"reconciledSymbols\": 0, \"reconciledWebIdlSymbols\": 0, \"unmatchedTypeScriptSymbols\": 0, \"unmatchedWebIdlSymbols\": 0, \"ambiguousSymbols\": 0, \"ambiguousWebIdlSymbols\": 0, \"unsupportedShapes\": 0 }},\n" +
            "  \"provenance\": { \"generator\": { \"name\": \"test\", \"version\": \"0.0.0\" }, \"typescript\": { \"package\": \"typescript\", \"version\": \"5.0.0\", \"license\": \"Apache-2.0\", \"aggregateSha256\": \"0000000000000000000000000000000000000000000000000000000000000000\", \"inputs\": [] }, \"webref\": { \"name\": \"x\", \"version\": \"0\", \"license\": \"MIT\" }, \"webidl2\": { \"name\": \"y\", \"version\": \"0\", \"license\": \"W3C\" }, \"overrides\": { \"input\": \"none\", \"sha256\": \"0000000000000000000000000000000000000000000000000000000000000000\", \"appliedCount\": 0 } }\n" +
            "}\n";
        File.WriteAllText(Path.Combine(_tempDir, "manifest.json"), manifest, Encoding.UTF8);
    }
}
