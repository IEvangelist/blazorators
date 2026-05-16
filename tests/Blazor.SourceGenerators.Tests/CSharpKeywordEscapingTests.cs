// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for C# reserved-keyword escaping on TypeScript
/// identifiers (parameter and callback-shim names).
///
/// <para>
/// Real DOM declarations contain parameters whose names collide with
/// C# reserved keywords:
/// <list type="bullet">
///   <item><c>Document.createElementNS(namespace: string, ...)</c></item>
///   <item><c>Document.createDocument(namespace: string | null, ...)</c></item>
///   <item><c>Element.getAttributeNS(namespace: string | null, ...)</c></item>
/// </list>
/// Without escaping, the generated method signature contained a bare
/// <c>namespace</c> token, which is illegal C# (a contextual misnomer:
/// <c>namespace</c> is <i>reserved</i>, not contextual). Any consumer
/// targeting <c>Document</c> or <c>Element</c> broke immediately on
/// the first generated file.
/// </para>
///
/// <para>
/// The fix prepends a <c>@</c> verbatim-identifier prefix when the
/// emitted parameter name collides with a C# reserved keyword.
/// Contextual keywords (<c>var</c>, <c>nameof</c>, etc.) remain valid
/// identifiers and are not escaped.
/// </para>
/// </summary>
public class CSharpKeywordEscapingTests
{
    [Theory]
    [InlineData("namespace", "@namespace")]
    [InlineData("event", "@event")]
    [InlineData("default", "@default")]
    [InlineData("class", "@class")]
    [InlineData("public", "@public")]
    [InlineData("new", "@new")]
    [InlineData("ref", "@ref")]
    [InlineData("out", "@out")]
    [InlineData("in", "@in")]
    [InlineData("base", "@base")]
    [InlineData("this", "@this")]
    [InlineData("null", "@null")]
    [InlineData("true", "@true")]
    [InlineData("false", "@false")]
    public void EscapeCSharpKeyword_ReservedKeywords_PrependsAtSign(string input, string expected)
    {
        Assert.Equal(expected, input.EscapeCSharpKeyword());
    }

    [Theory]
    [InlineData("var")]
    [InlineData("nameof")]
    [InlineData("where")]
    [InlineData("yield")]
    [InlineData("async")]
    [InlineData("await")]
    [InlineData("dynamic")]
    public void EscapeCSharpKeyword_ContextualKeywords_LeftAlone(string input)
    {
        // C# contextual keywords are valid identifiers in non-keyword
        // positions, so they must not be escaped.
        Assert.Equal(input, input.EscapeCSharpKeyword());
    }

    [Theory]
    [InlineData("position")]
    [InlineData("watchId")]
    [InlineData("element")]
    [InlineData("callback")]
    public void EscapeCSharpKeyword_RegularIdentifiers_LeftAlone(string input)
    {
        Assert.Equal(input, input.EscapeCSharpKeyword());
    }

    [Theory]
    [InlineData("@namespace")]
    [InlineData("@event")]
    public void EscapeCSharpKeyword_AlreadyEscaped_LeftAlone(string input)
    {
        // Defensive: if a caller has already escaped the identifier we
        // shouldn't produce `@@namespace`.
        Assert.Equal(input, input.EscapeCSharpKeyword());
    }

    [Theory]
    [InlineData("")]
    public void EscapeCSharpKeyword_EmptyString_ReturnsEmpty(string input)
    {
        Assert.Equal(input, input.EscapeCSharpKeyword());
    }

    [Fact]
    public void ToParameterString_NamespaceParameter_IsEscaped()
    {
        // Mirrors `Document.createElementNS(namespace: string, ...)`.
        var param = new CSharpType("namespace", "string", IsNullable: false);

        // ToParameterString is the canonical emit path for parameters in
        // the public interface / implementation signature. Without
        // escaping the parser-emitted name `namespace` was inserted
        // verbatim, producing an uncompilable method signature.
        Assert.Equal("string @namespace", param.ToParameterString());
    }

    [Fact]
    public void ToParameterString_NullableNamespace_IsEscaped()
    {
        // Mirrors `Document.createDocument(namespace: string | null, ...)`.
        var param = new CSharpType("namespace", "string", IsNullable: true);

        Assert.Equal("string? @namespace = null", param.ToParameterString());
    }

    [Theory]
    [InlineData("event")]
    [InlineData("default")]
    [InlineData("class")]
    public void ToParameterString_OtherReservedNames_AreEscaped(string paramName)
    {
        var param = new CSharpType(paramName, "string", IsNullable: false);

        Assert.Equal($"string @{paramName}", param.ToParameterString());
    }

    [Fact]
    public void ToArgumentString_NamespaceParameter_UsesEscapedName()
    {
        // The argument name flows through `_javaScript.Invoke(..., namespace, ...)`
        // -- with no escaping the same `namespace` token shows up at the call
        // site too, which is equally invalid.
        var param = new CSharpType("namespace", "string", IsNullable: false);

        Assert.Equal("@namespace", param.ToArgumentString());
    }

    [Fact]
    public void ToArgumentString_NamespaceWithJson_PreservesEscapedReceiver()
    {
        // For non-primitive parameters we route through
        // `.ToJson(jsonTypeInfo)` -- the receiver is the parameter name,
        // so the verbatim prefix has to carry through here too. Without
        // it the call becomes `namespace.ToJson(jsonTypeInfo)` which is
        // a parse error.
        var param = new CSharpType("namespace", "SomeOptions", IsNullable: false);

        Assert.Equal("@namespace.ToJson(jsonTypeInfo)", param.ToArgumentString(toJson: true));
    }
}
