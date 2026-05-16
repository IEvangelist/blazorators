// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;
using Blazor.SourceGenerators.Extensions;

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// Per-method emission helpers extracted from <see cref="CSharpTopLevelObject.ToImplementationString"/>
/// (T3.8). The previous monolithic method was ~286 lines deep with three structurally
/// distinct branches:
///
///   1. <see cref="EmitPureInvocationMethod"/>   - "pure" / non-bi-directional / JS-override invocations
///   2. <see cref="EmitGenericComponentMethod"/> - generic <c>TComponent</c> overload (when bi-directional)
///   3. <see cref="EmitActionComponentMethod"/>  - <c>Action&lt;T&gt;</c>-callback overload (when bi-directional)
///
/// Splitting them out doesn't change emit semantics - the snapshot suite
/// (`GeneratorSnapshotTests`) pins the byte-for-byte output - it just makes
/// each branch comprehensible on its own.
/// </summary>
internal sealed partial record CSharpTopLevelObject
{
    private static void EmitPureInvocationMethod(
        SourceBuilder builder,
        CSharpMethod method,
        MethodBuilderDetails details,
        GeneratorOptions options,
        bool isLastMethod)
    {
        var memberName = $"{details.CSharpMethodName}{details.Suffix}";
        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
            .AppendRaw(
                $"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(",
                appendNewLine: false,
                postIncreaseIndentation: true);

        if (method.ParameterDefinitions.Count > 0)
        {
            var genericTypeParameterConstraint = details.IsGenericReturnType
                ? $" where {MethodBuilderDetails.GenericTypeValue} : default"
                : "";

            foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
            {
                var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                if (pi.IsLast)
                {
                    if (details.IsSerializable)
                    {
                        builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},");
                        builder.AppendRaw($"JsonTypeInfo<{MethodBuilderDetails.GenericTypeValue}>? jsonTypeInfo){genericTypeParameterConstraint} =>");
                    }
                    else
                    {
                        builder.AppendRaw($"{parameter.ToParameterString(false, true)}) =>");
                    }
                }
                else
                {
                    builder.AppendRaw($"{parameter.ToParameterString(isGenericType, true)},");
                }
            }

            if (details.IsVoid)
            {
                builder.AppendRaw($"_javaScript.InvokeVoid{details.Suffix}(", postIncreaseIndentation: true);
            }
            else
            {
                builder.AppendRaw($"_javaScript.Invoke{details.Suffix}<{details.BareType}>(", postIncreaseIndentation: true);
            }

            builder.IncreaseIndentation()
                .AppendRaw($"\"{details.FullyQualifiedJavaScriptIdentifier}\",");

            foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
            {
                var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                if (ai.IsLast)
                {
                    if (details.IsGenericReturnType)
                    {
                        builder.AppendRaw($"{parameter.ToArgumentString(toJson: false)})");
                        builder.AppendRaw($".FromJson{details.Suffix}{details.GenericTypeArgs}(jsonTypeInfo);");
                    }
                    else
                    {
                        builder.AppendRaw($"{parameter.ToArgumentString(details.ContainsGenericParameters)});");
                    }

                    if (!isLastMethod) builder.AppendLine();
                }
                else
                {
                    builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)},");
                }
            }

            builder.DecreaseIndentation();
        }
        else
        {
            builder.AppendRaw(") =>");
            if (details.IsVoid)
            {
                builder.AppendRaw($"_javaScript.InvokeVoid{details.Suffix}(\"{details.FullyQualifiedJavaScriptIdentifier}\");");
                builder.AppendLine();
            }
            else
            {
                builder.AppendRaw($"_javaScript.Invoke{details.Suffix}<{details.BareType}>(\"{details.FullyQualifiedJavaScriptIdentifier}\");");
                builder.AppendLine();
            }
        }
    }

    private static void EmitGenericComponentMethod(
        SourceBuilder builder,
        CSharpMethod method,
        MethodBuilderDetails details,
        GeneratorOptions options,
        bool isLastMethod)
    {
        var genericTypeArgs = details.GenericTypeArgs ??
            MethodBuilderDetails.ToGenericTypeArgument(
                MethodBuilderDetails.GenericComponentType);

        var memberName = $"{details.CSharpMethodName}{details.Suffix}";
        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
            .AppendRaw(
                $"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(",
                postIncreaseIndentation: true)
            .AppendRaw($"TComponent component", appendNewLine: false);

        if (method.ParameterDefinitions.Count > 0)
        {
            builder.AppendRaw(
                ", ", false, false, true);
            foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
            {
                if (pi.IsLast)
                {
                    builder.AppendRaw($"{parameter.ToParameterString(false, true)}) where TComponent : class =>");
                }
                else
                {
                    builder.AppendRaw($"{parameter.ToParameterString(false, true)},");
                }
            }

            if (details.IsVoid)
            {
                builder.AppendRaw($"_javaScript.InvokeVoid{details.Suffix}(");
            }
            else
            {
                builder.AppendRaw($"_javaScript.Invoke{details.Suffix}<{details.BareType}>(");
            }

            builder.IncreaseIndentation()
                .AppendRaw($"\"{details.FullyQualifiedJavaScriptIdentifier}\",");

            builder.AppendRaw($"DotNetObjectReference.Create(component),");

            foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
            {
                var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                if (ai.IsLast)
                {
                    builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)});");

                    if (!isLastMethod) builder.AppendLine();
                }
                else
                {
                    builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)},");
                }
            }

            builder.DecreaseIndentation();
        }
    }

    private static void EmitActionComponentMethod(
        SourceBuilder builder,
        CSharpMethod method,
        MethodBuilderDetails details,
        GeneratorOptions options,
        bool isLastMethod)
    {
        var memberName = $"{details.CSharpMethodName}{details.Suffix}";
        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
            .AppendRaw(
                $"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}(",
                postIncreaseIndentation: true);

        if (method.ParameterDefinitions.Count == 0)
        {
            return;
        }

        foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
        {
            if (pi.IsLast)
            {
                builder.AppendRaw($"{parameter.ToActionString(false, true)})");
                builder.AppendOpeningCurlyBrace();
            }
            else
            {
                builder.AppendRaw($"{parameter.ToActionString(false, true)},");
            }
        }

        foreach (var parameter in method.ParameterDefinitions)
        {
            var isGenericType = parameter.IsGenericParameter(method.RawName, options);
            var arg = parameter.ToArgumentString(isGenericType, true);
            // Match `_paramName` fields to the parameter by ordinal
            // suffix. Without `StringComparison.Ordinal` the comparison
            // would be culture-sensitive on the host machine -- analyzers
            // run in the build process and inherit its culture, so a
            // Turkish locale (the classic `i` / `I` example) would alter
            // the match results.
            var fieldName =
                builder.Fields?.FirstOrDefault(field => field.EndsWith(parameter.RawName, StringComparison.Ordinal));

            if (fieldName is null) continue;
            builder.AppendRaw($"{fieldName} = {arg};");
        }

        if (details.IsVoid)
        {
            var returnExpression = options.IsWebAssembly ? "" : "return ";
            builder.AppendRaw($"{returnExpression}_javaScript.InvokeVoid{details.Suffix}(");
        }
        else
        {
            builder.AppendRaw($"return _javaScript.Invoke{details.Suffix}<{details.BareType}>(");
        }

        builder.IncreaseIndentation()
            .AppendRaw($"\"{details.FullyQualifiedJavaScriptIdentifier}\",");

        foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
        {
            if (ai.IsFirst)
            {
                builder.AppendRaw($"DotNetObjectReference.Create(this),");
            }

            var isGenericType = parameter.IsGenericParameter(method.RawName, options);
            var arg = parameter.ToArgumentString(isGenericType, true);
            // Strip the leading `on` prefix produced by
            // `ToArgumentString` for callback parameters (e.g. `onSuccess`
            // -> `Success`) so the suffix lines up with the `[JSInvokable]`
            // method name (`OnSuccess`). For non-callback parameters the
            // argument string is the raw parameter name, which won't
            // start with `on` -- and may be shorter than two characters
            // (real DOM signatures include single-letter parameters such
            // as `(x: number)`), so `arg.Substring(2)` previously threw
            // `ArgumentOutOfRangeException`. Guard against the short-name
            // case and use ordinal comparison to keep the lookup
            // culture-invariant inside a build-host analyzer.
            var methodSuffix = arg.Length >= 2 ? arg.Substring(2) : arg;
            var methodName =
                builder.Methods?.FirstOrDefault(
                    m => m.EndsWith(methodSuffix, StringComparison.Ordinal));
            var argExpression = methodName is not null ? $"nameof({methodName})" : arg;
            if (ai.IsLast)
            {
                builder.AppendRaw($"{argExpression});");
                builder.AppendClosingCurlyBrace();

                if (!isLastMethod) builder.AppendLine();
            }
            else
            {
                builder.AppendRaw($"{argExpression},");
            }
        }

        builder.DecreaseIndentation();
    }

    private static void EmitImplementationProperty(
        SourceBuilder builder,
        CSharpProperty property,
        GeneratorOptions options,
        bool isFirstProperty,
        bool isLastProperty,
        int methodLevel)
    {
        if (isFirstProperty) builder.AppendLine();
        if (property.IsIndexer) return;

        builder.ResetIndentationTo(methodLevel);

        var details = PropertyBuilderDetails.Create(property, options);

        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, details.CSharpPropertyName)
            .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpPropertyName} =>", postIncreaseIndentation: true)
            .AppendRaw($"_javaScript.Invoke{details.Suffix}{details.GenericTypeArgs}(", postIncreaseIndentation: true)
            .AppendRaw($"\"eval\", \"{details.FullyQualifiedJavaScriptIdentifier}\");");

        if (!isLastProperty)
        {
            builder.AppendLine();
        }
    }
}
