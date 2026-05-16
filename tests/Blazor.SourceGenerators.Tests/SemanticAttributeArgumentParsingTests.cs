// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// T1.11 regression coverage. The pre-audit attribute argument parser
/// was syntactic only: it called <c>.ToString()</c> on each argument
/// expression and string-replaced quotes. That breaks for:
///
///   - String literals containing escape sequences (the escape is
///     mangled by indiscriminate quote-stripping).
///   - <c>const</c> or <c>nameof(...)</c> argument expressions.
///   - Verbatim strings (<c>@""..."";"</c>).
///
/// These tests pin the *new* semantic-aware behavior - if anyone
/// regresses the parser, we lose support for legitimate C# attribute
/// expressions that consumers may rely on.
/// </summary>
public class SemanticAttributeArgumentParsingTests : GeneratorBaseUnitTests
{
    public override IIncrementalGenerator[] SourceGenerators =>
        [new JavaScriptInteropGenerator()];

    private static (AttributeSyntax Attribute, SemanticModel SemanticModel) GetAttributeAndModel(
        string source,
        string attributeName)
    {
        var compilation = GetCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);
        var attribute = tree.GetRoot()
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .First(a => a.Name.ToString().Contains(attributeName));
        return (attribute, semanticModel);
    }

    [Fact]
    public void StringLiteral_WithEscapedQuote_PreservesValue()
    {
        // Direct unit test against the extension method - the previous
        // implementation called `Replace("\"", "")` on the *raw source text*,
        // which mangled escape sequences inside literal strings. Driving
        // this through the full generator pipeline is impractical because
        // most `string` options (TypeName, Implementation, Url) feed into
        // downstream parsers that reject embedded quotes regardless. So
        // we assert on `GeneratorOptions` directly.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""foo\""bar"",
        Implementation = ""impl"")]
    public partial interface IService { }
}";

        var (attribute, semanticModel) = GetAttributeAndModel(source, "JSAutoInterop");
        var options = attribute.GetGeneratorOptions(supportsGenerics: false, semanticModel);

        Assert.Equal("foo\"bar", options.TypeName);
    }

    [Fact]
    public void ConstReference_ResolvesToConstantValue()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    internal static class Constants
    {
        public const string TypeName = ""Geolocation"";
        public const string Implementation = ""window.navigator.geolocation"";
    }

    [JSAutoInterop(
        TypeName = Constants.TypeName,
        Implementation = Constants.Implementation)]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);

        Assert.Contains(result.GeneratedTrees,
            t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");
        Assert.Contains(result.GeneratedTrees,
            t => Path.GetFileName(t.FilePath) == "IGeolocationService.g.cs");
    }

    [Fact]
    public void NameofExpression_ResolvesToSymbolName()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    internal static class WindowMembers
    {
        public static object Geolocation = null!;
    }

    [JSAutoInterop(
        TypeName = nameof(WindowMembers.Geolocation),
        Implementation = ""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);

        Assert.Contains(result.GeneratedTrees,
            t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");
    }

    [Fact]
    public void VerbatimStringLiteral_IsAccepted()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = @""Geolocation"",
        Implementation = @""window.navigator.geolocation"")]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);

        Assert.Contains(result.GeneratedTrees,
            t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");
    }

    [Fact]
    public void HostingModelEnumMember_ResolvesToCorrectMode()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"",
        HostingModel = BlazorHostingModel.Server)]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);

        var implementation = result.GeneratedTrees
            .First(t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs")
            .ToString();

        Assert.Contains("ValueTask", implementation);
        Assert.Contains("IJSRuntime", implementation);
    }

    [Fact]
    public void BooleanLiteral_OnlyGeneratePureJS_IsHonored()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoInterop(
        TypeName = ""Geolocation"",
        Implementation = ""window.navigator.geolocation"",
        OnlyGeneratePureJS = true)]
    public partial interface IGeolocationService { }
}";
        var result = GetRunResult(source);

        var implementation = Assert.Single(result.GeneratedTrees,
            t => Path.GetFileName(t.FilePath) == "GeolocationService.g.cs");

        var text = implementation.ToString();

        // `OnlyGeneratePureJS=true` must include pure methods (e.g. `clearWatch`,
        // which takes a primitive `long` and returns `void` with no callbacks)
        // but must *exclude* methods that require JS-callback wiring (e.g.
        // `watchPosition`, which accepts a `PositionCallback`). This is the
        // entire purpose of the flag and was previously unverified.
        Assert.Contains("ClearWatch", text);
        Assert.DoesNotContain("WatchPosition", text);
        Assert.DoesNotContain("GetCurrentPosition", text);
    }

    [Fact]
    public void StringArray_NewArrayInitializer_IsParsed()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoGenericInterop(
        TypeName = ""Storage"",
        Implementation = ""window.localStorage"",
        GenericMethodDescriptors = new[] { ""getItem"", ""setItem:value"" })]
    public partial interface ILocalStorageService { }
}";
        var (attribute, semanticModel) = GetAttributeAndModel(source, "JSAutoGenericInterop");
        var options = attribute.GetGeneratorOptions(supportsGenerics: true, semanticModel);

        Assert.NotNull(options.GenericMethodDescriptors);
        Assert.Equal(["getItem", "setItem:value"], options.GenericMethodDescriptors);
    }

    [Fact]
    public void StringArray_ExplicitlyTypedArray_IsParsed()
    {
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoGenericInterop(
        TypeName = ""Storage"",
        Implementation = ""window.localStorage"",
        GenericMethodDescriptors = new string[] { ""getItem"" })]
    public partial interface ILocalStorageService { }
}";
        var (attribute, semanticModel) = GetAttributeAndModel(source, "JSAutoGenericInterop");
        var options = attribute.GetGeneratorOptions(supportsGenerics: true, semanticModel);

        Assert.NotNull(options.GenericMethodDescriptors);
        Assert.Equal(["getItem"], options.GenericMethodDescriptors);
    }

    [Fact]
    public void StringArray_CollectionExpression_IsParsed()
    {
        // Regression: C# 12 collection-expression attribute arguments
        // (`["a", "b"]`) used to fall through to the regex-based fallback,
        // which silently dropped any element whose textual form included a
        // comma or quote. The `CollectionExpressionSyntax` shape is bound to
        // an array type by the semantic model, so we can iterate its
        // `Elements` directly and resolve each via `GetConstantValue`.
        const string source = @"
namespace Microsoft.JSInterop
{
    [JSAutoGenericInterop(
        TypeName = ""Storage"",
        Implementation = ""window.localStorage"",
        GenericMethodDescriptors = [""getItem"", ""setItem:value"", ""key""])]
    public partial interface ILocalStorageService { }
}";
        var (attribute, semanticModel) = GetAttributeAndModel(source, "JSAutoGenericInterop");
        var options = attribute.GetGeneratorOptions(supportsGenerics: true, semanticModel);

        Assert.NotNull(options.GenericMethodDescriptors);
        Assert.Equal(["getItem", "setItem:value", "key"], options.GenericMethodDescriptors);
    }

    [Fact]
    public void StringArray_CollectionExpression_ResolvesConstants()
    {
        // `[Constants.A, Constants.B]` — collection expression containing
        // const references. Each element must route through `ReadString`'s
        // semantic-constant path so const symbols are resolved.
        const string source = @"
namespace Microsoft.JSInterop
{
    internal static class Names
    {
        public const string GetItem = ""getItem"";
        public const string SetItem = ""setItem:value"";
    }

    [JSAutoGenericInterop(
        TypeName = ""Storage"",
        Implementation = ""window.localStorage"",
        GenericMethodDescriptors = [Names.GetItem, Names.SetItem])]
    public partial interface ILocalStorageService { }
}";
        var (attribute, semanticModel) = GetAttributeAndModel(source, "JSAutoGenericInterop");
        var options = attribute.GetGeneratorOptions(supportsGenerics: true, semanticModel);

        Assert.NotNull(options.GenericMethodDescriptors);
        Assert.Equal(["getItem", "setItem:value"], options.GenericMethodDescriptors);
    }
}
