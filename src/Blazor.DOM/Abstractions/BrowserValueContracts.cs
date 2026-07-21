// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>A live JavaScript promise that can be awaited without JSON serialization.</summary>
public interface IBrowserPromise<TResult> : IDomProxy
{
    /// <summary>Awaits the JavaScript promise. Cancellation stops the interop wait.</summary>
    ValueTask<TResult> AwaitAsync(CancellationToken cancellationToken = default);
}

/// <summary>A live JavaScript promise whose fulfillment value is ignored.</summary>
public interface IBrowserPromise : IDomProxy
{
    /// <summary>Awaits the JavaScript promise. Cancellation stops the interop wait.</summary>
    ValueTask AwaitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A live JavaScript thenable. Unlike a method-returned promise, it retains object identity.
/// </summary>
public interface IBrowserPromiseLike<TResult> : IDomProxy
{
    /// <summary>Awaits fulfillment through the JavaScript then contract.</summary>
    ValueTask<TResult> AwaitAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read-only logical access to a live JavaScript array.</summary>
public interface IReadOnlyBrowserArray<T> : IDomProxy
{
    /// <summary>Gets the current JavaScript array length.</summary>
    ValueTask<int> GetLengthAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the current value at <paramref name="index"/>.</summary>
    ValueTask<T> GetAsync(int index, CancellationToken cancellationToken = default);
}

/// <summary>Mutable logical access to a live JavaScript array.</summary>
public interface IBrowserArray<T> : IReadOnlyBrowserArray<T>
{
    /// <summary>Sets the value at <paramref name="index"/>.</summary>
    ValueTask SetAsync(
        int index,
        T value,
        CancellationToken cancellationToken = default);
}

/// <summary>Read-only logical access to a live JavaScript record.</summary>
public interface IReadOnlyBrowserRecord<TKey, TValue> : IDomProxy
{
    /// <summary>Gets a value without converting the live record into a snapshot.</summary>
    ValueTask<TValue> GetAsync(
        TKey key,
        CancellationToken cancellationToken = default);
}

/// <summary>Mutable logical access to a live JavaScript record.</summary>
public interface IBrowserRecord<TKey, TValue> : IReadOnlyBrowserRecord<TKey, TValue>
{
    /// <summary>Sets a value without converting the live record into a snapshot.</summary>
    ValueTask SetAsync(
        TKey key,
        TValue value,
        CancellationToken cancellationToken = default);
}

/// <summary>Read-only logical access to a live JavaScript Map.</summary>
public interface IReadOnlyBrowserMap<TKey, TValue> : IDomProxy
{
    ValueTask<int> GetSizeAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> HasAsync(TKey key, CancellationToken cancellationToken = default);
    ValueTask<TValue> GetAsync(TKey key, CancellationToken cancellationToken = default);
}

/// <summary>Mutable logical access to a live JavaScript Map.</summary>
public interface IBrowserMap<TKey, TValue> : IReadOnlyBrowserMap<TKey, TValue>
{
    ValueTask SetAsync(
        TKey key,
        TValue value,
        CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(TKey key, CancellationToken cancellationToken = default);
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read-only logical access to a live JavaScript Set.</summary>
public interface IReadOnlyBrowserSet<T> : IDomProxy
{
    ValueTask<int> GetSizeAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> HasAsync(T value, CancellationToken cancellationToken = default);
}

/// <summary>Mutable logical access to a live JavaScript Set.</summary>
public interface IBrowserSet<T> : IReadOnlyBrowserSet<T>
{
    ValueTask AddAsync(T value, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(T value, CancellationToken cancellationToken = default);
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
