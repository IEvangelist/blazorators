// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Xunit;
using Xunit.Abstractions;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Phase B8 — fallback for TypeScript "union alias" shapes that the
/// existing alias resolver doesn't recognize.
/// <para>
/// Today, <c>TypeDeclarationParser.TryGetPrimitiveType</c> handles
/// three alias shapes:
/// <list type="number">
///   <item>Bare primitive alias (<c>type DOMHighResTimeStamp = number;</c>) → mapped primitive.</item>
///   <item>Pure string-literal union (<c>type DocumentReadyState = "loading" | ...;</c>) → <c>string</c>.</item>
///   <item>Single-token identifier alias → falls through to interface resolution.</item>
/// </list>
/// Anything else (identifier-only unions like
/// <c>type BodyInit = ReadableStream | XMLHttpRequestBodyInit;</c>,
/// mixed identifier+primitive unions like
/// <c>type RequestInfo = Request | string;</c>, and union arms that
/// embed tuples/Records like
/// <c>type HeadersInit = [string, string][] | Record&lt;string, string&gt; | Headers;</c>)
/// leaks the raw TS alias name through to the emitted C#, producing
/// CS0246 in the consumer's compilation.
/// </para>
/// <para>
/// Fix: when an alias RHS contains a top-level <c>|</c> separator and
/// isn't a pure string-literal union, fall back to C# <c>object</c>
/// (<c>object?</c> if the use-site has a <c>| null</c> /
/// <c>| undefined</c> clause). The split must be depth-aware so a
/// generic / tuple arm with embedded <c>|</c> doesn't get torn apart.
/// </para>
/// </summary>
public class UnionAliasFallbackTests
{
    private readonly ITestOutputHelper _output;

    public UnionAliasFallbackTests(ITestOutputHelper output) => _output = output;

    private static (TypeDeclarationParser Parser, TypeDeclarationReader Reader) Make()
    {
        var reader = TypeDeclarationReader.Default;
        return (new TypeDeclarationParser(reader), reader);
    }

    private CSharpObject? ParseSynthetic(string synthetic)
    {
        var (parser, _) = Make();
        var obj = parser.ToObject(synthetic);
        if (obj is not null)
        {
            _output.WriteLine($"TypeName='{obj.TypeName}' Properties={string.Join(", ", obj.Properties.Keys)}");
            foreach (var p in obj.Properties.Values)
            {
                _output.WriteLine($"  {p.RawName}: raw='{p.RawTypeName}' mapped='{p.MappedTypeName}' nullable={p.IsNullable}");
            }
        }
        return obj;
    }

    [Fact]
    public void IdentifierOnlyUnionAlias_FallsBackToObject()
    {
        // BodyInit = ReadableStream | XMLHttpRequestBodyInit (identifier-only union).
        const string synthetic =
            "interface BodyContainer {\n" +
            "    body: BodyInit;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("body", out var body));
        Assert.NotNull(body);
        Assert.Equal("object", body!.MappedTypeName);
    }

    [Fact]
    public void MixedIdentifierAndPrimitiveUnionAlias_FallsBackToObject()
    {
        // RequestInfo = Request | string (identifier + primitive arm).
        const string synthetic =
            "interface RequestHolder {\n" +
            "    info: RequestInfo;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("info", out var info));
        Assert.NotNull(info);
        Assert.Equal("object", info!.MappedTypeName);
    }

    [Fact]
    public void UnionAliasWithTupleAndRecordArms_FallsBackToObject_DepthAwareSplit()
    {
        // HeadersInit = [string, string][] | Record<string, string> | Headers.
        // The tuple `[string, string]` and `Record<string, string>` both
        // contain commas; a naive `|` split would still split correctly,
        // but if we ever try to inspect each arm we must do so with depth
        // tracking. This test asserts the union itself is recognized.
        const string synthetic =
            "interface HeaderHolder {\n" +
            "    headers: HeadersInit;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("headers", out var headers));
        Assert.NotNull(headers);
        Assert.Equal("object", headers!.MappedTypeName);
    }

    [Fact]
    public void UnionAliasUsedNullable_BecomesNullableObject()
    {
        // body?: BodyInit | null is the real shape `RequestInit.body` uses
        // in lib.dom.d.ts. The use-site nullable clause must propagate
        // through the alias-fallback path.
        const string synthetic =
            "interface NullableBodyContainer {\n" +
            "    body?: BodyInit | null;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("body", out var body));
        Assert.NotNull(body);
        Assert.True(body!.IsNullable);
        Assert.Equal("object?", body.MappedTypeName);
    }

    [Fact]
    public void StringLiteralUnionAlias_StillMapsToString_Regression()
    {
        // DocumentReadyState = "complete" | "interactive" | "loading".
        // The existing slice-1 detector keeps this on the `string` path.
        const string synthetic =
            "interface DocumentLike {\n" +
            "    readyState: DocumentReadyState;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("readyState", out var readyState));
        Assert.NotNull(readyState);
        Assert.Equal("string", readyState!.MappedTypeName);
    }

    [Fact]
    public void PrimitiveAlias_StillMapsToPrimitive_Regression()
    {
        // DOMHighResTimeStamp = number — single-token primitive alias.
        const string synthetic =
            "interface TimingLike {\n" +
            "    startTime: DOMHighResTimeStamp;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("startTime", out var startTime));
        Assert.NotNull(startTime);
        Assert.Equal("double", startTime!.MappedTypeName);
    }

    [Fact]
    public void UnknownAlias_StillFallsThroughUnchanged_Regression()
    {
        // No alias means we treat the type as a dependent interface; the
        // mapped name stays the raw identifier (the parser then tries to
        // resolve it as an interface declaration via the reader).
        const string synthetic =
            "interface NodeLike {\n" +
            "    owner: HTMLElement;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("owner", out var owner));
        Assert.NotNull(owner);
        Assert.Equal("HTMLElement", owner!.MappedTypeName);
    }
}
