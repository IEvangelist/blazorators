// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

/// <summary>
/// Preserves the three states of an optional nullable Web IDL dictionary member:
/// omitted, explicitly null, and a concrete value.
/// </summary>
[JsonConverter(typeof(DomOptionalJsonConverterFactory))]
public readonly struct DomOptional<T> : IDomOptionalValue
{
    private readonly T? _value;

    private DomOptional(T? value)
    {
        _value = value;
        IsSpecified = true;
    }

    /// <summary>Gets whether the dictionary member was explicitly specified.</summary>
    public bool IsSpecified { get; }

    /// <summary>Gets the specified value, which may itself be null.</summary>
    public T? Value => IsSpecified
        ? _value
        : throw new InvalidOperationException("The optional DOM value was not specified.");

    object? IDomOptionalValue.UntypedValue => _value;

    /// <summary>Creates an explicitly specified optional value.</summary>
    public static DomOptional<T> From(T? value) => new(value);

    /// <summary>Marks an assigned value, including null, as explicitly specified.</summary>
    public static implicit operator DomOptional<T>(T? value) => From(value);
}

/// <summary>Exposes generated optional values to the DOM transport normalizer.</summary>
public interface IDomOptionalValue
{
    /// <summary>Gets whether a value was explicitly specified.</summary>
    bool IsSpecified { get; }

    /// <summary>Gets the specified value without its generic type.</summary>
    object? UntypedValue { get; }
}

/// <summary>Creates JSON converters for generated <see cref="DomOptional{T}"/> values.</summary>
public sealed class DomOptionalJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(DomOptional<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(
            typeof(DomOptionalJsonConverter<>).MakeGenericType(valueType))!;
    }

    private sealed class DomOptionalJsonConverter<TValue>
        : JsonConverter<DomOptional<TValue>>
    {
        public override DomOptional<TValue> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            DomOptional<TValue>.From(
                JsonSerializer.Deserialize<TValue>(ref reader, options));

        public override void Write(
            Utf8JsonWriter writer,
            DomOptional<TValue> value,
            JsonSerializerOptions options)
        {
            if (!value.IsSpecified)
            {
                throw new JsonException(
                    "An unspecified optional DOM value must be omitted by its dictionary property.");
            }

            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
