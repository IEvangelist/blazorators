// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpMethodExtensions
{
    internal static bool IsJavaScriptOverride(this CSharpMethod method, GeneratorOptions options)
    {
        var methodName = method.RawName.LowerCaseFirstLetter();
        return Array.Exists(options.PureJavaScriptOverrides ?? [], overriddenMethodName => overriddenMethodName == methodName);
    }

    internal static bool IsGenericReturnType(this CSharpMethod method, GeneratorOptions options) =>
        Array.Exists(options.GenericMethodDescriptors ?? [], descriptor =>
        {
            // If the descriptor describes a parameter, it's not a generic return.
            // TODO: consider APIs that might do this.
            if (descriptor.Contains(":"))
            {
                return false;
            }

            // If the descriptor is the method name
            return descriptor == method.RawName;
        });

    internal static bool IsGenericParameter(string methodName, CSharpType parameter, GeneratorOptions options) =>
        Array.Exists(options.GenericMethodDescriptors ?? [], descriptor =>
        {
            if (!descriptor.StartsWith(methodName))
            {
                return false;
            }

            if (descriptor.Contains(":"))
            {
                var nameParamPair = descriptor.Split(':');
                return nameParamPair[1].StartsWith(parameter.RawName);
            }

            return false;
        });

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

        var isJavaScriptOverride = method.IsJavaScriptOverride(options);
        if (options.IsWebAssembly && !isJavaScriptOverride)
        {
            string returnType;
            if (isPrimitiveType) returnType = primitiveType;
            else if (method.IsVoid) returnType = "void";
            else returnType = method.RawReturnTypeName;

            return (returnType, primitiveType);
        }
        else
        {
            string returnType;
            if (isPrimitiveType) returnType = $"ValueTask<{primitiveType}>";
            else if (method.IsVoid) returnType = "ValueTask";
            else returnType = $"ValueTask<{method.RawReturnTypeName}>";

            return (returnType, primitiveType);
        }
    }
}
