// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Parsers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class GeneratorOptionsTests
{
    [Fact]
    public void Parsers_ReturnsSameInstance_OnRepeatedAccess()
    {
        var options = new GeneratorOptions(SupportsGenerics: false);

        var first = options.Parsers;
        var second = options.Parsers;

        Assert.Same(first, second);
    }

    [Fact]
    public void Parsers_DoesNotGrow_OnRepeatedAccess()
    {
        // Regression: previously the getter re-added a fresh 'TypeDeclarationParser'
        // (reference equality) every access, so the set grew unboundedly whenever
        // 'TypeDeclarationSources' was non-empty.
        var options = new GeneratorOptions(
            SupportsGenerics: false,
            TypeDeclarationSources: ["foo.d.ts", "bar.d.ts"]);

        var initialCount = options.Parsers.Count;

        for (var i = 0; i < 5; i++)
        {
            _ = options.Parsers;
        }

        Assert.Equal(initialCount, options.Parsers.Count);
    }

    [Fact]
    public void Parsers_ReturnsDefaultParser_WhenNoSourcesProvided()
    {
        var options = new GeneratorOptions(SupportsGenerics: false);

        Assert.Contains(TypeDeclarationParser.Default, options.Parsers);
    }
}
