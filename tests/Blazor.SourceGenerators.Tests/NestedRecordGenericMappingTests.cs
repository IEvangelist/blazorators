// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Xunit;
using Xunit.Abstractions;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Phase B11 — nested generic value types inside <c>Record&lt;K, V&gt;</c>.
/// <para>
/// Today, <see cref="CSharpProperty.MappedTypeName"/> and
/// <c>CSharpType.ToParameterString</c> both route the <c>K</c> and
/// <c>V</c> of a <c>Record</c> through <c>TypeMap.PrimitiveTypes</c>
/// only. For shapes like <c>Record&lt;string, number[]&gt;</c> the
/// inner <c>number[]</c> isn't a direct primitive map entry, so the
/// lookup returns the input verbatim and the emitter ends up with the
/// invalid C# spelling <c>Dictionary&lt;string, number[]&gt;</c>
/// (instead of <c>Dictionary&lt;string, double[]&gt;</c>).
/// </para>
/// <para>
/// Fix: route the <c>K</c> and <c>V</c> through the same array+primitive
/// pipeline used at the property root. Also handle inline unions inside
/// the value position by falling back to <c>object</c> (same convention
/// as B8 for whole-alias unions).
/// </para>
/// </summary>
public class NestedRecordGenericMappingTests
{
    private readonly ITestOutputHelper _output;

    public NestedRecordGenericMappingTests(ITestOutputHelper output) => _output = output;

    private CSharpObject? ParseSynthetic(string synthetic)
    {
        var parser = new TypeDeclarationParser(TypeDeclarationReader.Default);
        var obj = parser.ToObject(synthetic);
        if (obj is not null)
        {
            foreach (var p in obj.Properties.Values)
            {
                _output.WriteLine($"  {p.RawName}: raw='{p.RawTypeName}' mapped='{p.MappedTypeName}' nullable={p.IsNullable}");
            }
        }
        return obj;
    }

    [Fact]
    public void RecordWithPrimitiveArrayValue_MapsBothArgs()
    {
        // Record<string, number[]> -> Dictionary<string, double[]>.
        // Today emits `Dictionary<string, number[]>` (invalid C#).
        const string synthetic =
            "interface Bag {\n" +
            "    metrics: Record<string, number[]>;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("metrics", out var metrics));
        Assert.NotNull(metrics);
        Assert.Equal("Dictionary<string, double[]>", metrics!.MappedTypeName);
    }

    [Fact]
    public void RecordWithBooleanArrayValue_MapsBothArgs()
    {
        // Record<string, boolean[]> -> Dictionary<string, bool[]>.
        const string synthetic =
            "interface Flags {\n" +
            "    bits: Record<string, boolean[]>;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("bits", out var bits));
        Assert.NotNull(bits);
        Assert.Equal("Dictionary<string, bool[]>", bits!.MappedTypeName);
    }

    [Fact]
    public void RecordWithReadonlyArrayValue_MapsBothArgs()
    {
        // Record<string, ReadonlyArray<number>> -> Dictionary<string, double[]>.
        const string synthetic =
            "interface ReadonlyBag {\n" +
            "    metrics: Record<string, ReadonlyArray<number>>;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("metrics", out var metrics));
        Assert.NotNull(metrics);
        Assert.Equal("Dictionary<string, double[]>", metrics!.MappedTypeName);
    }

    [Fact]
    public void RecordWithUnionValue_FallsBackToObjectValue()
    {
        // Record<string, string | number> -> Dictionary<string, object>
        // (B8 + B11 convention: any unsupported value position collapses
        // to `object` rather than leaking raw TS into the emitted C#).
        const string synthetic =
            "interface Mixed {\n" +
            "    items: Record<string, string | number>;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("items", out var items));
        Assert.NotNull(items);
        Assert.Equal("Dictionary<string, object>", items!.MappedTypeName);
    }

    [Fact]
    public void RecordWithPrimitiveValue_StaysMapped_Regression()
    {
        // Record<string, number> -> Dictionary<string, double>.
        // This already works today; pinned as a regression guard.
        const string synthetic =
            "interface Counters {\n" +
            "    counts: Record<string, number>;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("counts", out var counts));
        Assert.NotNull(counts);
        Assert.Equal("Dictionary<string, double>", counts!.MappedTypeName);
    }

    [Fact]
    public void RecordWithIdentifierValue_PreservesIdentifier_Regression()
    {
        // Record<string, Element> -> Dictionary<string, Element>.
        // The identifier flows through unchanged. Whether `Element` is
        // resolvable in the consumer's compilation is a separate concern
        // (dependent-type resolution does not currently recurse into
        // Record value positions -- tracked separately).
        const string synthetic =
            "interface Lookup {\n" +
            "    elements: Record<string, Element>;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("elements", out var elements));
        Assert.NotNull(elements);
        Assert.Equal("Dictionary<string, Element>", elements!.MappedTypeName);
    }
}
