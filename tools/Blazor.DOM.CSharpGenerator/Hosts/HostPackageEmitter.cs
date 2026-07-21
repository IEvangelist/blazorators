using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Hosts;

public sealed record HostPackageGenerationResult(
    HostApiManifest Server,
    HostApiManifest WebAssembly,
    HostParityReport Parity);

public static class HostPackageEmitter
{
    public static HostPackageGenerationResult Emit(
        IrBundle ir,
        OutputWriter writer,
        IReadOnlyDictionary<string, EmitterOverrideEntry> overrides,
        TypeResolver? sharedResolver = null,
        DeclarationRoutingPlan? sharedRouting = null,
        InterfaceEmitter? sharedInterfaceEmitter = null)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(overrides);

        var routing = sharedRouting
            ?? DeclarationRouter.Create(ir.TypescriptSymbols, overrides);
        var resolver = sharedResolver ?? new TypeResolver(
            ir.TypescriptSymbols,
            overrides,
            GenerationPipeline.GeneratedNamespace);
        var logicalEmitter = sharedInterfaceEmitter ?? new InterfaceEmitter(
            resolver,
            GenerationPipeline.GeneratorVersion,
            GenerationPipeline.GeneratedNamespace,
            routing);
        var serverTransformer = new HostContractTransformer(DomHostKind.Server);
        var wasmTransformer = new HostContractTransformer(DomHostKind.WebAssembly);
        var serverOperations = new List<HostApiOperation>();
        var wasmOperations = new List<HostApiOperation>();
        var serverFiles = new List<string>();
        var wasmFiles = new List<string>();
        var hostSymbols = new List<string>();

        foreach (var route in routing.Symbols
            .OrderBy(route => route.Symbol.Ordinal))
        {
            if (route.PrimaryRoute != DeclarationRouteKind.Interface
                || route.Declarations.Any(declaration =>
                    declaration.Declaration.EventMap.IsEventMap)
                || route.Symbol.Semantic.ExposedOnWorker
                    && !route.Symbol.Semantic.ExposedOnWindow
                    && route.Symbol.Semantic.Exposures.Count > 0)
            {
                continue;
            }

            var logical = logicalEmitter.Emit(route.Symbol);
            var server = serverTransformer.Transform(route.Symbol, logical.Source);
            var wasm = wasmTransformer.Transform(route.Symbol, logical.Source);
            var relativeDirectory = Naming.ToOutputSubdirectory(
                "Interfaces",
                route.Symbol.Name);
            var fileStem =
                $"I{Naming.ToCSharpSimpleTypeName(route.Symbol.Name)}";
            serverFiles.Add(writer.Write(
                fileStem,
                server.Source,
                Path.Combine("Server", relativeDirectory)));
            wasmFiles.Add(writer.Write(
                fileStem,
                wasm.Source,
                Path.Combine("WebAssembly", relativeDirectory)));
            serverOperations.AddRange(server.Operations);
            wasmOperations.AddRange(wasm.Operations);
            hostSymbols.Add(route.Symbol.Name);
        }

        var hostSymbolSet = hostSymbols.ToHashSet(StringComparer.Ordinal);
        var sharedSymbols = ir.TypescriptSymbols
            .Select(symbol => symbol.Name)
            .Where(symbol => !hostSymbolSet.Contains(symbol))
            .Order(StringComparer.Ordinal)
            .ToList();
        hostSymbols.Sort(StringComparer.Ordinal);

        var serverManifest = new HostApiManifest(
            SchemaVersion: 1,
            GenerationPipeline.GeneratorVersion,
            DomHostKind.Server,
            ir.TypescriptSymbols.Count,
            sharedSymbols,
            hostSymbols,
            serverOperations
                .OrderBy(operation => operation.LogicalIdentity, StringComparer.Ordinal)
                .ToList(),
            serverFiles.Order(StringComparer.Ordinal).ToList());
        var wasmManifest = new HostApiManifest(
            SchemaVersion: 1,
            GenerationPipeline.GeneratorVersion,
            DomHostKind.WebAssembly,
            ir.TypescriptSymbols.Count,
            sharedSymbols,
            hostSymbols,
            wasmOperations
                .OrderBy(operation => operation.LogicalIdentity, StringComparer.Ordinal)
                .ToList(),
            wasmFiles.Order(StringComparer.Ordinal).ToList());
        serverManifest.Validate();
        wasmManifest.Validate();
        var parity = HostParityReport.Compare(serverManifest, wasmManifest);
        if (!parity.Exact)
        {
            throw new InvalidOperationException(
                $"Host API parity failed with {parity.UnexplainedDeltas.Count} " +
                "unexplained delta(s).");
        }

        writer.WriteManifest(serverManifest, Path.Combine("Server", "host-manifest.json"));
        writer.WriteManifest(wasmManifest, Path.Combine("WebAssembly", "host-manifest.json"));
        writer.WriteManifest(parity, "host-parity.json");
        return new HostPackageGenerationResult(serverManifest, wasmManifest, parity);
    }
}
