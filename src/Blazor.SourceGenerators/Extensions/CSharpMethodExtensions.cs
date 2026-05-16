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
                if (descriptor.IndexOf(':') >= 0)
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
        // Resolve the C# spelling of the bare return type. For scalar
        // primitives this is a direct map lookup; for array-of-primitive
        // (e.g. `number[]` -> `double[]`) we strip the suffix, map the
        // element, and reattach. Without the array branch, the emitter
        // dropped the raw TypeScript name into both the method signature
        // and the `_javaScript.Invoke...<BareType>()` call, producing
        // invalid C#.
        //
        // The TS `| null` clause is peeled here so that custom-type
        // nullable returns (`Node | null`, `HTMLElement | null`) emit
        // `Node?` / `HTMLElement?` rather than the verbatim TS spelling
        // (which is not legal C#). For scalar primitives the direct map
        // already carries explicit `"T | null" -> "T?"` entries, so the
        // `isPrimitiveType` branch short-circuits before reaching the
        // peel. For array-of-primitive nullables (`number[] | null`,
        // etc.) and array-of-custom nullables (`Node[] | null`) the peel
        // routes through the array-element resolution and re-attaches
        // `?` at the end.
        //
        // For `Promise<T>` return types the wrapper is peeled here and
        // forwarded into the same primitive / array / nullable resolution
        // pipeline. `IsPromise` also forces the async invocation path
        // below: even under WebAssembly the wrapped value can't resolve
        // synchronously, so the method must return `ValueTask<T>` (or
        // `ValueTask` for `Promise<void>`) and call `InvokeAsync`.
        var rawBare = method.IsPromise
            ? method.PromiseUnwrappedTypeName
            : method.RawReturnTypeName;
        var rawBareIsPrimitive = method.IsPromise
            ? TypeMap.PrimitiveTypes.IsPrimitiveType(rawBare)
            : isPrimitiveType;

        string primitiveType;
        if (rawBareIsPrimitive)
        {
            primitiveType = TypeMap.PrimitiveTypes[rawBare];
        }
        else
        {
            var bare = TypeShape.StripNullClause(rawBare);
            var hadNullClause = bare.Length != rawBare.Length;

            string resolved;
            if (TypeShape.TryGetArrayElementTypeName(bare, out var arrayElement)
                && TypeMap.PrimitiveTypes.IsPrimitiveType(arrayElement))
            {
                resolved = $"{TypeMap.PrimitiveTypes[arrayElement]}[]";
            }
            else
            {
                resolved = bare;
            }

            primitiveType = hadNullClause ? $"{resolved}?" : resolved;
        }

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
        if (options.IsWebAssembly && !isJavaScriptOverride && !method.IsPromise)
        {
            var returnType = method.IsVoid
                ? "void"
                : primitiveType;

            return (returnType, primitiveType);
        }
        else
        {
            var returnType = method.IsVoid
                ? "ValueTask"
                : $"ValueTask<{primitiveType}>";

            return (returnType, primitiveType);
        }
    }
}
