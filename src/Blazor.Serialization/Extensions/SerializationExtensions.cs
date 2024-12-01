// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.Serialization.Extensions;

/// <summary>
/// JSON serialization extension methods.
/// </summary>
public static class SerializationExtensions
{
    private static readonly JsonSerializerOptions _defaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)" />
    public static TResult? FromJson<TResult>(
        this string? json,
        JsonSerializerOptions? options = null) =>
        json is { Length: > 0 }
            ? JsonSerializer.Deserialize<TResult>(json, options ?? _defaultOptions)
            : default;

    /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(string, JsonTypeInfo{TValue})" />
    public static TResult? FromJson<TResult>(
        this string? json,
        JsonTypeInfo<TResult>? jsonTypeInfo)
    {
        return json switch
        {
            { Length: > 0 } => jsonTypeInfo switch
            {
                null => JsonSerializer.Deserialize<TResult>(json, _defaultOptions),
                _ => JsonSerializer.Deserialize(json, jsonTypeInfo)
            },
            _ => default
        };
    }

    /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)" />
    public static async ValueTask<TResult?> FromJsonAsync<TResult>(
        this ValueTask<string?> jsonTask,
        JsonSerializerOptions? options = null)
    {
        var json = await jsonTask.ConfigureAwait(false);

        return json is { Length: > 0 }
            ? JsonSerializer.Deserialize<TResult>(json, options ?? _defaultOptions)
            : default;
    }

    /// <inheritdoc cref="JsonSerializer.Deserialize{TValue}(string, JsonTypeInfo{TValue})" />
    public static async ValueTask<TResult?> FromJsonAsync<TResult>(
        this ValueTask<string?> jsonTask,
        JsonTypeInfo<TResult>? jsonTypeInfo)
    {
        var json = await jsonTask.ConfigureAwait(false);

        return json switch
        {
            { Length: > 0 } => jsonTypeInfo switch
            {
                null => JsonSerializer.Deserialize<TResult>(json, _defaultOptions),
                _ => JsonSerializer.Deserialize(json, jsonTypeInfo)
            },
            _ => default
        };
    }

    /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)" />
    public static string ToJson<T>(
        this T value,
        JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(value, options ?? _defaultOptions);

    /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(TValue, JsonTypeInfo{TValue})" />
    public static string ToJson<T>(
        this T value,
        JsonTypeInfo<T>? jsonTypeInfo)
    {
        return jsonTypeInfo switch
        {
            null => JsonSerializer.Serialize(value, _defaultOptions),
            _ => JsonSerializer.Serialize(value, jsonTypeInfo)
        };
    }

    /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)" />
    public static async ValueTask<string> ToJsonAsync<T>(
        this ValueTask<T> valueTask,
        JsonSerializerOptions? options = null)
    {
        var value = await valueTask.ConfigureAwait(false);

        return JsonSerializer.Serialize(value, options ?? _defaultOptions);
    }

    /// <inheritdoc cref="JsonSerializer.Serialize{TValue}(TValue, JsonTypeInfo{TValue})" />
    public static async ValueTask<string> ToJsonAsync<T>(
        this ValueTask<T> valueTask,
        JsonTypeInfo<T>? jsonTypeInfo)
    {
        var value = await valueTask.ConfigureAwait(false);

        return jsonTypeInfo switch
        {
            null => JsonSerializer.Serialize(value, _defaultOptions),
            _ => JsonSerializer.Serialize(value, jsonTypeInfo)
        };
    }
}
