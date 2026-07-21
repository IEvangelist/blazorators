// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

/// <summary>Represents JavaScript's distinct <c>null</c> value in generic positions.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(BrowserNullJsonConverter))]
public readonly record struct BrowserNull;

/// <summary>Represents JavaScript's distinct <c>undefined</c> value in generic positions.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(BrowserUndefinedJsonConverter))]
public readonly record struct BrowserUndefined;

internal sealed class BrowserNullJsonConverter
    : System.Text.Json.Serialization.JsonConverter<BrowserNull>
{
    public override BrowserNull Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.Null)
            throw new System.Text.Json.JsonException("BrowserNull must be encoded as JSON null.");
        return default;
    }

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        BrowserNull value,
        System.Text.Json.JsonSerializerOptions options) => writer.WriteNullValue();
}

internal sealed class BrowserUndefinedJsonConverter
    : System.Text.Json.Serialization.JsonConverter<BrowserUndefined>
{
    public override BrowserUndefined Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.Null)
            throw new System.Text.Json.JsonException(
                "BrowserUndefined can only be received through the JSON null sentinel.");
        return default;
    }

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        BrowserUndefined value,
        System.Text.Json.JsonSerializerOptions options) => writer.WriteNullValue();
}

/// <summary>The exact yielded-or-returned result of a JavaScript iterator step.</summary>
public readonly struct BrowserIteratorResult<TYield, TReturn>
{
    private readonly TYield? _yield;
    private readonly TReturn? _return;

    private BrowserIteratorResult(bool done, TYield? yield, TReturn? @return) =>
        (Done, _yield, _return) = (done, yield, @return);

    public bool Done { get; }

    public static BrowserIteratorResult<TYield, TReturn> Yield(TYield value) =>
        new(false, value, default);

    public static BrowserIteratorResult<TYield, TReturn> Return(TReturn value) =>
        new(true, default, value);

    public TYield GetYield() => !Done
        ? _yield!
        : throw new InvalidOperationException("The iterator result is complete.");

    public TReturn GetReturn() => Done
        ? _return!
        : throw new InvalidOperationException("The iterator result contains a yield value.");
}

/// <summary>A live JavaScript iterator. It does not claim .NET enumerable semantics.</summary>
public interface IBrowserIterator<TYield, TReturn, in TNext> : IDomProxy
{
    ValueTask<BrowserIteratorResult<TYield, TReturn>> NextAsync(
        TNext value,
        CancellationToken cancellationToken = default);
}

/// <summary>An iterator that can be explicitly completed through JavaScript <c>return</c>.</summary>
public interface IReturnableBrowserIterator<TYield, TReturn, in TNext>
    : IBrowserIterator<TYield, TReturn, TNext>
{
    ValueTask<BrowserIteratorResult<TYield, TReturn>> ReturnAsync(
        TReturn value,
        CancellationToken cancellationToken = default);
}

/// <summary>An iterator that exposes JavaScript <c>throw</c>.</summary>
public interface IThrowableBrowserIterator<TYield, TReturn, in TNext>
    : IBrowserIterator<TYield, TReturn, TNext>
{
    ValueTask<BrowserIteratorResult<TYield, TReturn>> ThrowAsync(
        Exception error,
        CancellationToken cancellationToken = default);
}

/// <summary>A live JavaScript object that creates a synchronous iterator reference.</summary>
public interface IBrowserIterable<T> : IDomProxy
{
    ValueTask<IBrowserIterator<T, BrowserUndefined, object?>>
        GetIteratorAsync(CancellationToken cancellationToken = default);
}

/// <summary>A live JavaScript iterator that is also its own iterable.</summary>
public interface IBrowserIterableIterator<T>
    : IBrowserIterator<T, BrowserUndefined, object?>
{
    ValueTask<IBrowserIterableIterator<T>> GetIteratorAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>A live JavaScript async iterator.</summary>
public interface IBrowserAsyncIterator<TYield, TReturn, in TNext> : IDomProxy
{
    ValueTask<BrowserIteratorResult<TYield, TReturn>> NextAsync(
        TNext value,
        CancellationToken cancellationToken = default);
}

/// <summary>A live JavaScript object that creates an async iterator reference.</summary>
public interface IBrowserAsyncIterable<T> : IDomProxy
{
    ValueTask<IBrowserAsyncIterator<T, BrowserUndefined, object?>>
        GetAsyncIteratorAsync(CancellationToken cancellationToken = default);
}

/// <summary>A live JavaScript async iterator that is also its own async iterable.</summary>
public interface IBrowserAsyncIterableIterator<T>
    : IBrowserAsyncIterator<T, BrowserUndefined, object?>
{
    ValueTask<IBrowserAsyncIterableIterator<T>> GetAsyncIteratorAsync(
        CancellationToken cancellationToken = default);
}
