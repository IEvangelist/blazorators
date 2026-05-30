// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Parsers;
using Blazor.SourceGenerators.Readers;
using Blazor.SourceGenerators.Types;
using Xunit;
using Xunit.Abstractions;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Phase B7 — fallback for TypeScript intersection alias shapes
/// (<c>type X = A &amp; B</c>) that the existing alias resolver
/// doesn't recognize.
/// <para>
/// The DOM corpus has exactly one alias intersection:
/// <c>type ElementTagNameMap = HTMLElementTagNameMap &amp; Pick&lt;SVGElementTagNameMap, Exclude&lt;keyof SVGElementTagNameMap, keyof HTMLElementTagNameMap&gt;&gt;;</c>
/// -- the second arm uses TS-only <c>Pick</c>/<c>Exclude</c>/<c>keyof</c>
/// constructs we have no C# projection for, so attempting to flatten
/// the member set would still produce something broken. Fall back to
/// <c>object</c> (<c>object?</c> when the use-site adds a null clause)
/// so the consumer's compile keeps moving instead of CS0246-ing on the
/// raw alias identifier. Mirrors the B8 union-alias fallback.
/// </para>
/// <para>
/// The depth-aware splitter guards against <c>&amp;</c> tokens that
/// appear inside nested generics (e.g. <c>Pick&lt;A &amp; B, C&gt;</c>),
/// arrays, parens, or object types -- only top-level <c>&amp;</c> at
/// depth 0 triggers the fallback.
/// </para>
/// </summary>
public class IntersectionAliasFallbackTests
{
    private readonly ITestOutputHelper _output;

    public IntersectionAliasFallbackTests(ITestOutputHelper output) => _output = output;

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
    public void DomIntersectionAlias_FallsBackToObject()
    {
        // The real DOM alias is
        //   type ElementTagNameMap = HTMLElementTagNameMap & Pick<...>;
        // which is loaded into the default alias map. Referencing it
        // from a synthetic interface should produce `object`, not the
        // raw `ElementTagNameMap` identifier.
        const string synthetic =
            "interface ElementTagMapHolder {\n" +
            "    map: ElementTagNameMap;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("map", out var map));
        Assert.NotNull(map);
        Assert.Equal("object", map!.MappedTypeName);
    }

    [Fact]
    public void DomIntersectionAlias_Nullable_BecomesNullableObject()
    {
        // Use-site nullability must propagate through the alias-
        // fallback path. Mirrors B8's nullable test.
        const string synthetic =
            "interface NullableElementTagMapHolder {\n" +
            "    map?: ElementTagNameMap | null;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("map", out var map));
        Assert.NotNull(map);
        Assert.Equal("object?", map!.MappedTypeName);
    }

    [Fact]
    public void RegressionIdentifierAlias_StillResolvesInterface()
    {
        // `Location` is a plain interface (no intersection in its
        // declaration). After B7 lands it should still flow through
        // the dependent-type resolver and emit as `Location`, not get
        // collapsed to `object`.
        const string synthetic =
            "interface LocationHolder {\n" +
            "    location: Location;\n" +
            "}";

        var obj = ParseSynthetic(synthetic);

        Assert.NotNull(obj);
        Assert.True(obj!.Properties.TryGetValue("location", out var location));
        Assert.NotNull(location);
        Assert.Equal("Location", location!.MappedTypeName);
    }

    [Fact]
    public void RegressionUnionAlias_StillFallsBackToObject()
    {
        // Sanity check that B7's addition didn't shadow B8 -- a union
        // alias must still fall back to `object`.
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
    public void SplitTopLevelIntersectionArms_DepthAware_DoesNotSplitInsidePick()
    {
        // The DOM `ElementTagNameMap` RHS is
        //   HTMLElementTagNameMap & Pick<SVGElementTagNameMap, Exclude<keyof SVGElementTagNameMap, keyof HTMLElementTagNameMap>>
        // which contains exactly one top-level `&`. Nested generics
        // must not contribute to the split count.
        const string raw =
            "HTMLElementTagNameMap & Pick<SVGElementTagNameMap, Exclude<keyof SVGElementTagNameMap, keyof HTMLElementTagNameMap>>";

        var split = TypeShape.TrySplitTopLevelIntersectionArms(raw, out var arms);

        Assert.True(split);
        Assert.Equal(2, arms.Count);
        Assert.Equal("HTMLElementTagNameMap", arms[0]);
        Assert.Equal(
            "Pick<SVGElementTagNameMap, Exclude<keyof SVGElementTagNameMap, keyof HTMLElementTagNameMap>>",
            arms[1]);
    }

    [Fact]
    public void SplitTopLevelIntersectionArms_DoesNotMatchUnionOnly()
    {
        // A pure union must not be detected as an intersection.
        const string raw = "string | number | boolean";

        var split = TypeShape.TrySplitTopLevelIntersectionArms(raw, out var arms);

        Assert.False(split);
        Assert.Empty(arms);
    }

    [Fact]
    public void SplitTopLevelIntersectionArms_HandlesNestedAmpersandInGenerics()
    {
        // A single top-level `&`, but the inner generic arg also has
        // `&`. The depth-aware splitter must only split at depth 0.
        const string raw = "A & Pick<B & C, D>";

        var split = TypeShape.TrySplitTopLevelIntersectionArms(raw, out var arms);

        Assert.True(split);
        Assert.Equal(2, arms.Count);
        Assert.Equal("A", arms[0]);
        Assert.Equal("Pick<B & C, D>", arms[1]);
    }
}
