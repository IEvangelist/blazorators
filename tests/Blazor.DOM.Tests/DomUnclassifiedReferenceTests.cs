// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;

namespace Blazor.DOM.Tests;

public sealed class DomUnclassifiedReferenceTests
{
    [Fact]
    public async Task TakeAs_TransfersOwnershipExactlyOnce()
    {
        var reference = new FakeJSObjectReference();
        var factory = new DomProxyFactory(null!);
        factory.Register<TestProxy>((value, _, _) => new TestProxy(value));
        var unclassified = DomUnclassifiedReference.Owned(reference);

        var proxy = unclassified.TakeAs<TestProxy>(factory);

        await unclassified.DisposeAsync();
        Assert.False(reference.IsDisposed);
        Assert.Throws<InvalidOperationException>(
            () => unclassified.TakeAs<TestProxy>(factory));
        await proxy.DisposeAsync();
        Assert.True(reference.IsDisposed);
        Assert.Equal(1, reference.DisposeCallCount);
    }

    private sealed class TestProxy(IJSObjectReference reference) : IDomProxy
    {
        public IJSObjectReference Reference { get; } = reference;
        public ValueTask DisposeAsync() => Reference.DisposeAsync();
    }
}
