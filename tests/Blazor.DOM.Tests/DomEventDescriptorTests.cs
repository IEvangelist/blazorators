namespace Blazor.DOM.Tests;

public sealed class DomEventDescriptorTests
{
    [Fact]
    public void Reference_preserves_compile_time_event_metadata()
    {
        var descriptor = DomEventDescriptor<TestEvent>.Reference(
            "ready-state",
            "ExampleEventMap",
            "MessageEvent<string>",
            deprecated: true,
            "ExampleEventMap/decl[0]/member[1]/ready-state");

        Assert.Equal("ready-state", descriptor.Name);
        Assert.Equal("ExampleEventMap", descriptor.EventMap);
        Assert.Equal(DomTransportKind.JsReference, descriptor.Transport.Kind);
        Assert.Equal("MessageEvent<string>", descriptor.Transport.SourceType);
        Assert.True(descriptor.Deprecated);
        Assert.Single(descriptor.Provenance);
    }

    private sealed class TestEvent;
}
