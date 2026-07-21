namespace Blazor.DOM.CSharpGenerator.Hosts;

public sealed record HostEntryPoint(
    string Name,
    string Symbol,
    string JavaScriptPath);

public sealed record HostCapabilityMetadata(
    string Name,
    string Description,
    IReadOnlyList<string> Features,
    bool SecureContext,
    bool RequiresUserActivation,
    IReadOnlyList<HostEntryPoint> EntryPoints);

public sealed record HostPackageOptions(HostCapabilityMetadata Capability)
{
    public static HostPackageOptions Exhaustive { get; } = new(
        new HostCapabilityMetadata(
            "DOM",
            "Exhaustive browser DOM bindings.",
            [],
            false,
            false,
            [
                new("Window", "Window", "window"),
                new("Document", "Document", "document"),
                new("Navigator", "Navigator", "navigator"),
            ]));
}
