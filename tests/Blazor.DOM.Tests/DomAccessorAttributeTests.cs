using Microsoft.JSInterop;
using Xunit;

namespace Blazor.DOM.Tests;

public sealed class DomAccessorAttributeTests
{
    [Fact]
    public void Constructor_PreservesDirectionalTransportMetadata()
    {
        var attribute = new DomAccessorAttribute(
            "payload",
            DomAccessorOperation.Set,
            DomTransportKind.JsStream,
            "ReadableStream<Uint8Array>")
        {
            Nullable = true,
            Streamable = true,
            StructuredClone = false,
        };

        Assert.Equal("payload", attribute.PropertyName);
        Assert.Equal(DomAccessorOperation.Set, attribute.Operation);
        Assert.Equal(DomTransportKind.JsStream, attribute.TransportKind);
        Assert.Equal("ReadableStream<Uint8Array>", attribute.SourceType);
        Assert.True(attribute.Nullable);
        Assert.True(attribute.Streamable);
        Assert.False(attribute.StructuredClone);
        Assert.Null(attribute.UnsupportedReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsMissingPropertyName(string propertyName)
    {
        Assert.Throws<ArgumentException>(() => new DomAccessorAttribute(
            propertyName,
            DomAccessorOperation.Get,
            DomTransportKind.JsonValue,
            "string"));
    }
}
