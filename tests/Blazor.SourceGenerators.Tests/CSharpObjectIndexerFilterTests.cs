// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for TypeScript <em>index signatures</em>
/// (<c>[key: string]: T;</c>) appearing on a dependent DTO. The
/// top-level emit path already filtered these via
/// <see cref="CSharpProperty.IsIndexer"/>, but the dependent-DTO emit
/// path (<see cref="CSharpObject.ToClassString"/>) iterated
/// <see cref="CSharpObject.Properties"/> without the same guard. The
/// raw indexer key (e.g. <c>[index: number]</c>) was then emitted as
/// the C# property name, producing uncompilable output for any DTO
/// pulled in transitively from an interface like <c>CSSRuleList</c>
/// or <c>HTMLCollection</c> (both of which declare <c>[index:
/// number]: T;</c>).
/// </summary>
public class CSharpObjectIndexerFilterTests
{
    [Fact]
    public void ToClassString_PropertyDictionaryContainsIndexer_NotEmittedInClass()
    {
        var dto = new CSharpObject("Sample", null)
        {
            Properties =
            {
                ["validProperty"] = new CSharpProperty("validProperty", "string"),
                ["[index: number]"] = new CSharpProperty("[index: number]", "string"),
            },
        };

        var classText = dto.ToClassString();

        Assert.Contains("public string ValidProperty", classText);
        Assert.DoesNotContain("[index:", classText);
        Assert.DoesNotContain("[Index", classText);
    }

    [Fact]
    public void ToClassString_MultipleIndexers_NotEmittedInClass()
    {
        var dto = new CSharpObject("Sample", null)
        {
            Properties =
            {
                ["validProperty"] = new CSharpProperty("validProperty", "string"),
                ["[index: number]"] = new CSharpProperty("[index: number]", "string"),
                ["[property: string]"] = new CSharpProperty(
                    "[property: string]", "string"),
            },
        };

        var classText = dto.ToClassString();

        Assert.Contains("public string ValidProperty", classText);
        Assert.DoesNotContain("[index:", classText);
        Assert.DoesNotContain("[property:", classText);
    }

    [Fact]
    public void ToClassString_DtoWithOnlyIndexers_ProducesEmptyClassBody()
    {
        // Edge case: a DTO with no real members beyond indexers should
        // still emit a syntactically valid (empty-body) class.
        var dto = new CSharpObject("OnlyIndexers", null)
        {
            Properties =
            {
                ["[index: number]"] = new CSharpProperty("[index: number]", "string"),
            },
        };

        var classText = dto.ToClassString();

        Assert.Contains("public class OnlyIndexers", classText);
        Assert.DoesNotContain("[index:", classText);
    }
}
