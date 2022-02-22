// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class JavaScriptInteropTests : GeneratorBaseUnitTests
{
    public override IEnumerable<ISourceGenerator> SourceGenerators =>
        new[] { new JavaScriptInteropGenerator() };

    public SyntaxTree GetGeneratedTree(string sourceCode)
    {
        var result = GetRunResult(sourceCode);
        return result.GeneratedTrees.Single(x => x.FilePath.Contains("Extensions"));
    }

    [Fact]
    public void Basic()
    {
        // TODO: write test
        _ = @"
using System;
using Microsoft.JSInterop.Attributes;

#pragma warning disable 649

[JavaScriptInterop]
public static partial class 
";
    }
}
