// Naming convention tests: keyword escaping, PascalCase, enum member names, collision resolution.

using Blazor.DOM.CSharpGenerator.Projection;
using Xunit;

namespace Blazor.DOM.CSharpGenerator.Tests;

public sealed class NamingTests
{
    [Theory]
    [InlineData("event", "@event")]  // C# keyword (lowercase)
    [InlineData("object", "@object")] // C# keyword (lowercase)
    [InlineData("string", "@string")] // C# keyword (lowercase)
    [InlineData("int", "@int")]
    [InlineData("HTMLElement", "HTMLElement")]  // already PascalCase
    [InlineData("AbortController", "AbortController")]
    [InlineData("CSS.Hz", "CSS_Hz")]
    public void ToCSharpTypeName_EscapesKeywords_AndHandlesSpecialChars(
        string input, string expected)
    {
        Assert.Equal(expected, Naming.ToCSharpTypeName(input));
    }

    [Theory]
    [InlineData("\"center\"", "Center")]
    [InlineData("\"end\"", "End")]
    [InlineData("\"audio/mpeg\"", "AudioMpeg")]
    [InlineData("\"end-of-line\"", "EndOfLine")]
    [InlineData("\"2d\"", "_2D")]  // starts with digit
    public void ToEnumMemberName_ProducesValidCSharpIdentifiers(string raw, string expected)
    {
        Assert.Equal(expected, Naming.ToEnumMemberName(raw));
    }

    [Fact]
    public void ToEnumMemberName_EmptyString_ReturnsSafeDefault()
    {
        Assert.Equal("_Empty", Naming.ToEnumMemberName(""));
    }

    [Theory]
    [InlineData("onclick", "Onclick")]
    [InlineData("addEventListener", "AddEventListener")]
    [InlineData("id", "Id")]
    [InlineData("innerHTML", "InnerHTML")]
    public void ToCSharpMemberName_ConvertsToIdiomatic(string input, string _)
    {
        // Verify it produces a valid C# name (PascalCase, no dots)
        var result = Naming.ToCSharpMemberName(input);
        Assert.DoesNotContain(".", result);
        Assert.True(char.IsUpper(result[0]) || result[0] == '@');
    }

    [Fact]
    public void ResolveCollisions_AppendsNumericSuffix()
    {
        var names = new[] { "Alpha", "Beta", "Alpha", "Alpha" };
        var resolved = Naming.ResolveCollisions(names);
        Assert.Equal(4, resolved.Count);
        // First occurrence keeps name, subsequent ones get suffix
        Assert.Equal("Alpha", resolved[0]);
        Assert.Equal("Beta", resolved[1]);
        Assert.Equal("Alpha_1", resolved[2]);
        Assert.Equal("Alpha_2", resolved[3]);
    }

    [Fact]
    public void ResolveCollisions_NoDuplicates_Unchanged()
    {
        var names = new[] { "A", "B", "C" };
        var resolved = Naming.ResolveCollisions(names);
        Assert.Equal(names, resolved);
    }
}
