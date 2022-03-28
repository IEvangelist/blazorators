// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpMethodExtensions
{
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

    internal static bool IsGenericParameter(string methodName, CSharpType parameter, GeneratorOptions options) =>
        options.GenericMethodDescriptors
            ?.Any(descriptor =>
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

        if (options.IsWebAssembly)
        {
            var returnType = isPrimitiveType
                ? primitiveType
                    : method.IsVoid
                        ? "void"
                        : method.RawReturnTypeName;

            return (returnType, primitiveType);
        }
        else
        {
            var returnType = isPrimitiveType
                ? $"ValueTask<{primitiveType}>"
                    : method.IsVoid
                        ? "ValueTask"
                        : $"ValueTask<{method.RawReturnTypeName}>";

            return (returnType, primitiveType);
        }
    }
}
