// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

internal static class DomTransportValidator
{
    // IJSRuntime does not expose JSRuntime.JsonSerializerOptions, which is
    // protected internal. .NET 8, 9, and 10 configure its MaxDepth as 32.
    // Method payloads sit below both the JSRuntime argument array and the DOM
    // method-argument array; property and index payloads use only the former.
    // System.Text.Json also needs one remaining level to write a value in the
    // deepest container. Reserve both worst-case wrappers and that value level
    // so one limit is safe for every path.
    internal const int FrameworkJsonSerializerMaxDepth = 32;
    internal const int MaximumInteropJsonWrapperDepth = 2;
    internal const int RequiredTerminalJsonValueDepth = 1;
    internal const int MaximumJsonContainerDepth =
        FrameworkJsonSerializerMaxDepth -
        MaximumInteropJsonWrapperDepth -
        RequiredTerminalJsonValueDepth;

    private static readonly HashSet<Type> s_jsonScalars =
    [
        typeof(string),
        typeof(char),
        typeof(bool),
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Uri),
        typeof(JsonElement),
        typeof(JsonDocument),
    ];

    private static readonly JsonSerializerOptions s_reviewedJsonSerializerOptions = new()
    {
        MaxDepth = FrameworkJsonSerializerMaxDepth,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static void ValidateJsonResult<TValue>(
        bool allowStructuredClone = false)
    {
        var type = typeof(TValue);
        if (allowStructuredClone && type == typeof(object))
            return;
        if (!IsJsonType(type, new HashSet<Type>()))
        {
            throw UnsupportedJson(type, "result");
        }
    }

    public static object? PrepareArgument(object? value, string path)
    {
        switch (value)
        {
            case null:
                return null;
            case IDomProxy proxy:
                return proxy.Reference;
            case IJSObjectReference:
            case IJSStreamReference:
            case DotNetStreamReference:
            case byte[]:
                return value;
            case IDomUnionValue union:
                return PrepareUnion(union, path);
            case DomDynamicValue dynamicValue:
                return PrepareDynamic(dynamicValue, path);
            default:
                return NormalizeJsonValue(value, path);
        }
    }

    public static void ValidateJsonValue(object value, string path)
    {
        _ = NormalizeJsonValue(value, path);
    }

    private static object? NormalizeJsonValue(object value, string path)
    {
        return NormalizeJsonValue(
            value,
            path,
            new Dictionary<object, string>(ReferenceEqualityComparer.Instance),
            containerDepth: 0);
    }

    private static object? NormalizeJsonValue(
        object value,
        string path,
        Dictionary<object, string> visiting,
        int containerDepth)
    {
        if (value is IDomOptionalValue optional)
        {
            if (!optional.IsSpecified)
            {
                throw new DomTransportException(
                    $"Optional DOM value at '{path}' was not specified.");
            }
            return optional.UntypedValue is { } specified
                ? NormalizeJsonValue(specified, path, visiting, containerDepth)
                : null;
        }
        if (value is BrowserNull or BrowserUndefined)
        {
            return null;
        }
        if (value is IDomUnionValue union)
        {
            return PrepareUnion(union, path);
        }
        if (value is JsonElement element)
        {
            return NormalizeJsonElement(element, path, containerDepth);
        }

        if (value is JsonDocument document)
        {
            return NormalizeJsonElement(document.RootElement, path, containerDepth);
        }
        if (value is ITypeScriptStringValue stringValue)
        {
            return stringValue.Value;
        }

        var type = value.GetType();
        if (IsScalar(type) || type.IsEnum)
        {
            return value;
        }
        if (value is byte[])
        {
            throw new DomTransportException(
                $"Binary value at '{path}' must use the binary transport, not JSON.");
        }
        if (value is IDictionary dictionary)
        {
            EnterContainer(value, path, visiting, containerDepth);
            try
            {
                var normalized = new Dictionary<string, object?>(
                    dictionary.Count,
                    StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not string key)
                    {
                        throw new DomTransportException(
                            $"JSON dictionary at '{path}' has a non-string key.");
                    }
                    normalized.Add(
                        key,
                        entry.Value is null
                            ? null
                            : NormalizeJsonValue(
                                entry.Value,
                                AppendDictionaryPath(path, key),
                                visiting,
                                containerDepth + 1));
                }
                return normalized;
            }
            finally
            {
                visiting.Remove(value);
            }
        }
        if (value is Array array)
        {
            return NormalizeArray(array, path, visiting, containerDepth);
        }
        if (value is IEnumerable sequence && value is not string)
        {
            EnterContainer(value, path, visiting, containerDepth);
            try
            {
                var normalized = new List<object?>();
                var index = 0;
                foreach (var item in sequence)
                {
                    normalized.Add(
                        item is null
                            ? null
                            : NormalizeJsonValue(
                                item,
                                $"{path}[{index}]",
                                visiting,
                                containerDepth + 1));
                    index++;
                }
                return normalized;
            }
            finally
            {
                visiting.Remove(value);
            }
        }
        if (type.GetCustomAttribute<DomJsonValueAttribute>() is not null)
        {
            return NormalizeReviewedJsonValue(value, path, containerDepth);
        }

        throw UnsupportedJson(type, path);
    }

    private static object? PrepareUnion(IDomUnionValue union, string path)
    {
        if (union.ArmIndex <= 0)
        {
            throw new DomTransportException(
                $"Union value at '{path}' has no selected arm.");
        }
        var field = union.GetType().GetField(
            "_value",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null)
        {
            throw new DomTransportException(
                $"Union value '{union.GetType().FullName}' at '{path}' does not expose " +
                "the generated transport storage contract.");
        }
        var value = field.GetValue(union);
        var descriptor = union.SelectedTransport;
        if (descriptor.Kind == DomTransportKind.Unsupported)
        {
            throw new DomTransportException(
                $"TypeScript union arm '{descriptor.SourceType}' is unsupported: " +
                descriptor.Reason);
        }
        if (value is null)
        {
            if (!descriptor.Nullable)
            {
                throw new DomTransportException(
                    $"TypeScript union arm '{descriptor.SourceType}' at '{path}' is not nullable.");
            }
            return null;
        }
        if (value is IDomUnionValue nested)
        {
            return PrepareUnion(nested, path);
        }
        return descriptor.Kind switch
        {
            DomTransportKind.JsonValue => NormalizeJsonValue(value, path),
            DomTransportKind.JsReference or DomTransportKind.Transferable =>
                PrepareDynamicReference(value, descriptor),
            DomTransportKind.Binary => value is byte[] bytes
                ? bytes
                : throw WrongDynamicType(descriptor, value, "byte[]"),
            DomTransportKind.JsStream => value is DotNetStreamReference stream
                ? stream
                : throw WrongDynamicType(
                    descriptor,
                    value,
                    nameof(DotNetStreamReference)),
            _ => throw new DomTransportException(
                $"Transport '{descriptor.Kind}' cannot be used for union arm " +
                $"'{descriptor.SourceType}'."),
        };
    }

    private static object NormalizeReviewedJsonValue(
        object value,
        string path,
        int containerDepth)
    {
        return NormalizeReviewedJsonValue(
            value,
            path,
            new Dictionary<object, string>(
                ReferenceEqualityComparer.Instance),
            containerDepth);
    }

    private static object NormalizeReviewedJsonValue(
        object value,
        string path,
        Dictionary<object, string> visiting,
        int containerDepth)
    {
        EnterContainer(value, path, visiting, containerDepth);
        try
        {
            var normalized = new Dictionary<string, object?>(
                StringComparer.Ordinal);
            foreach (var property in value.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property =>
                    property.GetMethod is not null
                    && property.GetIndexParameters().Length == 0)
                .OrderBy(property => property.MetadataToken))
            {
                var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
                if (ignore?.Condition == JsonIgnoreCondition.Always)
                {
                    continue;
                }

                var propertyValue = property.GetValue(value);
                if ((ignore?.Condition == JsonIgnoreCondition.WhenWritingNull
                        && propertyValue is null)
                    || (ignore?.Condition == JsonIgnoreCondition.WhenWritingDefault
                        && IsDefaultJsonValue(propertyValue, property.PropertyType)))
                {
                    continue;
                }
                var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()
                    ?.Name
                    ?? s_reviewedJsonSerializerOptions.PropertyNamingPolicy
                        ?.ConvertName(property.Name)
                    ?? property.Name;
                normalized[name] = propertyValue is null
                    ? null
                    : NormalizeReviewedMember(
                        propertyValue,
                        $"{path}.{name}",
                        visiting,
                        containerDepth + 1);
            }

            return normalized;
        }
        finally
        {
            visiting.Remove(value);
        }
    }

    private static bool IsDefaultJsonValue(object? value, Type propertyType)
    {
        if (value is null)
        {
            return true;
        }
        if (!propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) is not null)
        {
            return false;
        }

        return Equals(value, Activator.CreateInstance(propertyType));
    }

    private static object? NormalizeReviewedMember(
        object value,
        string path,
        Dictionary<object, string> visiting,
        int containerDepth)
    {
        if (value is IDomOptionalValue optional)
        {
            if (!optional.IsSpecified)
            {
                throw new DomTransportException(
                    $"Optional DOM value at '{path}' was not specified.");
            }
            return optional.UntypedValue is { } specified
                ? NormalizeReviewedMember(
                    specified,
                    path,
                    visiting,
                    containerDepth)
                : null;
        }
        if (value is BrowserNull or BrowserUndefined)
        {
            return null;
        }
        if (value is IDomProxy proxy)
            return proxy.Reference;
        if (value is IJSObjectReference
            or IJSStreamReference
            or DotNetStreamReference
            or byte[])
        {
            return value;
        }
        if (value is IDomUnionValue union)
            return PrepareUnion(union, path);
        if (value is DomDynamicValue dynamicValue)
            return PrepareDynamic(dynamicValue, path);
        if (value is JsonElement element)
        {
            return NormalizeJsonElement(element, path, containerDepth);
        }
        if (value is JsonDocument document)
        {
            return NormalizeJsonElement(
                document.RootElement,
                path,
                containerDepth);
        }
        if (value is ITypeScriptStringValue stringValue)
        {
            return stringValue.Value;
        }

        var type = value.GetType();
        if (IsScalar(type) || type.IsEnum)
            return value;
        if (type.GetCustomAttribute<DomJsonValueAttribute>() is not null)
        {
            return NormalizeReviewedJsonValue(
                value,
                path,
                visiting,
                containerDepth);
        }
        if (value is IDictionary dictionary)
        {
            EnterContainer(value, path, visiting, containerDepth);
            try
            {
                var normalized = new Dictionary<string, object?>(
                    dictionary.Count,
                    StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not string key)
                    {
                        throw new DomTransportException(
                            $"JSON dictionary at '{path}' has a non-string key.");
                    }
                    normalized[key] = entry.Value is null
                        ? null
                        : NormalizeReviewedMember(
                            entry.Value,
                            AppendDictionaryPath(path, key),
                            visiting,
                            containerDepth + 1);
                }
                return normalized;
            }
            finally
            {
                visiting.Remove(value);
            }
        }
        if (value is IEnumerable sequence && value is not string)
        {
            EnterContainer(value, path, visiting, containerDepth);
            try
            {
                var normalized = new List<object?>();
                var index = 0;
                foreach (var item in sequence)
                {
                    normalized.Add(item is null
                        ? null
                        : NormalizeReviewedMember(
                            item,
                            $"{path}[{index}]",
                            visiting,
                            containerDepth + 1));
                    index++;
                }
                return normalized.ToArray();
            }
            finally
            {
                visiting.Remove(value);
            }
        }

        throw UnsupportedJson(type, path);
    }

    private static JsonElement NormalizeJsonElement(
        JsonElement element,
        string path,
        int containerDepth)
    {
        ValidateJsonElement(element, path, containerDepth);
        return element.Clone();
    }

    private static void ValidateJsonElement(
        JsonElement element,
        string path,
        int containerDepth)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateContainerDepth(path, containerDepth);
                foreach (var property in element.EnumerateObject())
                {
                    ValidateJsonElement(
                        property.Value,
                        AppendDictionaryPath(path, property.Name),
                        containerDepth + 1);
                }
                break;
            case JsonValueKind.Array:
                ValidateContainerDepth(path, containerDepth);
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    ValidateJsonElement(
                        item,
                        $"{path}[{index}]",
                        containerDepth + 1);
                    index++;
                }
                break;
            case JsonValueKind.Undefined:
                throw new DomTransportException(
                    $"JSON value at '{path}' is undefined and cannot be transported.");
        }
    }

    private static object NormalizeArray(
        Array array,
        string path,
        Dictionary<object, string> visiting,
        int containerDepth)
    {
        if (array.Rank != 1 || array.GetLowerBound(0) != 0)
        {
            throw new DomTransportException(
                $"JSON array at '{path}' must be one-dimensional and zero-based.");
        }

        EnterContainer(array, path, visiting, containerDepth);
        try
        {
            var values = new object?[array.Length];
            var changed = false;
            for (var index = 0; index < array.Length; index++)
            {
                var item = array.GetValue(index);
                var normalized = item is null
                    ? null
                    : NormalizeJsonValue(
                        item,
                        $"{path}[{index}]",
                        visiting,
                        containerDepth + 1);
                values[index] = normalized;
                changed |= !ReferenceEquals(item, normalized);
            }
            if (!changed)
            {
                return array;
            }

            var elementType = array.GetType().GetElementType();
            if (elementType is not null &&
                values.All(value => value is null
                    ? !elementType.IsValueType ||
                        Nullable.GetUnderlyingType(elementType) is not null
                    : elementType.IsInstanceOfType(value)))
            {
                var normalizedArray = Array.CreateInstance(elementType, values.Length);
                for (var index = 0; index < values.Length; index++)
                {
                    normalizedArray.SetValue(values[index], index);
                }
                return normalizedArray;
            }
            return values;
        }
        finally
        {
            visiting.Remove(array);
        }
    }

    private static void EnterContainer(
        object value,
        string path,
        Dictionary<object, string> visiting,
        int containerDepth)
    {
        if (visiting.TryGetValue(value, out var ancestorPath))
        {
            throw new DomTransportException(
                $"JSON container at '{path}' contains a reference cycle to '{ancestorPath}'.");
        }
        ValidateContainerDepth(path, containerDepth);
        visiting.Add(value, path);
    }

    private static void ValidateContainerDepth(string path, int containerDepth)
    {
        if (containerDepth >= MaximumJsonContainerDepth)
        {
            throw new DomTransportException(
                $"JSON container at '{path}' exceeds the maximum validation depth " +
                $"of {MaximumJsonContainerDepth}.");
        }
    }

    private static string AppendDictionaryPath(string path, string key) =>
        IsIdentifier(key)
            ? $"{path}.{key}"
            : $"{path}[{JsonSerializer.Serialize(key)}]";

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 ||
            !(char.IsLetter(value[0]) || value[0] is '_' or '$'))
        {
            return false;
        }
        for (var index = 1; index < value.Length; index++)
        {
            if (!(char.IsLetterOrDigit(value[index]) ||
                value[index] is '_' or '$'))
            {
                return false;
            }
        }
        return true;
    }

    private static object? PrepareDynamic(DomDynamicValue value, string path)
    {
        var descriptor = value.Transport;
        if (descriptor.Kind == DomTransportKind.Unsupported)
        {
            throw new DomTransportException(
                $"TypeScript value '{descriptor.SourceType}' is unsupported: {descriptor.Reason}");
        }
        if (value.Value is null)
        {
            if (!descriptor.Nullable)
            {
                throw new DomTransportException(
                    $"TypeScript value '{descriptor.SourceType}' is not nullable.");
            }
            return null;
        }

        return descriptor.Kind switch
        {
            DomTransportKind.JsonValue => PrepareDynamicJson(value.Value, path),
            DomTransportKind.JsReference or DomTransportKind.Transferable =>
                PrepareDynamicReference(value.Value, descriptor),
            DomTransportKind.Binary => value.Value is byte[] bytes
                ? bytes
                : throw WrongDynamicType(descriptor, value.Value, "byte[]"),
            DomTransportKind.JsStream => value.Value is DotNetStreamReference stream
                ? stream
                : throw WrongDynamicType(
                    descriptor,
                    value.Value,
                    nameof(DotNetStreamReference)),
            _ => throw new DomTransportException(
                $"Transport '{descriptor.Kind}' cannot be used as a direct JS argument."),
        };
    }

    private static object? PrepareDynamicJson(object value, string path)
        => NormalizeJsonValue(value, path);

    private static object PrepareDynamicReference(
        object value,
        DomTransportDescriptor descriptor) =>
        value switch
        {
            IDomProxy proxy => proxy.Reference,
            IJSObjectReference reference => reference,
            _ => throw WrongDynamicType(
                descriptor,
                value,
                nameof(IJSObjectReference)),
        };

    private static bool IsJsonType(Type type, HashSet<Type> visiting)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (IsScalar(type) || type.IsEnum)
        {
            return true;
        }
        if (typeof(ITypeScriptStringValue).IsAssignableFrom(type))
        {
            return true;
        }
        if (type == typeof(object) ||
            type == typeof(byte[]) ||
            typeof(IDomProxy).IsAssignableFrom(type) ||
            typeof(IJSObjectReference).IsAssignableFrom(type) ||
            typeof(IJSStreamReference).IsAssignableFrom(type) ||
            typeof(DotNetStreamReference).IsAssignableFrom(type))
        {
            return false;
        }
        if (type.GetCustomAttribute<DomJsonValueAttribute>() is not null)
        {
            return true;
        }
        if (!visiting.Add(type))
        {
            return false;
        }
        try
        {
            if (type.IsArray)
            {
                var element = type.GetElementType();
                return element is not null && IsJsonType(element, visiting);
            }

            var dictionary = FindGeneric(type, typeof(IDictionary<,>)) ??
                FindGeneric(type, typeof(IReadOnlyDictionary<,>));
            if (dictionary is not null)
            {
                var arguments = dictionary.GetGenericArguments();
                return arguments[0] == typeof(string) &&
                    IsJsonType(arguments[1], visiting);
            }

            var sequence = FindGeneric(type, typeof(IEnumerable<>));
            return sequence is not null &&
                IsJsonType(sequence.GetGenericArguments()[0], visiting);
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    private static Type? FindGeneric(Type type, Type definition)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == definition)
        {
            return type;
        }
        return type.GetInterfaces().FirstOrDefault(
            candidate => candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == definition);
    }

    private static bool IsScalar(Type type) =>
        s_jsonScalars.Contains(Nullable.GetUnderlyingType(type) ?? type);

    private static DomTransportException UnsupportedJson(Type type, string path) =>
        new(
            $"Value of managed type '{type.FullName}' at '{path}' is not an approved JSON " +
            $"transport. Use a primitive, enum, string-keyed dictionary, " +
            $"[{nameof(DomJsonValueAttribute)}], or {nameof(DomDynamicValue)} with an explicit transport.");

    private static DomTransportException WrongDynamicType(
        DomTransportDescriptor descriptor,
        object value,
        string expected) =>
        new(
            $"Dynamic TypeScript value '{descriptor.SourceType}' selected transport " +
            $"'{descriptor.Kind}' but supplied managed type '{value.GetType().FullName}' " +
            $"other than {expected}.");
}
