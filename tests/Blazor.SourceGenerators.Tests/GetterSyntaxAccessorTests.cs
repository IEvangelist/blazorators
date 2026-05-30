// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Readers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Phase B4 — TypeScript ES2015 accessor syntax. Some interfaces in
/// <c>lib.dom.d.ts</c> (notably <c>Document</c> and <c>Window</c>)
/// expose properties using the <c>get foo(): T;</c> / <c>set foo(v: T);</c>
/// accessor form instead of the shorter <c>foo: T;</c> form. The
/// generator previously dropped both lines silently — both shapes
/// fall through <c>IsMethod</c> (no <c>(</c> immediately after <c>get</c>)
/// and <c>IsProperty</c> (<c>Name</c> capture contains <c>(</c>) — so
/// <c>Document.location</c> and <c>Window.location</c> simply went
/// missing from the parsed type. The setter line stays filtered (Blazor
/// JS-interop properties are read-only by design today), but the
/// getter must be recovered.
/// </summary>
public class GetterSyntaxAccessorTests
{
    [Fact]
    public void Document_ExposesLocationProperty_FromGetterAccessor()
    {
        var reader = TypeDeclarationReader.Default;
        var sut = new global::Blazor.SourceGenerators.Parsers.TypeDeclarationParser(reader);
        Assert.True(reader.TryGetDeclaration("Document", out var text)
            && !string.IsNullOrWhiteSpace(text));

        var actual = sut.ToObject(text!);

        Assert.NotNull(actual);
        Assert.True(
            actual!.Properties.ContainsKey("location"),
            $"Expected 'location' property on Document; got: {string.Join(", ", actual.Properties.Keys)}");
    }

    [Fact]
    public void Document_LocationProperty_IsReadOnly()
    {
        var reader = TypeDeclarationReader.Default;
        var sut = new global::Blazor.SourceGenerators.Parsers.TypeDeclarationParser(reader);
        Assert.True(reader.TryGetDeclaration("Document", out var text)
            && !string.IsNullOrWhiteSpace(text));

        var actual = sut.ToObject(text!);

        Assert.NotNull(actual);
        Assert.True(actual!.Properties.TryGetValue("location", out var location));
        Assert.NotNull(location);
        // Accessors translate to read-only C# properties because the
        // matching `set location(href: ...)` line is intentionally
        // dropped (Blazor JS-interop properties don't round-trip writes
        // safely through the current eval-based property emitter).
        Assert.True(location!.IsReadonly);
    }

    [Fact]
    public void Document_LocationProperty_RawTypeIsLocation()
    {
        var reader = TypeDeclarationReader.Default;
        var sut = new global::Blazor.SourceGenerators.Parsers.TypeDeclarationParser(reader);
        Assert.True(reader.TryGetDeclaration("Document", out var text)
            && !string.IsNullOrWhiteSpace(text));

        var actual = sut.ToObject(text!);

        Assert.NotNull(actual);
        Assert.True(actual!.Properties.TryGetValue("location", out var location));
        Assert.NotNull(location);
        Assert.Equal("Location", location!.RawTypeName);
    }

    [Fact]
    public void Window_ExposesLocationProperty_FromGetterAccessor()
    {
        var reader = TypeDeclarationReader.Default;
        var sut = new global::Blazor.SourceGenerators.Parsers.TypeDeclarationParser(reader);
        Assert.True(reader.TryGetDeclaration("Window", out var text)
            && !string.IsNullOrWhiteSpace(text));

        var actual = sut.ToObject(text!);

        Assert.NotNull(actual);
        Assert.True(
            actual!.Properties.ContainsKey("location"),
            $"Expected 'location' property on Window; got: {string.Join(", ", actual.Properties.Keys)}");
    }

    [Fact]
    public void GetterAccessor_DoesNotProduceMethod()
    {
        var reader = TypeDeclarationReader.Default;
        var sut = new global::Blazor.SourceGenerators.Parsers.TypeDeclarationParser(reader);
        Assert.True(reader.TryGetDeclaration("Document", out var text)
            && !string.IsNullOrWhiteSpace(text));

        var actual = sut.ToObject(text!);

        Assert.NotNull(actual);
        // Critical regression guard: an accessor must not be misparsed
        // as a method named "get" or "location" or anything similar.
        Assert.DoesNotContain("get", actual!.Methods.Keys);
        Assert.DoesNotContain("set", actual.Methods.Keys);
    }
}
