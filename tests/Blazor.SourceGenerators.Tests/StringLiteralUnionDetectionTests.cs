// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Expressions;
using Blazor.SourceGenerators.Readers;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Phase B3 slice 1 — detection only. These tests verify that the
/// generator can recognize TypeScript string-literal union aliases
/// (e.g. <c>type DocumentReadyState = "complete" | "interactive" |
/// "loading";</c>) and produce a deterministic list of distinct raw
/// values, plus convert each raw value into a valid C# enum member
/// identifier. Emission and consumer-visible type substitution are
/// intentionally deferred — this slice is purely additive.
/// </summary>
public class StringLiteralUnionDetectionTests
{
    [Theory]
    [InlineData("\"a\" | \"b\" | \"c\"", new[] { "a", "b", "c" })]
    [InlineData("\"complete\" | \"interactive\" | \"loading\"", new[] { "complete", "interactive", "loading" })]
    [InlineData("  \"a\"  |  \"b\"  ", new[] { "a", "b" })]
    [InlineData("\"only\"", new[] { "only" })]
    [InlineData("\"a-b\" | \"c_d\" | \"e.f\"", new[] { "a-b", "c_d", "e.f" })]
    [InlineData("\"a\" | \"b\" | \"a\"", new[] { "a", "b" })] // duplicate dedup
    [InlineData("\"\"", new[] { "" })] // single empty literal is still a literal
    public void TryParse_RecognizesStringLiteralUnion(string body, string[] expected)
    {
        var matched = StringLiteralUnionDetector.TryParse(body, out var members);

        Assert.True(matched);
        Assert.Equal(expected, members);
    }

    [Theory]
    [InlineData("string")]                              // primitive alias
    [InlineData("number")]                              // primitive alias
    [InlineData("A | B | C")]                           // identifier union
    [InlineData("\"a\" | B")]                           // mixed literal + identifier
    [InlineData("A | \"b\"")]                           // mixed identifier + literal
    [InlineData("(x: number) => void")]                 // function alias
    [InlineData("{ x: number }")]                       // object alias
    [InlineData("\"a\" \"b\"")]                         // missing pipe
    [InlineData("\"a\" |")]                             // trailing pipe
    [InlineData("| \"a\"")]                             // leading pipe
    [InlineData("")]                                    // empty body
    [InlineData("   ")]                                 // whitespace-only body
    public void TryParse_RejectsNonStringLiteralUnion(string body)
    {
        var matched = StringLiteralUnionDetector.TryParse(body, out var members);

        Assert.False(matched);
        Assert.Empty(members);
    }

    [Theory]
    [InlineData("copy", "Copy")]
    [InlineData("Copy", "Copy")]
    [InlineData("CamelAlready", "CamelAlready")]
    [InlineData("data-source", "DataSource")]
    [InlineData("data_source", "DataSource")]
    [InlineData("data.source", "DataSource")]
    [InlineData("data source", "DataSource")]
    [InlineData("data/source", "DataSource")]
    [InlineData("dataSource", "DataSource")]
    [InlineData("camelCase", "CamelCase")]
    [InlineData("2d", "_2d")]                       // leading digit → underscore prefix
    [InlineData("3d", "_3d")]
    [InlineData("class", "Class")]                  // C# keyword resolved by PascalCasing
    [InlineData("new", "New")]
    [InlineData("this", "This")]
    [InlineData("true", "True")]
    [InlineData("false", "False")]
    [InlineData("null", "Null")]
    [InlineData("end", "End")]
    [InlineData("start", "Start")]
    [InlineData("a", "A")]
    public void ToEnumMemberName_ProducesExpectedIdentifier(string raw, string expected)
    {
        var actual = StringLiteralUnionDetector.ToEnumMemberName(raw);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]                                // empty
    [InlineData("   ")]                             // whitespace only
    [InlineData("---")]                             // all separators
    [InlineData("_-_-")]                            // all separators (mixed)
    [InlineData(" . / _")]                          // all separators
    public void ToEnumMemberName_ReturnsNullWhenNoIdentifierPossible(string raw)
    {
        var actual = StringLiteralUnionDetector.ToEnumMemberName(raw);

        Assert.Null(actual);
    }

    [Fact]
    public void Reader_TryGetStringLiteralUnion_RecognizesDocumentReadyState()
    {
        var reader = TypeDeclarationReader.Default;

        var matched = reader.TryGetStringLiteralUnion("DocumentReadyState", out var members);

        Assert.True(matched);
        Assert.Contains("complete", members);
        Assert.Contains("interactive", members);
        Assert.Contains("loading", members);
    }

    [Fact]
    public void Reader_TryGetStringLiteralUnion_RecognizesPermissionName()
    {
        var reader = TypeDeclarationReader.Default;

        var matched = reader.TryGetStringLiteralUnion("PermissionName", out var members);

        Assert.True(matched);
        // The Permissions API spec defines at least these well-known
        // names; we assert a few stable members rather than the entire
        // set so the test is resilient to lib.dom.d.ts updates.
        Assert.Contains("geolocation", members);
        Assert.Contains("notifications", members);
    }

    [Fact]
    public void Reader_TryGetStringLiteralUnion_RecognizesInsertPosition()
    {
        var reader = TypeDeclarationReader.Default;

        var matched = reader.TryGetStringLiteralUnion("InsertPosition", out var members);

        Assert.True(matched);
        Assert.Equal(new[] { "beforebegin", "afterbegin", "beforeend", "afterend" }, members);
    }

    [Fact]
    public void Reader_TryGetStringLiteralUnion_RejectsNonStringLiteralAlias()
    {
        var reader = TypeDeclarationReader.Default;

        // `BodyInit` is an identifier-union alias, not a string-literal
        // union, so the detector must refuse to project it.
        var matched = reader.TryGetStringLiteralUnion("BodyInit", out var members);

        Assert.False(matched);
        Assert.Empty(members);
    }

    [Fact]
    public void Reader_TryGetStringLiteralUnion_ReturnsFalseForUnknownAlias()
    {
        var reader = TypeDeclarationReader.Default;

        var matched = reader.TryGetStringLiteralUnion("ThisAliasDoesNotExist", out var members);

        Assert.False(matched);
        Assert.Empty(members);
    }
}
