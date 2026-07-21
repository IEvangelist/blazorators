using Blazor.DOM.CSharpGenerator.Hosts;
using Blazor.DOM.CSharpGenerator.Output;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class HostApiManifestTests
{
    [Fact]
    public void Compare_AllowsIntentionalHostSignatureDifferences()
    {
        var server = Manifest(
            DomHostKind.Server,
            new HostApiOperation(
                "Window/document:get",
                "Window",
                "property-get",
                "document",
                false,
                "ValueTask<IDocument> GetDocumentAsync(CancellationToken)"));
        var wasm = Manifest(
            DomHostKind.WebAssembly,
            new HostApiOperation(
                "Window/document:get",
                "Window",
                "property-get",
                "document",
                false,
                "IDocument Document { get; }"));

        var parity = HostParityReport.Compare(server, wasm);

        Assert.True(parity.Exact);
        Assert.Empty(parity.UnexplainedDeltas);
    }

    [Fact]
    public void Compare_ReportsMissingLogicalIdentity()
    {
        var server = Manifest(
            DomHostKind.Server,
            new HostApiOperation(
                "Blob/text",
                "Blob",
                "method",
                "text",
                true,
                "ValueTask<string> TextAsync(CancellationToken)"));
        var wasm = Manifest(DomHostKind.WebAssembly);

        var delta = Assert.Single(
            HostParityReport.Compare(server, wasm).UnexplainedDeltas);

        Assert.Equal("Blob/text", delta.LogicalIdentity);
        Assert.Equal("Missing from WebAssembly host.", delta.Reason);
    }

    [Fact]
    public void Validate_RejectsIncompleteSymbolCoverage()
    {
        var manifest = new HostApiManifest(
            1,
            "test",
            DomHostKind.Server,
            SourceSymbolCount: 2,
            SharedSymbols: ["Blob"],
            HostSymbols: [],
            Operations: [],
            GeneratedFiles: []);

        Assert.Throws<InvalidOperationException>(manifest.Validate);
    }

    [Fact]
    public void OutputWriter_RejectsTraversalAndCaseInsensitiveCollisions()
    {
        var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new OutputWriter(output);
            Assert.Throws<ArgumentException>(
                () => writer.Write("IWindow", "", ".."));
            writer.Write("IWindow", "", "Server");
            Assert.Throws<InvalidOperationException>(
                () => writer.Write("iwindow", "", "server"));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Theory]
    [InlineData("Shared/Enums/ReadyState.g.cs")]
    [InlineData("Server/Interfaces/IWindow.g.cs")]
    [InlineData("WebAssembly/Proxies/WindowProxy.g.cs")]
    [InlineData("host-parity.json")]
    public void HostOutputs_AreTransactionallyOwned(string path) =>
        Assert.True(OutputPromotion.IsExhaustiveOwnedPath(path));

    private static HostApiManifest Manifest(
        DomHostKind host,
        params HostApiOperation[] operations) =>
        new(
            1,
            "test",
            host,
            SourceSymbolCount: 1,
            SharedSymbols: ["Shared"],
            HostSymbols: [],
            operations,
            GeneratedFiles: []);
}
