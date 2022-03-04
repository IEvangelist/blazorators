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

    /// <inheritdoc cref="Deserialize{TValue}(string, JsonSerializerOptions?)" />
    public static TResult? FromJson<TResult>(
        this string? json,
        JsonSerializerOptions? options = null) =>
        json is { Length: > 0 }
            ? Deserialize<TResult>(json, options ?? _defaultOptions)
            : default;

    /// <inheritdoc cref="Serialize{TValue}(TValue, JsonSerializerOptions?)" />
    public static string ToJson<T>(
        this T value,
        JsonSerializerOptions? options = null) =>
        Serialize(value, options ?? _defaultOptions);
}
