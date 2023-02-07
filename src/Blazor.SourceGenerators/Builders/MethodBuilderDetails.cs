// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Builders;

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

    internal static readonly Func<string, string> ToGenericTypeArgument =
        string (string value) => $"<{value}>";

    internal bool IsSerializable => IsGenericReturnType || ContainsGenericParameters;

    internal static MethodBuilderDetails Create(CSharpMethod method, GeneratorOptions options)
    {
        var isGenericReturnType = method.IsGenericReturnType(options);
        var isPrimitiveType = TypeMap.PrimitiveTypes.IsPrimitiveType(method.RawReturnTypeName);
        var containsGenericParameters =
            method.ParameterDefinitions.Any(p => p.IsGenericParameter(method.RawName, options));
        var genericTypeArgs = isGenericReturnType
            ? ToGenericTypeArgument(GenericTypeValue)
            : containsGenericParameters ? ToGenericTypeArgument(GenericTypeValue) : null;
        var fullyQualifiedJavaScriptIdentifier = method.JavaScriptMethodDependency?.InvokableMethodName;
        fullyQualifiedJavaScriptIdentifier ??=
            options.Implementation is not null
                ? $"{options.Implementation}.{method.RawName}"
                : method.RawName;
        var (suffix, extendingType) = options.IsWebAssembly
            ? ("", "IJSInProcessRuntime")
            : ("Async", "IJSRuntime");

        if (method.IsJavaScriptOverride(options) && options.Implementation is not null)
        {
            var impl =
                options.Implementation.Substring(
                    options.Implementation.LastIndexOf(".") + 1);

            fullyQualifiedJavaScriptIdentifier =
                $"blazorators.{impl}.{method.RawName}";

            suffix = "Async";
        }

        var (returnType, bareType) = method.GetMethodTypes(options, isGenericReturnType, isPrimitiveType);

        return new MethodBuilderDetails(
            Method: method,
            IsVoid: method.IsVoid,
            IsPrimitiveType: TypeMap.PrimitiveTypes.IsPrimitiveType(method.RawReturnTypeName),
            IsGenericReturnType: isGenericReturnType,
            ContainsGenericParameters: containsGenericParameters,
            CSharpMethodName: method.RawName.CapitalizeFirstLetter(),
            FullyQualifiedJavaScriptIdentifier: fullyQualifiedJavaScriptIdentifier,
            ReturnType: returnType,
            BareType: bareType,
            Suffix: suffix,
            ExtendingType: extendingType,
            GenericTypeArgs: genericTypeArgs);
    }
}
