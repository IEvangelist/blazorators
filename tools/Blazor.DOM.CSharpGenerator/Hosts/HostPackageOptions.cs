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
    IReadOnlyList<HostEntryPoint> EntryPoints,
    IReadOnlyList<string>? Permissions = null);

public sealed record HostPackageOptions(
    HostCapabilityMetadata Capability,
    bool EmitCapabilityFacade = true)
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
            ]),
        EmitCapabilityFacade: false);
}
