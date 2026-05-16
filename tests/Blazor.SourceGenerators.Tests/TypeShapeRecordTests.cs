// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Types;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression tests that cover the TS <c>Record&lt;K, V&gt;</c>
/// utility type. Real-world DOM precedent:
/// <list type="bullet">
///   <item><description><c>RTCStats.parameterData: Record&lt;string, number&gt;</c></description></item>
///   <item><description><c>PushSubscriptionJSON.keys: Record&lt;string, string&gt;</c></description></item>
///   <item><description><c>HeadersInit</c> alias includes <c>Record&lt;string, string&gt;</c></description></item>
/// </list>
/// Before this fix the parser dropped the raw <c>Record&lt;...&gt;</c>
/// token into the generated C# DTO property type, which is not a
/// valid CLR type. The expected mapping is
/// <c>Dictionary&lt;TKey, TValue&gt;</c> with both type arguments
/// mapped through the primitive map.
/// </summary>
public sealed class TypeShapeRecordTests
{
    [Theory]
    [InlineData("Record<string, number>", "string", "number")]
    [InlineData("Record<string, string>", "string", "string")]
    [InlineData("Record<string, boolean>", "string", "boolean")]
    [InlineData("Record<string, ExportValue>", "string", "ExportValue")]
    [InlineData("Record<number, Foo>", "number", "Foo")]
    public void TryGetRecordTypeArguments_ReturnsTrueForRecordShape(
        string input,
        string expectedKey,
        string expectedValue)
    {
        var matched = TypeShape.TryGetRecordTypeArguments(input, out var key, out var value);

        Assert.True(matched);
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("string[]")]
    [InlineData("Array<string>")]
    [InlineData("ReadonlyArray<number>")]
    [InlineData("Map<string, number>")]
    [InlineData("Promise<string>")]
    [InlineData("RecordX<string, number>")]
    [InlineData("Record<string>")]
    [InlineData("")]
    public void TryGetRecordTypeArguments_ReturnsFalseForNonRecordShape(string input)
    {
        var matched = TypeShape.TryGetRecordTypeArguments(input, out var key, out var value);

        Assert.False(matched);
        Assert.Equal(string.Empty, key);
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void TryGetRecordTypeArguments_HandlesNestedGenericValue()
    {
        var matched = TypeShape.TryGetRecordTypeArguments(
            "Record<string, Array<number>>",
            out var key,
            out var value);

        Assert.True(matched);
        Assert.Equal("string", key);
        Assert.Equal("Array<number>", value);
    }

    [Fact]
    public void TryGetRecordTypeArguments_StripsTrailingNullClause()
    {
        var matched = TypeShape.TryGetRecordTypeArguments(
            "Record<string, number> | null",
            out var key,
            out var value);

        Assert.True(matched);
        Assert.Equal("string", key);
        Assert.Equal("number", value);
    }

    [Fact]
    public void CSharpProperty_MappedTypeName_RendersRecordAsDictionary()
    {
        var property = new CSharpProperty(
            RawName: "parameterData",
            RawTypeName: "Record<string, number>");

        Assert.Equal("Dictionary<string, double>", property.MappedTypeName);
    }

    [Fact]
    public void CSharpProperty_MappedTypeName_RendersStringStringRecordAsDictionary()
    {
        var property = new CSharpProperty(
            RawName: "keys",
            RawTypeName: "Record<string, string>");

        Assert.Equal("Dictionary<string, string>", property.MappedTypeName);
    }

    [Fact]
    public void CSharpProperty_MappedTypeName_RendersNullableRecord()
    {
        var property = new CSharpProperty(
            RawName: "extras",
            RawTypeName: "Record<string, string> | null",
            IsNullable: true);

        Assert.Equal("Dictionary<string, string>", property.MappedTypeName);
    }

    [Fact]
    public void CSharpType_ToParameterString_RendersRecordParameterAsDictionary()
    {
        // TS site: `init?: Record<string, string>`
        // Parser produces a CSharpType with RawName="init",
        // RawTypeName="Record<string, string>", IsNullable=true.
        // Before this fix the emitted parameter was the literal
        // `Record<string,string>? init = null`, which doesn't compile.
        var type = new CSharpType(
            RawName: "init",
            RawTypeName: "Record<string, string>",
            IsNullable: true);

        Assert.Equal("Dictionary<string, string>? init = null", type.ToParameterString());
    }

    [Fact]
    public void CSharpType_ToParameterString_RendersNonNullableRecordParameter()
    {
        var type = new CSharpType(
            RawName: "parameterData",
            RawTypeName: "Record<string, number>");

        Assert.Equal("Dictionary<string, double> parameterData", type.ToParameterString());
    }
}
