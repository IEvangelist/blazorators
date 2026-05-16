// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;
using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for <c>SourceBuilder.AppendTripleSlashMethodComments</c>
/// when extrapolating per-parameter <c>&lt;param&gt;</c> nodes for a
/// callback parameter.
///
/// <para>
/// The previous implementation derived the rendered
/// <c>System.Action{...}</c> generic argument list from the callback's
/// <c>DependentTypes.Keys</c>. That collection only contains the
/// <i>custom</i> types pulled into the dependency graph; TS primitives
/// (which never go through the declaration reader) and array element
/// shapes (which the graph flattens to the bare element key) were
/// silently dropped. The rendered XML doc then disagreed with the
/// emitted delegate signature:
/// </para>
///
/// <list type="bullet">
///   <item>
///     <c>FrameRequestCallback: (time: number): void;</c> -- field is
///     <c>Action&lt;double&gt;? _onCallback;</c>, doc said
///     <c>System.Action{}</c> (empty generic args).
///   </item>
///   <item>
///     <c>IntersectionObserverCallback: (entries: IntersectionObserverEntry[],
///     observer: IntersectionObserver)</c> -- field is
///     <c>Action&lt;IntersectionObserverEntry[], IntersectionObserver&gt;</c>,
///     doc said <c>System.Action{IntersectionObserverEntry, IntersectionObserver}</c>
///     (lost the `[]`).
///   </item>
/// </list>
///
/// <para>
/// The fix routes through <c>MappedActionTypeArguments</c>, the same
/// helper used by the field/shim emit paths, so the rendered doc
/// always agrees with the emitted delegate. The fix also addressed an
/// unbalanced quote (<c>{...}}\"</c>) in the original format string.
/// </para>
/// </summary>
public class SourceBuilderCallbackDocCommentTests
{
    private static SourceBuilder CreateBuilder() =>
        new(new GeneratorOptions(
            SupportsGenerics: false,
            TypeName: "Window",
            Implementation: "window"));

    private static CSharpMethod BuildMethodWith(CSharpAction action, string callbackParamName = "callback")
    {
        var callbackParam = new CSharpType(
            RawName: callbackParamName,
            RawTypeName: "CallbackType",
            IsNullable: false,
            ActionDeclaration: action);

        return new CSharpMethod(
            RawName: "DoIt",
            RawReturnTypeName: "void",
            ParameterDefinitions: [callbackParam],
            JavaScriptMethodDependency: null);
    }

    [Fact]
    public void AppendTripleSlashMethodComments_PrimitiveOnlyCallback_RendersMappedPrimitive()
    {
        // TS: `interface FrameRequestCallback { (time: number): void; }`
        // -- the doc should render `System.Action{double}`, not the
        // previous empty `System.Action{}` (which suggested a
        // zero-parameter delegate and disagreed with the emitted
        // `Action<double>?` field).
        var action = new CSharpAction(
            RawName: "FrameRequestCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions:
            [
                new CSharpType(RawName: "time", RawTypeName: "number")
            ]);

        var builder = CreateBuilder();
        builder.AppendTripleSlashMethodComments(
            BuildMethodWith(action), extrapolateParameters: true);

        var output = builder.ToSourceCodeString();

        Assert.Contains("<c>System.Action{double}</c>", output);
        Assert.DoesNotContain("System.Action{}", output);
    }

    [Fact]
    public void AppendTripleSlashMethodComments_PrimitiveArrayCallback_PreservesArraySuffix()
    {
        // TS: `interface PrimitiveBatchCallback { (values: number[]): void; }`
        // -- the doc should render `System.Action{double[]}`. The
        // previous code resolved `DependentTypes.Keys` to the empty
        // set (primitive element type) and dropped the array shape
        // entirely.
        var action = new CSharpAction(
            RawName: "PrimitiveBatchCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions:
            [
                new CSharpType(RawName: "values", RawTypeName: "number[]")
            ]);

        var builder = CreateBuilder();
        builder.AppendTripleSlashMethodComments(
            BuildMethodWith(action), extrapolateParameters: true);

        var output = builder.ToSourceCodeString();

        Assert.Contains("<c>System.Action{double[]}</c>", output);
    }

    [Fact]
    public void AppendTripleSlashMethodComments_MixedCustomAndPrimitive_OrdersParametersCorrectly()
    {
        // TS: `interface MixedCallback { (count: number, observer: Observer): void; }`
        // -- the doc must preserve parameter order so `<c>System.Action{...}</c>`
        // matches the emitted delegate's generic argument order. The
        // previous code drove from `DependentTypes.Keys` which is an
        // unordered set; the rendered doc dropped the primitive and
        // emitted `System.Action{Observer}`.
        var action = new CSharpAction(
            RawName: "MixedCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions:
            [
                new CSharpType(RawName: "count", RawTypeName: "number"),
                new CSharpType(RawName: "observer", RawTypeName: "Observer")
            ]);

        var builder = CreateBuilder();
        builder.AppendTripleSlashMethodComments(
            BuildMethodWith(action), extrapolateParameters: true);

        var output = builder.ToSourceCodeString();

        Assert.Contains("<c>System.Action{double, Observer}</c>", output);
    }

    [Fact]
    public void AppendTripleSlashMethodComments_ZeroParameterCallback_EmitsNonGenericAction()
    {
        // TS: `interface VoidFunction { (): void; }` -- emitted field
        // is `Action?` (non-generic), so the doc should mirror that
        // with `System.Action` (no braces).
        var action = new CSharpAction(
            RawName: "VoidFunction",
            RawReturnTypeName: "void",
            ParameterDefinitions: []);

        var builder = CreateBuilder();
        builder.AppendTripleSlashMethodComments(
            BuildMethodWith(action), extrapolateParameters: true);

        var output = builder.ToSourceCodeString();

        Assert.Contains("<c>System.Action</c>", output);
        Assert.DoesNotContain("System.Action{}", output);
        Assert.DoesNotContain("System.Action{ }", output);
    }

    [Fact]
    public void AppendTripleSlashMethodComments_CallbackParam_DoesNotEmitStrayQuote()
    {
        // The pre-fix format string emitted `{...}}\"</c>` -- an
        // unbalanced double-quote leaked into the rendered XML doc
        // and made the comment difficult to read in tooltips. Assert
        // a count of `<c>` vs `</c>` to detect any future regression
        // of the same shape.
        var action = new CSharpAction(
            RawName: "FrameRequestCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions:
            [
                new CSharpType(RawName: "time", RawTypeName: "number")
            ]);

        var builder = CreateBuilder();
        builder.AppendTripleSlashMethodComments(
            BuildMethodWith(action), extrapolateParameters: true);

        var output = builder.ToSourceCodeString();

        var openCount = System.Text.RegularExpressions.Regex.Matches(output, "<c>").Count;
        var closeCount = System.Text.RegularExpressions.Regex.Matches(output, "</c>").Count;
        Assert.Equal(openCount, closeCount);

        // Specifically: the `System.Action{...}</c>.` token must not be
        // preceded by a stray `"`.
        Assert.DoesNotContain("}\"</c>", output);
    }
}
