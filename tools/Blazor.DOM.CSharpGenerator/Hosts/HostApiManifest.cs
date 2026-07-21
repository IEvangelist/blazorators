namespace Blazor.DOM.CSharpGenerator.Hosts;

using System.Text.Json.Serialization;

public enum DomHostKind
{
    Server,
    WebAssembly,
}

public sealed record HostApiOperation(
    string LogicalIdentity,
    string Symbol,
    string Kind,
    string JavaScriptName,
    bool Promise,
    string HostSignature);

public sealed record HostApiManifest(
    int SchemaVersion,
    string GeneratorVersion,
    DomHostKind Host,
    int SourceSymbolCount,
    IReadOnlyList<string> SharedSymbols,
    IReadOnlyList<string> HostSymbols,
    IReadOnlyList<HostApiOperation> Operations,
    IReadOnlyList<string> GeneratedFiles,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        HostCapabilityMetadata? Capability = null)
{
    public IReadOnlyList<string> CoveredSymbols => SharedSymbols
        .Concat(HostSymbols)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToList();

    public void Validate()
    {
        if (CoveredSymbols.Count != SourceSymbolCount)
        {
            throw new InvalidOperationException(
                $"{Host} host coverage accounts for {CoveredSymbols.Count} of " +
                $"{SourceSymbolCount} source symbols.");
        }

        if (Capability is not null && Capability.EntryPoints.Count == 0)
        {
            throw new InvalidOperationException(
                $"{Host} host capability metadata has no entry points.");
        }

        var duplicateOperation = Operations
            .GroupBy(operation => operation.LogicalIdentity, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOperation is not null)
        {
            throw new InvalidOperationException(
                $"{Host} host emits duplicate logical API identity " +
                $"'{duplicateOperation.Key}'.");
        }

        var duplicateFile = GeneratedFiles
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateFile is not null)
        {
            throw new InvalidOperationException(
                $"{Host} host emits colliding path '{duplicateFile.Key}'.");
        }
    }
}

public sealed record HostParityDelta(
    string LogicalIdentity,
    string Reason,
    HostApiOperation? Server,
    HostApiOperation? WebAssembly);

public sealed record HostParityReport(
    int SchemaVersion,
    int ServerOperationCount,
    int WebAssemblyOperationCount,
    IReadOnlyList<HostParityDelta> UnexplainedDeltas)
{
    public bool Exact => UnexplainedDeltas.Count == 0;

    public static HostParityReport Compare(
        HostApiManifest server,
        HostApiManifest webAssembly)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(webAssembly);
        server.Validate();
        webAssembly.Validate();
        if (server.Host != DomHostKind.Server)
            throw new ArgumentException("Expected a Server host manifest.", nameof(server));
        if (webAssembly.Host != DomHostKind.WebAssembly)
        {
            throw new ArgumentException(
                "Expected a WebAssembly host manifest.",
                nameof(webAssembly));
        }

        var serverOperations = server.Operations.ToDictionary(
            operation => operation.LogicalIdentity,
            StringComparer.Ordinal);
        var wasmOperations = webAssembly.Operations.ToDictionary(
            operation => operation.LogicalIdentity,
            StringComparer.Ordinal);
        var identities = serverOperations.Keys
            .Concat(wasmOperations.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var deltas = new List<HostParityDelta>();

        foreach (var identity in identities)
        {
            serverOperations.TryGetValue(identity, out var serverOperation);
            wasmOperations.TryGetValue(identity, out var wasmOperation);
            string? reason = (serverOperation, wasmOperation) switch
            {
                (null, _) => "Missing from Server host.",
                (_, null) => "Missing from WebAssembly host.",
                _ when !string.Equals(
                    serverOperation.Symbol,
                    wasmOperation.Symbol,
                    StringComparison.Ordinal) => "Symbol identity differs.",
                _ when !string.Equals(
                    serverOperation.Kind,
                    wasmOperation.Kind,
                    StringComparison.Ordinal) => "Operation kind differs.",
                _ when !string.Equals(
                    serverOperation.JavaScriptName,
                    wasmOperation.JavaScriptName,
                    StringComparison.Ordinal) => "JavaScript operation name differs.",
                _ when serverOperation.Promise != wasmOperation.Promise =>
                    "Promise semantics differ.",
                _ => null,
            };
            if (reason is not null)
            {
                deltas.Add(new HostParityDelta(
                    identity,
                    reason,
                    serverOperation,
                    wasmOperation));
            }
        }

        return new HostParityReport(
            SchemaVersion: 1,
            server.Operations.Count,
            webAssembly.Operations.Count,
            deltas);
    }
}
