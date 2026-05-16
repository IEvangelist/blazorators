// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for the <see cref="CSharpObject.IsActionParameter"/>
/// predicate. Historically, callback detection relied on the brittle
/// heuristic <c>TypeName.EndsWith("Callback")</c>. That misclassified
/// any DTO whose name happened to end with the substring even if it
/// had real properties (and not a single anonymous call signature).
/// Now classification is driven by <see cref="CSharpObject.IsCallback"/>
/// which is set via shape-based detection in
/// <c>TypeDeclarationParser.IsCallbackTypeDeclaration</c>.
/// </summary>
public class CSharpObjectIsActionParameterTests
{
    [Fact]
    public void IsActionParameter_True_WhenIsCallback()
    {
        var obj = new CSharpObject("PositionCallback", null)
        {
            IsCallback = true,
        };

        Assert.True(obj.IsActionParameter);
    }

    [Fact]
    public void IsActionParameter_False_WhenSuffixedButNotCallback()
    {
        // A user-defined DTO whose name happens to end with "Callback" but
        // is a normal interface (properties, not a call signature) MUST be
        // emitted as a dependent type. Treating it as an action parameter
        // would silently drop it from the generated output.
        var obj = new CSharpObject("PaymentCallback", null)
        {
            IsCallback = false,
        };
        obj.Properties.Add("amount", new CSharpProperty("amount", "number"));
        obj.Properties.Add("currency", new CSharpProperty("currency", "string"));

        Assert.False(obj.IsActionParameter);
    }

    [Fact]
    public void IsActionParameter_False_WhenNeitherCallbackNorSuffixed()
    {
        var obj = new CSharpObject("PositionOptions", null);
        obj.Properties.Add("timeout", new CSharpProperty("timeout", "number"));

        Assert.False(obj.IsActionParameter);
    }

    [Theory]
    [InlineData("BlobCallback")]
    [InlineData("ErrorCallback")]
    [InlineData("FrameRequestCallback")]
    [InlineData("EventListener")]
    [InlineData("VoidFunction")]
    public void IsActionParameter_RespectsIsCallback_RegardlessOfName(string typeName)
    {
        var asCallback = new CSharpObject(typeName, null) { IsCallback = true };
        var asNonCallback = new CSharpObject(typeName, null) { IsCallback = false };

        Assert.True(asCallback.IsActionParameter);
        Assert.False(asNonCallback.IsActionParameter);
    }
}
