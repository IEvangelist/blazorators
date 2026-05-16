// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.CSharp;

internal record CSharpType(
    string RawName,
    string RawTypeName,
    bool IsNullable = false,
    CSharpAction? ActionDeclaration = null) : ICSharpDependencyGraphObject
{
    public Dictionary<string, CSharpObject> DependentTypes
    {
        get
        {
            Dictionary<string, CSharpObject> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in ActionDeclaration?.DependentTypes
                ?? Enumerable.Empty<KeyValuePair<string, CSharpObject>>())
            {
                result[prop.Key] = prop.Value;
            }

            if (ActionDeclaration is { ParameterDefinitions.Count: > 0 })
            {
                foreach (var type in ActionDeclaration.ParameterDefinitions)
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

    private IImmutableSet<(string TypeName, CSharpObject Object)>? _allDependentTypes;
    private bool _isComputingAllDependentTypes;

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            if (_allDependentTypes is not null)
            {
                return _allDependentTypes;
            }

            if (_isComputingAllDependentTypes)
            {
                return ImmutableHashSet<(string, CSharpObject)>.Empty;
            }

            _isComputingAllDependentTypes = true;
            try
            {
                return _allDependentTypes = DependentTypes
                    .SelectMany(kvp => kvp.Value.AllDependentTypes)
                    .ToImmutableHashSet();
            }
            finally
            {
                _isComputingAllDependentTypes = false;
            }
        }
    }

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

        var isCallback = ActionDeclaration is not null;
        string typeName;
        if (TypeMap.PrimitiveTypes.IsPrimitiveType(RawTypeName))
        {
            typeName = TypeMap.PrimitiveTypes[RawTypeName];
        }
        else if (isCallback)
        {
            // When the action is a callback, we require `T` instance and callback names.
            typeName = "string";
        }
        else if (TypeShape.TryGetArrayElementTypeName(RawTypeName, out var elementTypeName) &&
                 TypeMap.PrimitiveTypes.IsPrimitiveType(elementTypeName))
        {
            // Array of TS primitive (e.g. `number[]`) -- map the element to its
            // C# spelling and re-attach `[]`. Without this, the emitter dropped
            // the raw TypeScript name (`number[] segments`) into the generated
            // method signature, which is not valid C#.
            typeName = $"{TypeMap.PrimitiveTypes[elementTypeName]}[]";
        }
        else
        {
            typeName = RawTypeName;
        }

        var parameterName = ToArgumentString();
        var parameterDefault = overrideNullability ? "" : " = null";

        return IsNullable
            ? $"{typeName}? {parameterName}{parameterDefault}"
            : $"{typeName} {parameterName}";
    }

    public string ToActionString(bool isGenericType = false, bool overrideNullability = false)
    {
        if (ActionDeclaration is not null)
        {
            var parameterName = ToArgumentString(asDelegate: true);
            var dependentTypes = ActionDeclaration.DependentTypes.Keys;
            var parameterDefault = overrideNullability ? "" : " = null";

            // For a zero-parameter callback (e.g. the TS `VoidFunction`
            // interface, `(): void;`) there is no dependent type list to
            // splice in -- emit the non-generic `Action`. Otherwise the
            // generator produced `Action<>? on... = null` which does not
            // compile.
            var actionType = dependentTypes.Count == 0
                ? "Action"
                : $"Action<{string.Join(", ", dependentTypes)}>";

            return IsNullable
                ? $"{actionType}? {parameterName}{parameterDefault}"
                : $"{actionType} {parameterName}";
        }

        return ToParameterString(isGenericType, overrideNullability);
    }

    public string ToArgumentString(bool toJson = false, bool asDelegate = false)
    {
        var isCallback = ActionDeclaration is not null;
        var suffix = asDelegate ? "" : "MethodName";
        var parameterName = isCallback
            ? $"on{RawName.CapitalizeFirstLetter()}{suffix}"
            : RawName.LowerCaseFirstLetter();

        return toJson
            ? $"{parameterName}.ToJson(jsonTypeInfo)"
            : parameterName;
    }
}
