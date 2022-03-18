// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;

namespace Blazor.SourceGenerators.CSharp;

internal sealed partial record CSharpExtensionObject(string RawTypeName)
    : ICSharpDependencyGraphObject
{
    public List<CSharpProperty>? Properties { get; init; } = new();

    public List<CSharpMethod>? Methods { get; init; } = new();

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

    internal string ToStaticPartialClassString(
        GeneratorOptions options,
        string existingClassName,
        string? namespaceString,
        bool? isPublic = null)
    {
        var builder = new SourceBuilder(options)
            .AppendCopyRightHeader()
            .AppendUsingDeclarations()
            .AppendNamespace(namespaceString ?? "Microsoft.JSInterop")
            .AppendStaticPartialClassDeclaration(existingClassName, isPublic.GetValueOrDefault() ? "public" : null)
            .AppendOpeningCurlyBrace()
            .IncreaseIndentation();

        var methodLevel = builder.IndentationLevel;

        // Add methods.
        foreach (var (index, method) in (Methods ?? new List<CSharpMethod>()).Select())
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentiationTo(methodLevel);

            if (method.IsPureJavaScriptInvocation)
            {
                builder.AppendTripleSlashMethodComments(method)
                    .AppendRaw($"public static {details.ReturnType} {details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(", postIncreaseIndentation: true)
                    .AppendRaw($"this {details.ExtendingType} javaScript", appendNewLine: false);

                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.AppendRaw(",");
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (pi.IsLast)
                        {
                            if (details.IsSerializable)
                            {
                                builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},");
                                builder.AppendRaw($"JsonSerializerOptions? options = null) =>");
                            }
                            else
                            {
                                builder.AppendRaw($"{parameter.ToParameterString(isGenericType)}) =>");
                            }
                        }
                        else
                        {
                            builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},");
                        }
                    }

                    if (details.IsVoid)
                    {
                        builder.AppendRaw($"javaScript.InvokeVoid{details.Suffix}(");
                    }
                    else
                    {
                        builder.AppendRaw($"javaScript.Invoke{details.Suffix}<{details.BareType}>(");
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
                                builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)})");
                                builder.AppendRaw($".FromJson{details.GenericTypeArgs}(options);");
                            }
                            else
                            {
                                builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)});");
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
                        builder.AppendRaw($"javaScript.InvokeVoid{details.Suffix}(\"{details.FullyQualifiedJavaScriptIdentifier}\");");
                        builder.AppendLine();
                    }
                    else
                    {
                        builder.AppendRaw($"javaScript.Invoke{details.Suffix}<{details.BareType}>(\"{details.FullyQualifiedJavaScriptIdentifier}\");");
                        builder.AppendLine();
                    }
                }
            }
            else if (options.OnlyGeneratePureJS is false)
            {
                var genericTypeArgs = details.GenericTypeArgs ??
                    MethodBuilderDetails.ToGenericTypeArgument(
                        MethodBuilderDetails.GenericComponentType);

                builder.AppendTripleSlashMethodComments(method)
                    .AppendRaw(
                        $"public static {details.ReturnType} {details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(",
                        postIncreaseIndentation: true)
                    .AppendRaw($"this {details.ExtendingType} javaScript,")
                    .AppendRaw($"TComponent component", appendNewLine: false, postIncreaseIndentation: true);

                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.AppendRaw(",");
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (pi.IsLast)
                        {
                            builder.AppendRaw($"{parameter.ToParameterString(isGenericType)}) where TComponent : class =>");
                        }
                        else
                        {
                            builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},");
                        }
                    }

                    if (details.IsVoid)
                    {
                        builder.AppendRaw($"javaScript.InvokeVoid{details.Suffix}(");
                    }
                    else
                    {
                        builder.AppendRaw($"javaScript.Invoke{details.Suffix}<{details.BareType}>(");
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
            }
        }

        // Add properties.
        foreach (var (index, property) in (Properties ?? new List<CSharpProperty>()).Select())
        {
            if (index.IsFirst) builder.AppendLine();
            if (property.IsIndexer) continue;

            builder.ResetIndentiationTo(methodLevel);

            var details = PropertyBuilderDetails.Create(property, options);

            builder.AppendTripleSlashPropertyComments(details.Property)
                .AppendRaw($"public static {details.ReturnType} {details.CSharpPropertyName}(", postIncreaseIndentation: true)
                .AppendRaw($"this {details.ExtendingType} javaScript) =>", postIncreaseIndentation: true)
                .AppendRaw($"javaScript.Invoke{details.Suffix}{details.GenericTypeArgs}(", postIncreaseIndentation: true)
                .AppendRaw($"\"eval\", \"{details.FullyQualifiedJavaScriptIdentifier}\");");

            if (!index.IsLast)
            {
                builder.AppendLine();
            }
        }

        builder.ResetIndentiationTo(0);
        builder.AppendClosingCurlyBrace();

        var staticPartialClassDefinition = TryFormatCSharpSourceText(builder.ToSourceCodeString());
        return staticPartialClassDefinition;
    }

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
        foreach (var method in Methods ?? new List<CSharpMethod>())
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentiationTo(methodLevel);

            if (method.IsPureJavaScriptInvocation)
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
            }
        }

        // Properties
        foreach (var (index, property) in (Properties ?? new List<CSharpProperty>()).Select())
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
            .AppendImplementationCtor();

        var methodLevel = builder.IndentationLevel;

        // Methods
        foreach (var (index, method) in (Methods ?? new List<CSharpMethod>()).Select())
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentiationTo(methodLevel);

            if (method.IsPureJavaScriptInvocation)
            {
                builder.AppendEmptyTripleSlashInheritdocComments()
                    .AppendRaw(
                        $"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(",
                        appendNewLine: false,
                        postIncreaseIndentation: true);

                if (method.ParameterDefinitions.Count > 0)
                {
                    var genericTypeParameterConstraint = details.IsGenericReturnType
                        ? $" where {MethodBuilderDetails.GenericReturnType} : default"
                        : "";

                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (pi.IsLast)
                        {
                            if (details.IsSerializable)
                            {
                                builder.AppendRaw($"{parameter.ToParameterString(isGenericType, true)},");
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
                        builder.AppendRaw(
                            $"_javaScript.{details.CSharpMethodName}{details.Suffix}(",
                            postIncreaseIndentation: true);
                    }
                    else
                    {
                        builder.AppendRaw(
                            $"_javaScript.{details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(",
                            postIncreaseIndentation: true);
                    }

                    foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
                    {
                        if (ai.IsLast)
                        {
                            if (details.IsSerializable)
                            {
                                builder.AppendRaw($"{parameter.ToArgumentString(false)},");
                                builder.AppendRaw($"options);");
                            }
                            else
                            {
                                builder.AppendRaw($"{parameter.ToArgumentString(false)});");
                            }

                            if (!index.IsLast)
                            {
                                builder.AppendLine();
                            }
                        }
                        else
                        {
                            builder.AppendRaw($"{parameter.ToArgumentString(false)},");
                        }
                    }
                }
                else
                {
                    builder.AppendRaw($") => _javaScript.{details.CSharpMethodName}{details.Suffix}();");
                }
            }
            else if (options.OnlyGeneratePureJS is false)
            {
                var genericTypeArgs = details.GenericTypeArgs ??
                    MethodBuilderDetails.ToGenericTypeArgument(
                        MethodBuilderDetails.GenericComponentType);

                builder.AppendEmptyTripleSlashInheritdocComments()
                    .AppendRaw(
                        $"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(",
                        postIncreaseIndentation: true)
                    .AppendRaw($"TComponent component", appendNewLine: false);

                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.AppendRaw(",");

                    var genericTypeParameterConstraint = " where TComponent : class";
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                        if (pi.IsLast)
                        {
                            builder.AppendRaw($"{parameter.ToParameterString(false, true)}){genericTypeParameterConstraint} =>");
                        }
                        else
                        {
                            builder.AppendRaw($"{parameter.ToParameterString(isGenericType, true)},");
                        }
                    }

                    if (details.IsVoid)
                    {
                        builder.AppendRaw(
                            $"_javaScript.{details.CSharpMethodName}{details.Suffix}(",
                            postIncreaseIndentation: true);
                    }
                    else
                    {
                        builder.AppendRaw(
                            $"_javaScript.{details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(",
                            postIncreaseIndentation: true);
                    }

                    foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
                    {
                        if (ai.IsLast)
                        {
                            builder.AppendRaw($"{parameter.ToArgumentString(false)});");

                            if (!index.IsLast)
                            {
                                builder.AppendLine();
                            }
                        }
                        else
                        {
                            if (ai.IsFirst)
                            {
                                builder.AppendRaw("component,");
                            }
                            builder.AppendRaw($"{parameter.ToArgumentString(false)},");
                        }
                    }
                }
                else
                {
                    builder.AppendRaw($") => _javaScript.{details.CSharpMethodName}{details.Suffix}();");
                }
            }
        }

        // Properties
        foreach (var (index, property) in (Properties ?? new List<CSharpProperty>()).Select())
        {
            if (index.IsFirst) builder.AppendLine();
            if (property.IsIndexer) continue;

            builder.ResetIndentiationTo(methodLevel);

            var details = PropertyBuilderDetails.Create(property, options);

            var expression = details.Property.IsReadonly
                ? $"_javaScript.{details.CSharpPropertyName}();"
                : "throw new System.NotImplementedException();";

            builder.AppendEmptyTripleSlashInheritdocComments()
                .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpPropertyName} => {expression}");

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
        var addExpression = options.IsWebAssembly
            ? @"        services.AddSingleton<IJSInProcessRuntime>(serviceProvider =>
            (IJSInProcessRuntime)serviceProvider.GetRequiredService<IJSRuntime>())
            "
            : "services";

        var typeName = $"I{options.TypeName}";
        var extensions = $@"// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License:
// https://github.com/IEvangelist/blazorators/blob/main/LICENSE
// Auto-generated by blazorators.

using Microsoft.JSInterop;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary></summary>
public static class {implementation}ServiceCollectionExtensions
{{
    /// <summary>
    /// Adds the <see cref=""{typeName}"" /> service to the service collection.
    /// </summary>
    public static IServiceCollection Add{implementation}Services(
        this IServiceCollection services) =>
        {addExpression}.AddSingleton<{typeName}, {implementation}>();
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
            Console.WriteLine(ex);

            return csharpSourceText;
        }
    }
}
