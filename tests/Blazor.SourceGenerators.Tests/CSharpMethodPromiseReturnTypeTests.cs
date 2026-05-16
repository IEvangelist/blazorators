// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;
using Blazor.SourceGenerators.CSharp;
using Blazor.SourceGenerators.Extensions;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for <c>Promise&lt;T&gt;</c> return-type
/// unwrapping in <see cref="CSharpMethodExtensions.GetMethodTypes"/>
/// and <see cref="MethodBuilderDetails.Create"/>.
///
/// <para>
/// TypeScript declares many DOM APIs as Promise-returning:
/// <c>Permissions.query(): Promise&lt;PermissionStatus&gt;</c>,
/// <c>Body.arrayBuffer(): Promise&lt;ArrayBuffer&gt;</c>,
/// <c>AudioContext.close(): Promise&lt;void&gt;</c>, etc. Without
/// explicit handling the generator dropped the raw
/// <c>Promise&lt;T&gt;</c> straight into the C# method signature and
/// the <c>_javaScript.Invoke&lt;T&gt;()</c> call -- <c>Promise</c>
/// is not a CLR type, so the consumer's compilation broke
/// immediately.
/// </para>
///
/// <para>
/// The fix peels <c>Promise&lt;T&gt;</c> at the parser boundary,
/// maps the unwrapped <c>T</c> through the usual primitive / array
/// / custom-nullable resolution, and forces the async invocation
/// suffix (<c>InvokeAsync</c>, <c>InvokeVoidAsync</c>) plus a
/// <c>ValueTask</c>-wrapped return type even when hosting under
/// WebAssembly. The <c>_javaScript</c> field stays
/// <c>IJSInProcessRuntime</c> for WASM (which inherits the async
/// methods from <c>IJSRuntime</c>), so consumers retain a single
/// runtime dependency.
/// </para>
/// </summary>
public class CSharpMethodPromiseReturnTypeTests
{
    private static GeneratorOptions WebAssembly => new(SupportsGenerics: false, IsWebAssembly: true);
    private static GeneratorOptions Server => new(SupportsGenerics: false, IsWebAssembly: false);

    [Theory]
    [InlineData("Promise<string>", "string")]
    [InlineData("Promise<number>", "number")]
    [InlineData("Promise<boolean>", "boolean")]
    [InlineData("Promise<PermissionStatus>", "PermissionStatus")]
    [InlineData("Promise<ArrayBuffer>", "ArrayBuffer")]
    public void IsPromise_DetectsAndUnwraps(string rawReturnType, string expectedUnwrapped)
    {
        var method = new CSharpMethod("doStuff", rawReturnType, [], null);

        Assert.True(method.IsPromise);
        Assert.Equal(expectedUnwrapped, method.PromiseUnwrappedTypeName);
    }

    [Theory]
    [InlineData("void")]
    [InlineData("string")]
    [InlineData("string | null")]
    [InlineData("number[]")]
    public void IsPromise_FalseForNonPromise(string rawReturnType)
    {
        var method = new CSharpMethod("doStuff", rawReturnType, [], null);

        Assert.False(method.IsPromise);
    }

    [Fact]
    public void IsPromise_DoesNotMatchPromiseStringRejection_NoAngleBrackets()
    {
        // Defensive: ensure the bare identifier `Promise` (no generic
        // payload) doesn't trip the prefix-only check. This is unlikely
        // in real DOM input but cheap to pin.
        var method = new CSharpMethod("doStuff", "Promise", [], null);

        Assert.False(method.IsPromise);
    }

    [Theory]
    [InlineData("Promise<string>", "ValueTask<string>", "string")]
    [InlineData("Promise<number>", "ValueTask<double>", "double")]
    [InlineData("Promise<PermissionStatus>", "ValueTask<PermissionStatus>", "PermissionStatus")]
    [InlineData("Promise<Node | null>", "ValueTask<Node?>", "Node?")]
    [InlineData("Promise<number[]>", "ValueTask<double[]>", "double[]")]
    public void GetMethodTypes_WebAssemblyPromise_WrapsInValueTaskEvenOnWasm(
        string rawReturnType,
        string expectedReturnType,
        string expectedBareType)
    {
        var method = new CSharpMethod("doStuff", rawReturnType, [], null);
        var (returnType, bareType) = method.GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal(expectedReturnType, returnType);
        Assert.Equal(expectedBareType, bareType);
    }

    [Fact]
    public void GetMethodTypes_PromiseVoid_EmitsValueTask()
    {
        var method = new CSharpMethod("doStuff", "Promise<void>", [], null);
        var (returnType, _) = method.GetMethodTypes(
            WebAssembly,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal("ValueTask", returnType);
    }

    [Fact]
    public void GetMethodTypes_PromiseVoidOnServer_EmitsValueTask()
    {
        var method = new CSharpMethod("doStuff", "Promise<void>", [], null);
        var (returnType, _) = method.GetMethodTypes(
            Server,
            isGenericReturnType: false,
            isPrimitiveType: false);

        Assert.Equal("ValueTask", returnType);
    }

    [Fact]
    public void MethodBuilderDetails_PromiseOnWasm_ForcesAsyncSuffix()
    {
        var method = new CSharpMethod("doStuff", "Promise<string>", [], null);
        var details = MethodBuilderDetails.Create(method, WebAssembly);

        // Even on WebAssembly, a Promise-returning method must use
        // `InvokeAsync` (not `Invoke`), so the suffix is forced to
        // "Async". The extending type stays `IJSInProcessRuntime` --
        // it inherits `InvokeAsync<T>` from `IJSRuntime`.
        Assert.Equal("Async", details.Suffix);
        Assert.Equal("IJSInProcessRuntime", details.ExtendingType);
    }

    [Fact]
    public void MethodBuilderDetails_PromiseVoidOnWasm_IsVoid()
    {
        var method = new CSharpMethod("doStuff", "Promise<void>", [], null);
        var details = MethodBuilderDetails.Create(method, WebAssembly);

        Assert.True(details.IsVoid);
        Assert.Equal("Async", details.Suffix);
    }

    /// <summary>
    /// Regression: a method whose generic return type is nullable
    /// inside a Promise (<c>setItem&lt;T&gt;(): Promise&lt;T | null&gt;</c>
    /// in real-world DOM code, e.g. a hypothetical typed
    /// <c>localStorage.getItem</c>) needs to emit
    /// <c>ValueTask&lt;TValue?&gt;</c> -- not
    /// <c>ValueTask&lt;TValue&gt;</c>. The original
    /// <see cref="CSharpMethod.IsReturnTypeNullable"/> implementation
    /// only inspected the outer <c>RawReturnTypeName</c>
    /// (<c>"Promise&lt;T | null&gt;"</c>), which ends with <c>&gt;</c>
    /// not <c> | null</c>, so the nullable annotation was silently
    /// dropped under the WASM + generics + Promise combination. Pin
    /// behaviour at both layers so the next refactor surfaces a
    /// failure if either the unwrap step or the nullable propagation
    /// regresses.
    /// </summary>
    [Theory]
    [InlineData("Promise<string | null>", true)]
    [InlineData("Promise<Node | null>", true)]
    [InlineData("Promise<T | null>", true)]
    [InlineData("Promise<string | undefined>", true)]
    [InlineData("Promise<string>", false)]
    [InlineData("Promise<Node>", false)]
    [InlineData("Promise<void>", false)]
    public void IsReturnTypeNullable_PeelsPromiseBeforeCheck(
        string rawReturnType, bool expectedNullable)
    {
        var method = new CSharpMethod("doStuff", rawReturnType, [], null);

        Assert.Equal(expectedNullable, method.IsReturnTypeNullable);
    }

    [Fact]
    public void GetMethodTypes_GenericPromiseNullable_EmitsValueTaskOfTValueQuestionMark()
    {
        // Hypothetical typed `localStorage.getItem<T>(key: string): Promise<T | null>`
        // under SupportsGenerics. The historical regression dropped
        // the `?` on `TValue` because `IsReturnTypeNullable` only saw
        // the outer `Promise<...>` wrapper.
        var method = new CSharpMethod("getItem", "Promise<T | null>", [], null);
        var (returnType, _) = method.GetMethodTypes(
            WebAssembly,
            isGenericReturnType: true,
            isPrimitiveType: false);

        Assert.Equal("ValueTask<TValue?>", returnType);
    }
}
