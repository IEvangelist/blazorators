// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class CSharpMethodTests
{
    [Theory]
    // Genuine nullable shapes that TypeScript can produce.
    [InlineData("string | null", true)]
    [InlineData("number | null", true)]
    [InlineData("MyType | null", true)]
    [InlineData("string | number | null", true)]
    [InlineData("null", true)]
    // Non-nullable types whose names *contain* the substring "null".
    // The previous implementation used `Contains("null")`, which
    // incorrectly flagged any of these as nullable and would have
    // emitted `T?` instead of `T` for a generic return type.
    [InlineData("OnBeforeUnloadEventHandlerNonNull", false)]
    [InlineData("OnErrorEventHandlerNonNull", false)]
    [InlineData("XmlNullable", false)]
    [InlineData("MyNullCallback", false)]
    [InlineData("void", false)]
    [InlineData("string", false)]
    public void IsReturnTypeNullable_RecognizesOnlyTypeLevelNullClause(string rawReturnType, bool expected)
    {
        var method = new CSharpMethod(
            RawName: "doIt",
            RawReturnTypeName: rawReturnType,
            ParameterDefinitions: []);

        Assert.Equal(expected, method.IsReturnTypeNullable);
    }
}
