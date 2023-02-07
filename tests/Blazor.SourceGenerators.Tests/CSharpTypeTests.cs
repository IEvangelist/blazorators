// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

public class CSharpTypeTests
{
    [
        Theory,
        InlineData("date", "Date", true, "DateTime? date = null"),
        InlineData("date", "Date", false, "DateTime date"),
        InlineData("count", "number", true, "double? count = null"),
        InlineData("IsBusy", "boolean", false, "bool isBusy")
    ]
    public void ToParametersCorrectlyFormatsString(
        string name, string typeName, bool isNullable, string expected) =>
        Assert.Equal(expected, new CSharpType(name, typeName, isNullable).ToParameterString());
}
