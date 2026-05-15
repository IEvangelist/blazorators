// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpMethodExtensions
{
    internal static bool IsJavaScriptOverride(this CSharpMethod method, GeneratorOptions options)
    {
        var methodName = method.RawName.LowerCaseFirstLetter();
        return options?.PureJavaScriptOverrides
            ?.Any(overriddenMethodName => overriddenMethodName == methodName)
            ?? false;
    }

    internal static bool IsGenericReturnType(this CSharpMethod method, GeneratorOptions options) =>
        options.GenericMethodDescriptors
            ?.Any(descriptor =>
            {
                // If the descriptor describes a parameter, it's not a generic return.
                // TODO: consider APIs that might do this.
                if (descriptor.Contains(":"))
                {
                    return false;
                }

                // If the descriptor is the method name
                return descriptor == method.RawName;
            })
            ?? false;

    internal static (string ReturnType, string BareType) GetMethodTypes(
        this CSharpMethod method, GeneratorOptions options, bool isGenericReturnType, bool isPrimitiveType)
    {
        var primitiveType = isPrimitiveType
            ? TypeMap.PrimitiveTypes[method.RawReturnTypeName]
            : method.RawReturnTypeName;

        if (!method.IsVoid && isGenericReturnType)
        {
            var nullable =
                method.IsReturnTypeNullable ? "?" : "";

            return options.IsWebAssembly
                ? ($"{MethodBuilderDetails.GenericTypeValue}{nullable}", primitiveType)
                : ($"ValueTask<{MethodBuilderDetails.GenericTypeValue}{nullable}>", primitiveType);
        }

        // IsVoid must be checked before isPrimitiveType. Once `void` lives in
        // the primitive map (added in T5.2 so dependent-type recursion short-
        // circuits on void return), the `isPrimitiveType` branch otherwise
        // emits `ValueTask<void>` for Server hosting — illegal C#.
        var isJavaScriptOverride = method.IsJavaScriptOverride(options);
        if (options.IsWebAssembly && !isJavaScriptOverride)
        {
            var returnType = method.IsVoid
                ? "void"
                : isPrimitiveType
                    ? primitiveType
                    : method.RawReturnTypeName;

            return (returnType, primitiveType);
        }
        else
        {
            var returnType = method.IsVoid
                ? "ValueTask"
                : isPrimitiveType
                    ? $"ValueTask<{primitiveType}>"
                    : $"ValueTask<{method.RawReturnTypeName}>";

            return (returnType, primitiveType);
        }
    }
}
