// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Options;

namespace Blazor.SourceGenerators.Builders;

/// <summary>
/// Represents the details of a method builder, including information about the method's return type, name, and parameters.
/// </summary>
/// <param name="Method">The <see cref="CSharpMethod"/> to generate code for.</param>
/// <param name="IsVoid">A value indicating whether the method returns <see langword="void"/>.</param>
/// <param name="IsPrimitiveType">A value indicating whether the method returns a primitive type.</param>
/// <param name="IsGenericReturnType">A value indicating whether the method returns a generic type.</param>
/// <param name="ContainsGenericParameters">A value indicating whether the method contains generic parameters.</param>
/// <param name="CSharpMethodName">The name of the method.</param>
/// <param name="FullyQualifiedJavaScriptIdentifier">The fully qualified JavaScript identifier.</param>
/// <param name="ReturnType">The return type of the method.</param>
/// <param name="BareType">The bare type of the method.</param>
/// <param name="Suffix">The suffix to append to the method name.</param>
/// <param name="ExtendingType">The type to extend.</param>
/// <param name="GenericTypeArgs">The generic type arguments.</param>
internal readonly record struct MethodBuilderDetails(
    CSharpMethod Method,
    bool IsVoid,
    bool IsPrimitiveType,
    bool IsGenericReturnType,
    bool ContainsGenericParameters,
    string CSharpMethodName,
    string FullyQualifiedJavaScriptIdentifier,
    string ReturnType,
    string BareType,
    string Suffix,
    string ExtendingType,
    string? GenericTypeArgs)
{
    /// <summary>
    /// A value representing the generic return type, <c>"TValue"</c>.
    /// </summary>
    internal static readonly string GenericTypeValue = "TValue";

    /// <summary>
    /// A value representing the generic component type, <c>"TComponent"</c>.
    /// </summary>
    internal const string GenericComponentType = "TComponent";

    /// <summary>
    /// Returns a string representing a generic type argument with the specified value.
    /// </summary>
    internal static readonly Func<string, string> ToGenericTypeArgument = static string (string value) => $"<{value}>";

    /// <summary>
    /// Gets a value indicating whether the method's return type is serializable.
    /// </summary>
    internal bool IsSerializable => IsGenericReturnType || ContainsGenericParameters;

    /// <summary>
    /// Creates a new instance of <see cref="MethodBuilderDetails"/> based on the provided <see cref="CSharpMethod"/> and <see cref="GeneratorOptions"/>.
    /// </summary>
    /// <param name="method">The <see cref="CSharpMethod"/> to create the <see cref="MethodBuilderDetails"/> from.</param>
    /// <param name="options">The <see cref="GeneratorOptions"/> to use when creating the <see cref="MethodBuilderDetails"/>.</param>
    /// <returns>A new instance of <see cref="MethodBuilderDetails"/> based on the provided <see cref="CSharpMethod"/> and <see cref="GeneratorOptions"/>.</returns>
    internal static MethodBuilderDetails Create(CSharpMethod method, GeneratorOptions options)
    {
        var isGenericReturnType = method.IsGenericReturnType(options);
        var isPrimitiveType = Primitives.IsPrimitiveType(method.RawReturnTypeName);
        var containsGenericParameters = method.ParameterDefinitions.Any(p => p.IsGenericParameter(method.RawName, options));

        var genericTypeArgs = DetermineGenericTypeArgs(isGenericReturnType, containsGenericParameters);
        var fullyQualifiedJavaScriptIdentifier = DetermineJavaScriptIdentifier(method, options);
        (var suffix, var extendingType) = DetermineSuffixAndExtendingType(method, options);
        (var returnType, var bareType) = method.GetMethodTypes(options, isGenericReturnType, isPrimitiveType);

        return new MethodBuilderDetails(
            Method: method,
            IsVoid: method.IsVoid,
            IsPrimitiveType: isPrimitiveType,
            IsGenericReturnType: isGenericReturnType,
            ContainsGenericParameters: containsGenericParameters,
            CSharpMethodName: method.RawName.CapitalizeFirstLetter(),
            FullyQualifiedJavaScriptIdentifier: fullyQualifiedJavaScriptIdentifier,
            ReturnType: returnType,
            BareType: bareType,
            Suffix: suffix,
            ExtendingType: extendingType,
            GenericTypeArgs: genericTypeArgs
        );
    }

    private static string? DetermineGenericTypeArgs(bool isGenericReturnType, bool containsGenericParameters)
    {
        if (isGenericReturnType) return ToGenericTypeArgument(GenericTypeValue);
        if (containsGenericParameters) return ToGenericTypeArgument(GenericTypeValue);
        return null;
    }

    private static string DetermineJavaScriptIdentifier(CSharpMethod method, GeneratorOptions options)
    {
        if (method.IsJavaScriptOverride(options) && options.Implementation is not null)
        {
            var impl = options.Implementation.Substring(options.Implementation.LastIndexOf(".") + 1);
            return $"blazorators.{impl}.{method.RawName}";
        }
        return method.JavaScriptMethodDependency?.InvokableMethodName ??
            (options.Implementation is not null ? $"{options.Implementation}.{method.RawName}" : method.RawName);
    }

    private static (string Suffix, string ExtendingType) DetermineSuffixAndExtendingType(CSharpMethod method, GeneratorOptions options)
    {
        if (method.IsJavaScriptOverride(options) && options.Implementation is not null)
        {
            return ("Async", "IJSRuntime");
        }
        return options.IsWebAssembly ? ("", "IJSInProcessRuntime") : ("Async", "IJSRuntime");
    }
}