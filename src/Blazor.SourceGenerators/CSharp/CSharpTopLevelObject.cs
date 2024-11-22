// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.CSharp;

internal sealed partial record CSharpTopLevelObject(string RawTypeName)
    : ICSharpDependencyGraphObject
{
    public List<CSharpProperty>? Properties { get; init; } = [];

    public List<CSharpMethod>? Methods { get; init; } = [];

    public Dictionary<string, CSharpObject> DependentTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            Dictionary<string, CSharpObject> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (var prop
                in DependentTypes
                    .Select(kvp => (TypeName: kvp.Key, Object: kvp.Value))
                    .Concat(Properties.SelectMany(
                        p => p.AllDependentTypes))
                    .Concat(Methods.SelectMany(
                        p => p.AllDependentTypes)))
            {
                result[prop.TypeName] = prop.Object;
            }

            return result.Select(pair => (pair.Key, pair.Value))
                .ToImmutableHashSet();
        }
    }

    public int MemberCount => Properties!.Count + Methods!.Count;

    internal string ToInterfaceString(
        GeneratorOptions options,
        string? namespaceString)
    {
        var builder = new SourceBuilder(options)
            .AppendCopyRightHeader()
            .AppendUsingDeclarations()
            .AppendNamespace(namespaceString ?? "Microsoft.JSInterop")
            .AppendPublicInterfaceDeclaration()
            .AppendOpeningCurlyBrace()
            .IncreaseIndentation();

        var methodLevel = builder.IndentationLevel;

        // Methods
        foreach (var method in Methods ?? [])
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentiationTo(methodLevel);

            var isJavaScriptOverride = method.IsJavaScriptOverride(options);
            var isPureNonBiDirectionalOrOverriddenJS =
                method.IsPureJavaScriptInvocation ||
                method.IsNotBiDirectionalJavaScript ||
                isJavaScriptOverride;

            if (isPureNonBiDirectionalOrOverriddenJS)
            {
                builder.AppendTripleSlashMethodComments(details.Method)
                    .AppendRaw(
                        $"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(",
                        appendNewLine: false,
                        postIncreaseIndentation: true);

                if (method.ParameterDefinitions.Count > 0)
                {
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (pi.IsLast)
                        {
                            if (details.IsSerializable)
                            {
                                builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},")
                                    .AppendRaw($"JsonSerializerOptions? options = null);")
                                    .AppendLine();
                            }
                            else
                            {
                                builder.AppendRaw($"{parameter.ToParameterString(isGenericType)});")
                                    .AppendLine();
                            }
                        }
                        else
                        {
                            if (pi.IsFirst)
                            {
                                builder.AppendLine();
                            }

                            builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},");
                        }
                    }

                    builder.DecreaseIndentation();
                }
                else
                {
                    builder.AppendRaw(");", appendNewLine: true, omitIndentation: true);
                }
            }
            else if (options.OnlyGeneratePureJS is false)
            {
                var genericTypeArgs = details.GenericTypeArgs ??
                    MethodBuilderDetails.ToGenericTypeArgument(
                        MethodBuilderDetails.GenericComponentType);

                builder.AppendTripleSlashMethodComments(details.Method, extrapolateParameters: true)
                    .AppendRaw(
                        $"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(")
                    .AppendRaw($"TComponent component", appendNewLine: false, postIncreaseIndentation: true);

                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.AppendRaw(",");
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (pi.IsLast)
                        {
                            builder.AppendRaw($"{parameter.ToParameterString(isGenericType)}) where TComponent : class;")
                                .AppendLine();
                        }
                        else
                        {
                            if (pi.IsFirst)
                            {
                                builder.AppendLine();
                            }

                            builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},");
                        }
                    }

                    builder.DecreaseIndentation();
                }
                else
                {
                    builder.AppendRaw(") where TComponent : class;", appendNewLine: true, omitIndentation: true);
                }

                builder.AppendTripleSlashMethodComments(details.Method)
                    .AppendRaw(
                        $"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}(",
                        postIncreaseIndentation: true);

                if (method.ParameterDefinitions.Count > 0)
                {
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (pi.IsLast)
                        {
                            builder.AppendRaw($"{parameter.ToActionString(isGenericType)});")
                                .AppendLine();
                        }
                        else
                        {
                            if (pi.IsFirst)
                            {
                                builder.AppendLine();
                            }

                            builder.AppendRaw($"{parameter.ToActionString(isGenericType)},");
                        }
                    }

                    builder.DecreaseIndentation();
                }
                else
                {
                    builder.AppendRaw(");", appendNewLine: true, omitIndentation: true);
                }
            }
        }

        // Properties
        foreach (var (index, property) in (Properties ?? []).Select())
        {
            if (index.IsFirst) builder.AppendLine();
            if (property.IsIndexer) continue;

            builder.ResetIndentiationTo(methodLevel);

            var details = PropertyBuilderDetails.Create(property, options);

            var accessors = details.Property.IsReadonly
                ? "{ get; }" : "{ get; set; }";
            builder.AppendTripleSlashPropertyComments(details.Property)
                .AppendRaw($"{details.ReturnType} {details.CSharpPropertyName} {accessors}");

            if (!index.IsLast)
            {
                builder.AppendLine();
            }
        }

        builder.ResetIndentiationTo(0);
        builder.AppendClosingCurlyBrace();

        var interfaceDeclaration = TryFormatCSharpSourceText(builder.ToSourceCodeString());
        return interfaceDeclaration;
    }

    internal string ToImplementationString(
        GeneratorOptions options,
        string? namespaceString)
    {
        var builder = new SourceBuilder(options)
            .AppendCopyRightHeader()
            .AppendUsingDeclarations()
            .AppendNamespace(namespaceString ?? "Microsoft.JSInterop")
            .AppendInternalImplementationDeclaration()
            .AppendOpeningCurlyBrace()
            .IncreaseIndentation()
            .AppendConditionalDelegateFields(Methods)
            .AppendImplementationCtor();

        var methodLevel = builder.IndentationLevel;

        builder.AppendConditionalDelegateCallbackMethods(Methods);

        // Methods
        foreach (var (index, method) in (Methods ?? []).Select())
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentiationTo(methodLevel);

            var isJavaScriptOverride = method.IsJavaScriptOverride(options);
            var isPureNonBiDirectionalOrOverriddenJS =
                method.IsPureJavaScriptInvocation ||
                method.IsNotBiDirectionalJavaScript ||
                isJavaScriptOverride;

            if (isPureNonBiDirectionalOrOverriddenJS)
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
                                builder.AppendRaw($"JsonSerializerOptions? options){genericTypeParameterConstraint} =>");
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
                    // Write method body / expression, and arguments to javaScript.Invoke*
                    foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (ai.IsLast)
                        {
                            if (details.IsGenericReturnType)
                            {
                                // Overridden to control explicitly
                                builder.AppendRaw($"{parameter.ToArgumentString(toJson: false)})");
                                builder.AppendRaw($".FromJson{details.GenericTypeArgs}(options);");
                            }
                            else
                            {
                                builder.AppendRaw($"{parameter.ToArgumentString(details.ContainsGenericParameters)});");
                            }

                            if (!index.IsLast) builder.AppendLine();
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
            else if (options.OnlyGeneratePureJS is false)
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

                    // Write method body / expression, and arguments to javaScript.Invoke*
                    foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (ai.IsLast)
                        {
                            builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)});");

                            if (!index.IsLast) builder.AppendLine();
                        }
                        else
                        {
                            builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)},");
                        }
                    }

                    builder.DecreaseIndentation();
                }

                builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
                    .AppendRaw(
                        $"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}(",
                        postIncreaseIndentation: true);

                if (method.ParameterDefinitions.Count > 0)
                {
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
                        var fieldName =
                            builder.Fields?.FirstOrDefault(field => field.EndsWith(parameter.RawName));

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

                    // Write method body / expression, and arguments to javaScript.Invoke*
                    foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
                    {
                        if (ai.IsFirst)
                        {
                            builder.AppendRaw($"DotNetObjectReference.Create(this),");
                        }

                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        var arg = parameter.ToArgumentString(isGenericType, true);
                        var methodName =
                            builder.Methods?.FirstOrDefault(
                                method => method.EndsWith(arg.Substring(2)));
                        var argExpression = methodName is not null ? $"nameof({methodName})" : arg;
                        if (ai.IsLast)
                        {
                            builder.AppendRaw($"{argExpression});");
                            builder.AppendClosingCurlyBrace();

                            if (!index.IsLast) builder.AppendLine();
                        }
                        else
                        {
                            builder.AppendRaw($"{argExpression},");
                        }
                    }

                    builder.DecreaseIndentation();
                }
            }
        }

        // Properties
        foreach (var (index, property) in (Properties ?? []).Select())
        {
            if (index.IsFirst) builder.AppendLine();
            if (property.IsIndexer) continue;

            builder.ResetIndentiationTo(methodLevel);

            var details = PropertyBuilderDetails.Create(property, options);

            builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, details.CSharpPropertyName)
                .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpPropertyName} =>", postIncreaseIndentation: true)
                .AppendRaw($"_javaScript.Invoke{details.Suffix}{details.GenericTypeArgs}(", postIncreaseIndentation: true)
                .AppendRaw($"\"eval\", \"{details.FullyQualifiedJavaScriptIdentifier}\");");

            if (!index.IsLast)
            {
                builder.AppendLine();
            }
        }

        builder.ResetIndentiationTo(0);
        builder.AppendClosingCurlyBrace();

        var implementation = TryFormatCSharpSourceText(builder.ToSourceCodeString());
        return implementation;
    }

    internal string ToServiceCollectionExtensions(
        GeneratorOptions options,
        string implementation)
    {
        var serviceLifetime = options.IsWebAssembly
            ? "Singleton"
            : "Scoped";
        var addExpression = options.IsWebAssembly
            ? $@"services.Add{serviceLifetime}<IJSInProcessRuntime>(serviceProvider =>
            (IJSInProcessRuntime)serviceProvider.GetRequiredService<IJSRuntime>())
            "
            : "services";

        var @interface = options.Implementation.ToInterfaceName();
        var nonService = options.Implementation.ToImplementationName(false);

        var extensions = $@"// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License:
// https://github.com/IEvangelist/blazorators/blob/main/LICENSE
// Auto-generated by blazorators.

using Microsoft.JSInterop;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary></summary>
public static class {nonService}ServiceCollectionExtensions
{{
    /// <summary>
    /// Adds the <see cref=""{@interface}"" /> service to the service collection.
    /// </summary>
    public static IServiceCollection Add{nonService}Services(
        this IServiceCollection services) =>
        {addExpression}.Add{serviceLifetime}<{@interface}, {implementation}>();
}}
";

        return extensions;
    }

    static string TryFormatCSharpSourceText(string csharpSourceText)
    {
        try
        {
            return CSharpSyntaxTree.ParseText(csharpSourceText)
                .GetRoot()
                .NormalizeWhitespace()
                .ToFullString();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);

            return csharpSourceText;
        }
    }
}
