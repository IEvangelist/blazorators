// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.DOM.Tests.Fakes;

public interface IConfigurableJSObjectReference : IJSObjectReference
{
    int DisposeCallCount { get; }

    List<(string Identifier, object?[]? Args)> Invocations { get; }

    Dictionary<string, object?> ReturnValues { get; }

    Dictionary<string, Exception> ThrowValues { get; }

    Dictionary<
        string,
        Func<object?[]?, CancellationToken, ValueTask<object?>>> InvocationHandlers { get; }
}
