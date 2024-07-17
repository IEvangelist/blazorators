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
            var result = new Dictionary<string, CSharpObject>(StringComparer.OrdinalIgnoreCase);

            // Add dependent types from ActionDeclation
            if (ActionDeclation is not null)
            {
                foreach (var prop in ActionDeclation.DependentTypes)
                {
                    result[prop.Key] = prop.Value;
                }

                foreach (var type in ActionDeclation.ParameterDefinitions ?? [])
                {
                    foreach (var pair in type.DependentTypes.SelectMany(dt => dt.Value.AllDependentTypes))
                    {
                        result[pair.TypeName] = pair.Object;
                    }
                }
            }

            return result;
        }
    }

    public IImmutableSet<DependentType> AllDependentTypes => DependentTypes
        .Select(kvp => new DependentType(kvp.Key, kvp.Value))
        .ToImmutableHashSet(DependentTypeComparer.Default);

    /// <summary>
    /// Gets a string representation of the C# type as a parameter declaration.
    /// </summary>
    public string ToParameterString(bool isGenericType = false, bool overrideNullability = false)
    {
        if (isGenericType)
        {
            return $"{MethodBuilderDetails.GenericTypeValue}{(IsNullable ? "?" : "")} {ToArgumentString()}";
        }

        var isCallback = ActionDeclation is not null;
        string typeName;

        if (Primitives.IsPrimitiveType(RawTypeName)) typeName = Primitives.Instance[RawTypeName];
        else if (isCallback) typeName = "string";
        else typeName = RawTypeName;

        var parameterName = ToArgumentString();
        var parameterDefault = overrideNullability ? "" : " = null";

        return IsNullable
            ? $"{typeName}? {parameterName}{parameterDefault}"
            : $"{typeName} {parameterName}";
    }

    /// <summary>
    /// Gets a string representation of the C# type as an action declaration.
    /// </summary>
    public string ToActionString(bool isGenericType = false, bool overrideNullability = false)
    {
        if (ActionDeclation is not null)
        {
            var parameterName = ToArgumentString(asDelegate: true);
            var dependentTypes = string.Join(", ", ActionDeclation.DependentTypes.Keys);
            var parameterDefault = overrideNullability ? "" : " = null";

            return IsNullable
                ? $"Action<{dependentTypes}>? {parameterName}{parameterDefault}"
                : $"Action<{dependentTypes}> {parameterName}";
        }

        return ToParameterString(isGenericType, overrideNullability);
    }

    /// <summary>
    /// Gets the argument string representation of the C# type.
    /// </summary>
    public string ToArgumentString(bool toJson = false, bool asDelegate = false)
    {
        var isCallback = ActionDeclation is not null;
        var suffix = asDelegate ? "" : "MethodName";
        var parameterName = isCallback
            ? $"on{RawName.CapitalizeFirstLetter()}{suffix}"
            : RawName.LowerCaseFirstLetter();

        return toJson ? $"{parameterName}.ToJson(options)" : parameterName;
    }
}