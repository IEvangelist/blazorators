// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Builders;
using Blazor.SourceGenerators.Options;

namespace Blazor.SourceGenerators.CSharp;

/// <summary>
/// Represents a top-level C# object which may contain properties and methods.
/// </summary>
internal sealed partial record CSharpTopLevelObject(string RawTypeName) : ICSharpDependencyGraphObject
{
    public List<CSharpProperty> Properties { get; init; } = [];
    public List<CSharpMethod> Methods { get; init; } = [];
    public Dictionary<string, CSharpObject> DependentTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all dependent types for this top-level object, including properties and methods.
    /// </summary>
    public IImmutableSet<DependentType> AllDependentTypes => DependentTypes
        .Select(kvp => new DependentType(kvp.Key, kvp.Value))
        .Concat(Methods.SelectMany(method => method.AllDependentTypes))
        .Concat(Properties.SelectMany(method => method.AllDependentTypes))
        .ToImmutableHashSet(DependentTypeComparer.Default);

    /// <summary>
    /// Gets the count of members (properties and methods).
    /// </summary>
    public int MemberCount => Properties.Count + Methods.Count;

    /// <summary>
    /// Generates the interface string for the C# object.
    /// </summary>
    internal string ToInterfaceString(GeneratorOptions options, string? namespaceString)
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
        foreach (var method in Methods)
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentiationTo(methodLevel);

            var isJavaScriptOverride = method.IsJavaScriptOverride(options);
            var isPureNonBiDirectionalOrOverriddenJS = method.IsPureJavaScriptInvocation ||
                                                       method.IsNotBiDirectionalJavaScript ||
                                                       isJavaScriptOverride;

            if (isPureNonBiDirectionalOrOverriddenJS)
            {
                var hasParameters = method.ParameterDefinitions.Count > 0;

                builder.AppendTripleSlashMethodComments(details.Method)
                       .AppendRaw($"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(",
                                  appendNewLine: hasParameters,
                                  postIncreaseIndentation: hasParameters);

                if (hasParameters)
                {
                    AppendMethodParameters(builder, method, details, options, suffix: ");");
                }
                else
                {
                    builder.AppendRaw(");", omitIndentation: true);
                }
            }
            else if (!options.OnlyGeneratePureJS)
            {
                AppendNonPureMethod(builder, method, details, options);
            }

            builder.AppendLine();
        }

        // Properties
        foreach (var property in Properties)
        {
            if (!property.IsIndexer)
            {
                AppendProperty(builder, property, options);
            }
        }

        builder.ResetIndentiationTo(0);
        builder.AppendClosingCurlyBrace();

        return TryFormatCSharpSourceText(builder.ToSourceCodeString());
    }

    private static void AppendMethodParameters(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options, string suffix = "", bool asDelegate = false)
    {
        foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
        {
            var isGenericType = parameter.IsGenericParameter(method.RawName, options);
            var parameterString = asDelegate
                ? parameter.ToActionString(isGenericType)
                : parameter.ToParameterString(isGenericType);

            if (pi.IsLast)
            {
                if (details.IsSerializable)
                {
                    builder.AppendRaw($"{parameterString},")
                           .AppendRaw($"JsonSerializerOptions? options = null{suffix}", appendNewLine: false);
                }
                else
                {
                    builder.AppendRaw($"{parameterString}{suffix}", appendNewLine: false);
                }
            }
            else
            {
                builder.AppendRaw($"{parameterString},");
            }
        }
    }

    private static void AppendNonPureMethod(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
    {
        var genericTypeArgs = details.GenericTypeArgs ?? MethodBuilderDetails.ToGenericTypeArgument(MethodBuilderDetails.GenericComponentType);

        builder.AppendTripleSlashMethodComments(details.Method, extrapolateParameters: true)
               .AppendRaw($"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(", postIncreaseIndentation: true)
               .AppendRaw("TComponent component", appendNewLine: false);

        if (method.ParameterDefinitions.Count > 0)
        {
            builder.AppendRaw(",", omitIndentation: true);
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
               .AppendRaw($"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}(", postIncreaseIndentation: true);

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
                    builder.AppendRaw($"{parameter.ToActionString(isGenericType)},");
                }
            }

            builder.DecreaseIndentation();
        }
        else
        {
            builder.AppendRaw(");", omitIndentation: true);
        }
    }

    private static void AppendProperty(SourceBuilder builder, CSharpProperty property, GeneratorOptions options)
    {
        var details = PropertyBuilderDetails.Create(property, options);
        var accessors = details.Property.IsReadonly ? "{ get; }" : "{ get; set; }";
        builder.AppendTripleSlashPropertyComments(details.Property)
               .AppendRaw($"{details.ReturnType} {details.CSharpPropertyName} {accessors}");
    }

    internal string ToImplementationString(GeneratorOptions options, string? namespaceString)
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
        foreach (var method in Methods)
        {
            AppendImplementationMethod(builder, method, options, methodLevel);
        }

        // Properties
        foreach (var (index, property) in Properties.Select())
        {
            if (!property.IsIndexer)
            {
                AppendImplementationProperty(builder, property, options, methodLevel, index);
            }
        }

        builder.ResetIndentiationTo(0);
        builder.AppendClosingCurlyBrace();

        return TryFormatCSharpSourceText(builder.ToSourceCodeString());
    }

    internal static string ToServiceCollectionExtensions(GeneratorOptions options, string implementation)
    {
        var serviceLifetime = options.IsWebAssembly
            ? "Singleton"
            : "Scoped";
        var addExpression = options.IsWebAssembly
            ? $$"""
                        services.Add{{serviceLifetime}}<IJSInProcessRuntime>(serviceProvider =>
                        (IJSInProcessRuntime)serviceProvider.GetRequiredService<IJSRuntime>())
                """
            : "services";

        var @interface = options.Implementation.ToInterfaceName();
        var nonService = options.Implementation.ToImplementationName(false);

        var extensions = $$"""
        // Copyright (c) David Pine. All rights reserved.
        // Licensed under the MIT License:
        // https://github.com/IEvangelist/blazorators/blob/main/LICENSE
        // Auto-generated by blazorators.

        using Microsoft.JSInterop;

        namespace Microsoft.Extensions.DependencyInjection;

        /// <summary></summary>
        public static class {{nonService}}ServiceCollectionExtensions
        {
            /// <summary>
            /// Adds the <see cref="{{@interface}}" /> service to the service collection.
            /// </summary>
            public static IServiceCollection Add{{nonService}}Services(
                this IServiceCollection services) =>
                {{addExpression}}.Add{{serviceLifetime}}<{{@interface}}, {{implementation}}>();
        }

        """;

        return extensions;
    }

    private static void AppendImplementationMethod(SourceBuilder builder, CSharpMethod method, GeneratorOptions options, int methodLevel)
    {
        var details = MethodBuilderDetails.Create(method, options);
        builder.ResetIndentiationTo(methodLevel);

        var isJavaScriptOverride = method.IsJavaScriptOverride(options);
        var isPureNonBiDirectionalOrOverriddenJS = method.IsPureJavaScriptInvocation ||
                                                   method.IsNotBiDirectionalJavaScript ||
                                                   isJavaScriptOverride;

        if (isPureNonBiDirectionalOrOverriddenJS)
        {
            AppendPureMethod(builder, method, details, options);
        }
        else if (!options.OnlyGeneratePureJS)
        {
            AppendNonPureMethodImplementation(builder, method, details, options);

            builder.AppendLine();

            AppendActionCallbackMethodImplementation(builder, method, details, options);
        }

        builder.AppendLine();
    }

    private static void AppendActionCallbackMethodImplementation(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
    {
        var memberName = $"{details.CSharpMethodName}{details.Suffix}";

        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
               .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}(",
                          postIncreaseIndentation: true);

        if (method.ParameterDefinitions.Count > 0)
        {
            AppendMethodParameters(builder, method, details, options, suffix: ") =>", asDelegate: true);
        }
        else
        {
            builder.AppendRaw(") =>", appendNewLine: true, omitIndentation: true);
        }

        builder.AppendLine();

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

        AppendActionJavascriptParameters(method, builder, options);

        builder.DecreaseIndentation();
    }

    private static void AppendActionJavascriptParameters(CSharpMethod method, SourceBuilder builder, GeneratorOptions options)
    {
        foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
        {
            if (pi.IsFirst)
            {
                builder.AppendRaw($"DotNetObjectReference.Create(this),");
            }

            var isGenericType = parameter.IsGenericParameter(method.RawName, options);
            var argument = parameter.ToArgumentString(isGenericType, asDelegate: true);
            var methodName = builder.Methods?.FirstOrDefault(method => method.EndsWith(argument.Substring(2)));
            var argExpression = methodName is not null ? $"nameof({methodName})" : argument;

            if (pi.IsLast)
            {
                builder.AppendRaw($"{argExpression});");
            }
            else
            {
                builder.AppendRaw($"{argExpression},");
            }
        }

        builder.DecreaseIndentation();
    }

    private static void AppendPureMethod(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
    {
        var memberName = $"{details.CSharpMethodName}{details.Suffix}";
        var hasParameters = method.ParameterDefinitions.Count > 0;

        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
               .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(",
                          appendNewLine: hasParameters,
                          postIncreaseIndentation: hasParameters);

        if (hasParameters)
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
                        builder.AppendRaw($"{parameter.ToParameterString(isGenericType: false, overrideNullability: true)}){genericTypeParameterConstraint} =>");
                    }
                }
                else
                {
                    builder.AppendRaw($"{parameter.ToParameterString(isGenericType, overrideNullability: true)},");
                }
            }

            builder.DecreaseIndentation();
        }
        else
        {
            builder.AppendRaw(") =>", omitIndentation: true);
        }

        builder.IncreaseIndentation();

        if (details.IsVoid)
        {
            builder.AppendRaw($"_javaScript.InvokeVoid{details.Suffix}(");
        }
        else
        {
            builder.AppendRaw($"_javaScript.Invoke{details.Suffix}<{details.BareType}>(");
        }

        builder.IncreaseIndentation()
               .AppendRaw($"\"{details.FullyQualifiedJavaScriptIdentifier}\"", appendNewLine: false);

        if (hasParameters)
        {
            AppendPureJavascriptParameters(method, builder, details, options);
        }
        else
        {
            builder.AppendRaw(");", omitIndentation: true);
        }

        builder.DecreaseIndentation();
        builder.DecreaseIndentation();
    }

    private static void AppendPureJavascriptParameters(CSharpMethod method, SourceBuilder builder, MethodBuilderDetails details, GeneratorOptions options)
    {
        builder.AppendRaw(",", omitIndentation: true);

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
            }
            else
            {
                builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)},");
            }
        }
    }

    private static void AppendNonPureMethodImplementation(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
    {
        var genericTypeArgs = details.GenericTypeArgs ?? MethodBuilderDetails.ToGenericTypeArgument(MethodBuilderDetails.GenericComponentType);
        var memberName = $"{details.CSharpMethodName}{details.Suffix}";

        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
               .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(", postIncreaseIndentation: true)
               .AppendRaw("TComponent component", appendNewLine: false);

        if (method.ParameterDefinitions.Count > 0)
        {
            builder.AppendRaw(",", omitIndentation: true);
            AppendMethodParameters(builder, method, details, options, suffix: ") where TComponent : class =>");
        }
        else
        {
            builder.AppendRaw(") where TComponent : class =>", appendNewLine: true, omitIndentation: true);
        }

        builder.AppendLine();

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

        AppendNonPureJavascriptParameters(method, builder, options);

        builder.DecreaseIndentation();
    }

    private static void AppendNonPureJavascriptParameters(CSharpMethod method, SourceBuilder builder, GeneratorOptions options)
    {
        foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
        {
            var isGenericType = parameter.IsGenericParameter(method.RawName, options);
            if (ai.IsLast)
            {
                builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)});");
            }
            else
            {
                builder.AppendRaw($"{parameter.ToArgumentString(isGenericType)},");
            }
        }

        builder.DecreaseIndentation();
    }

    private static void AppendImplementationProperty(SourceBuilder builder, CSharpProperty property, GeneratorOptions options, int methodLevel, Iteration index)
    {
        if (index.IsFirst) builder.AppendLine();
        if (property.IsIndexer) return;

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

    /// <summary>
    /// Format the C# source code string.
    /// </summary>
    private static string TryFormatCSharpSourceText(string sourceCode)
    {
        // Use Roslyn or another C# formatter if available
        // For now, return the raw source code
        return sourceCode;
    }
}