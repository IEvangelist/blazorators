// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;
using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for the <c>SourceBuilder</c> emission paths that
/// generate the <c>private Action&lt;...&gt;? _callback</c> backing
/// field and the <c>[JSInvokable] public void OnX(...) =&gt;
/// _x?.Invoke(...);</c> shim for callback parameters.
///
/// <para>
/// For TS callbacks with zero parameters (e.g. <c>VoidFunction</c>,
/// <c>(): void;</c>) both helpers previously emitted invalid C#:
/// the field was <c>private Action&lt;&gt;? _x</c> (empty generic
/// argument list) and the shim was truncated at the open paren
/// (<c>public void OnX(</c>) with no closing paren or body. Consumers
/// targeting any DOM interface that uses <c>VoidFunction</c> (for
/// example <c>WindowOrWorkerGlobalScope.queueMicrotask</c>) would see
/// a compile failure in their generated implementation file.
/// </para>
/// </summary>
public class SourceBuilderZeroParameterCallbackTests
{
    private static SourceBuilder CreateBuilder() =>
        new(new GeneratorOptions(
            SupportsGenerics: false,
            TypeName: "Window",
            Implementation: "window"));

    private static List<CSharpMethod> BuildMethodWithZeroParameterCallback()
    {
        var action = new CSharpAction(
            RawName: "callback",
            RawReturnTypeName: "void",
            ParameterDefinitions: []);

        var callbackParam = new CSharpType(
            RawName: "callback",
            RawTypeName: "VoidFunction",
            IsNullable: false,
            ActionDeclaration: action);

        return
        [
            new CSharpMethod(
                RawName: "QueueMicrotask",
                RawReturnTypeName: "void",
                ParameterDefinitions: [callbackParam],
                JavaScriptMethodDependency: null)
        ];
    }

    [Fact]
    public void AppendConditionalDelegateFields_ZeroParameterCallback_EmitsNonGenericActionField()
    {
        var builder = CreateBuilder();
        builder.AppendConditionalDelegateFields(BuildMethodWithZeroParameterCallback());

        var output = builder.ToSourceCodeString();

        Assert.Contains("private Action? _callback;", output);
        Assert.DoesNotContain("Action<>", output);
    }

    [Fact]
    public void AppendConditionalDelegateCallbackMethods_ZeroParameterCallback_EmitsClosedJSInvokableShim()
    {
        var builder = CreateBuilder();
        builder.AppendConditionalDelegateCallbackMethods(BuildMethodWithZeroParameterCallback());

        var output = builder.ToSourceCodeString();

        // Builder output is later normalized by Roslyn (which collapses
        // whitespace), so this asserts the structural pieces are present
        // rather than line-strict matching. Crucially, the closing `)`
        // must follow the open `(` -- previously the open paren was left
        // dangling, producing uncompilable output.
        Assert.Contains("[JSInvokable]", output);
        Assert.Contains("public void OnCallback(", output);
        Assert.Contains(") =>", output);
        Assert.Contains("_callback?.Invoke();", output);
    }
}
