// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for TypeScript property lines typed as
/// arrow functions. These appear in two flavors in <c>lib.dom.d.ts</c>:
/// <list type="bullet">
/// <item>
///   <c>on*</c> event handlers, always written with a <c>this:</c>
///   annotation -- e.g.
///   <c>onabort: ((this: AbortSignal, ev: Event) =&gt; any) | null;</c>.
///   The pre-existing <c>name.Contains("this")</c> safety net inside
///   <c>IsProperty</c> happens to catch these.
/// </item>
/// <item>
///   Optional callback-style properties without a <c>this:</c>
///   annotation -- e.g. <c>UnderlyingSource</c>'s
///   <c>pull?: (controller: ReadableStreamDefaultController&lt;R&gt;) =&gt; void | PromiseLike&lt;void&gt;;</c>.
///   These have nothing to filter on; the greedy <c>Name</c> capture
///   in the shared property regex (<c>^(?'Name'.*)\:</c>) walks to the
///   *last* <c>:</c>, producing bogus names like
///   <c>"pull?: (controller"</c> and types like
///   <c>"ReadableStreamDefaultController&lt;R&gt;) =&gt; void | PromiseLike&lt;void&gt;"</c>.
///   That bogus property used to enter the emitted DTO and yield
///   uncompilable C# (the field name is not a legal identifier).
/// </item>
/// </list>
///
/// <para>
/// The fix is a parser-level filter: property lines whose captured
/// <c>Type</c> contains <c>=&gt;</c> (or whose captured <c>Name</c>
/// contains <c>(</c>, indicating the regex walked past a colon inside
/// a parameter list) are skipped. Blazor cannot currently round-trip
/// callback-typed property handlers through JS-interop anyway -- they
/// would need <c>DotNetObjectReference</c> wiring that the generator
/// does not produce for properties.
/// </para>
/// </summary>
public class ArrowFunctionPropertyFilterTests
{
    [Fact]
    public void Property_ArrowFunctionTyped_NotEmittedAsField()
    {
        // `UnderlyingSource`-style: arrow-function properties WITHOUT
        // a `this:` annotation. The pre-existing "name contains 'this'"
        // bail-out doesn't catch these, so this surfaces the real bug.
        var dts = @"
interface UnderlyingSource {
    readonly type: string;
    pull?: (controller: ReadableStreamDefaultController) => void;
    start?: (controller: ReadableStreamDefaultController) => any;
}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);

        var result = parser.ParseTargetType("UnderlyingSource");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var topLevel = result.Value!;
        var propertyNames = topLevel.Properties!.Select(p => p.RawName).ToList();

        Assert.Contains("type", propertyNames);
        Assert.DoesNotContain(propertyNames, n => n.Contains("(") || n.Contains(":") || n.Contains("=>"));
    }

    [Fact]
    public void Property_ArrowFunctionTyped_NotEmittedAsDependentDtoField()
    {
        // Same expectation, but for the dependent-type emission path
        // (`ToObject`). Without the filter, a dependent DTO discovered
        // through some other path would carry these bogus members.
        var dts = @"
interface Holder {
    source: UnderlyingSource;
}
interface UnderlyingSource {
    readonly type: string;
    pull?: (controller: ReadableStreamDefaultController) => void;
    start?: (controller: ReadableStreamDefaultController) => any;
}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);

        var result = parser.ParseTargetType("Holder");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var dependent = Assert.Contains("UnderlyingSource", result.Value!.DependentTypes);
        var propertyNames = dependent.Properties.Keys.ToList();

        Assert.Contains("type", propertyNames);
        Assert.DoesNotContain(propertyNames, n => n.Contains("(") || n.Contains(":") || n.Contains("=>"));
    }

    [Fact]
    public void Property_OnEventHandler_NotEmitted()
    {
        // The other flavor: `on*` properties with `this:` annotations.
        // Already filtered by the existing `Contains("this")` safety
        // net in IsProperty, but pin the behavior to prevent regression
        // if that bail-out is ever removed in favor of the new filter.
        var dts = @"
interface AbortSignal {
    readonly aborted: boolean;
    onabort: ((this: AbortSignal, ev: Event) => any) | null;
}";
        var reader = new TypeDeclarationReader(dts);
        var parser = new TypeDeclarationParser(reader);

        var result = parser.ParseTargetType("AbortSignal");

        Assert.Equal(ParserResultStatus.SuccessfullyParsed, result.Status);
        var topLevel = result.Value!;
        var propertyNames = topLevel.Properties!.Select(p => p.RawName).ToList();

        Assert.Contains("aborted", propertyNames);
        Assert.DoesNotContain(propertyNames, n => n.Contains("(") || n.Contains(":") || n.Contains("=>"));
    }
}
