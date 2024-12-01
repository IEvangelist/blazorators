// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.CSharp;

internal record CSharpType(
    string RawName,
    string RawTypeName,
    bool IsNullable = false,
    CSharpAction? ActionDeclation = null) : ICSharpDependencyGraphObject
{
    public Dictionary<string, CSharpObject> DependentTypes
    {
        get
        {
            Dictionary<string, CSharpObject> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in ActionDeclation?.DependentTypes
                ?? Enumerable.Empty<KeyValuePair<string, CSharpObject>>())
            {
                result[prop.Key] = prop.Value;
            }

            if (ActionDeclation is { ParameterDefinitions.Count: > 0 })
            {
                foreach (var type in ActionDeclation.ParameterDefinitions)
                {
                    foreach (var pair
                        in type.DependentTypes.SelectMany(
                            dt => dt.Value.AllDependentTypes))
                    {
                        result[pair.TypeName] = pair.Object;
                    }
                }
            }

            return result;
        }
    }

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes =>
        DependentTypes
            .SelectMany(kvp => kvp.Value.AllDependentTypes)
            .ToImmutableHashSet();

    /// <summary>
    /// Gets a string representation of the C# type as a parameter declaration. For example,
    /// <c>"DateTime date"</c> might be returned from a <see cref="CSharpType"/> with
    /// <c>"date"</c> as its <see cref="RawName"/> and <c>"DateTime"</c>
    /// as its <see cref="RawTypeName"/>.
    /// </summary>
    public string ToParameterString(bool isGenericType = false, bool overrideNullability = false)
    {
        if (isGenericType)
        {
            return IsNullable
                ? $"{MethodBuilderDetails.GenericTypeValue}? {ToArgumentString()}"
                : $"{MethodBuilderDetails.GenericTypeValue} {ToArgumentString()}";
        }

        var isCallback = ActionDeclation is not null;
        var typeName = TypeMap.PrimitiveTypes.IsPrimitiveType(RawTypeName)
            ? TypeMap.PrimitiveTypes[RawTypeName]
            : isCallback
                ? "string" // When the action is a callback, we require `T` instance and callback names.
                : RawTypeName;

        var parameterName = ToArgumentString();
        var parameterDefault = overrideNullability ? "" : " = null";

        return IsNullable
            ? $"{typeName}? {parameterName}{parameterDefault}"
            : $"{typeName} {parameterName}";
    }

    public string ToActionString(bool isGenericType = false, bool overrideNullability = false)
    {
        if (ActionDeclation is not null)
        {
            var parameterName = ToArgumentString(asDelegate: true);
            var dependentTypes = ActionDeclation.DependentTypes.Keys;
            var parameterDefault = overrideNullability ? "" : " = null";

            return IsNullable
                ? $"Action<{string.Join(", ", dependentTypes)}>? {parameterName}{parameterDefault}"
                : $"Action<{string.Join(", ", dependentTypes)}> {parameterName}";
        }

        return ToParameterString(isGenericType, overrideNullability);
    }

    public string ToArgumentString(bool toJson = false, bool asDelegate = false)
    {
        var isCallback = ActionDeclation is not null;
        var suffix = asDelegate ? "" : "MethodName";
        var parameterName = isCallback
            ? $"on{RawName.CapitalizeFirstLetter()}{suffix}"
            : RawName.LowerCaseFirstLetter();

        return toJson
            ? $"{parameterName}.ToJson(jsonTypeInfo)"
            : parameterName;
    }
}
