// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.JSInterop.Infrastructure;

namespace Blazor.DOM.Tests;

internal sealed class SerializingJSRuntime : JSRuntime
{
    private long _nextObjectReferenceId;

    public List<Invocation> Invocations { get; } = [];

    public int SerializerMaxDepth => JsonSerializerOptions.MaxDepth;

    protected override void BeginInvokeJS(
        long taskId,
        string identifier,
        string? argsJson,
        JSCallResultType resultType,
        long targetInstanceId)
    {
        Invocations.Add(new Invocation(identifier, argsJson, targetInstanceId));

        var resultJson = resultType == JSCallResultType.JSObjectReference
            ? $$"""{"__jsObjectId":{{Interlocked.Increment(ref _nextObjectReferenceId)}}}"""
            : "null";
        DotNetDispatcher.EndInvokeJS(this, $"[{taskId},true,{resultJson}]");
    }

    protected override void EndInvokeDotNet(
        DotNetInvocationInfo invocationInfo,
        in DotNetInvocationResult invocationResult) =>
        throw new NotSupportedException();

    internal sealed record Invocation(
        string Identifier,
        string? ArgsJson,
        long TargetInstanceId);
}
