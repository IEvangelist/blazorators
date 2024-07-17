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
                builder.AppendTripleSlashMethodComments(details.Method)
                       .AppendRaw($"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(", appendNewLine: false, postIncreaseIndentation: true);

                if (method.ParameterDefinitions.Count > 0)
                {
                    AppendMethodParameters(builder, method, details, options);
                }
                else
                {
                    builder.AppendRaw(");", appendNewLine: true, omitIndentation: true);
                }
            }
            else if (!options.OnlyGeneratePureJS)
            {
                AppendNonPureMethod(builder, method, details, options);
            }
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

    private static void AppendMethodParameters(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
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

    private static void AppendNonPureMethod(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
    {
        var genericTypeArgs = details.GenericTypeArgs ?? MethodBuilderDetails.ToGenericTypeArgument(MethodBuilderDetails.GenericComponentType);

        builder.AppendTripleSlashMethodComments(details.Method, extrapolateParameters: true)
               .AppendRaw($"{details.ReturnType} {details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(")
               .AppendRaw("TComponent component", appendNewLine: false, postIncreaseIndentation: true);

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
        foreach (var property in Properties)
        {
            if (!property.IsIndexer)
            {
                AppendImplementationProperty(builder, property, options);
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
        }
    }

    private static void AppendPureMethod(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
    {
        var memberName = $"{details.CSharpMethodName}{details.Suffix}";
        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
               .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{details.GenericTypeArgs}(", appendNewLine: false, postIncreaseIndentation: true);

        if (method.ParameterDefinitions.Count > 0)
        {
            foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
            {
                var isGenericType = parameter.IsGenericParameter(method.RawName, options);
                if (pi.IsLast)
                {
                    builder.AppendRaw($"{parameter.ToParameterString(isGenericType)},")
                           .AppendRaw($"JsonSerializerOptions? options = null);")
                           .AppendLine()
                           .AppendOpeningCurlyBrace()
                           .IncreaseIndentation()
                           .AppendRaw("throw new NotImplementedException();")
                           .DecreaseIndentation()
                           .AppendClosingCurlyBrace();
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
            builder.AppendRaw(");")
                   .AppendLine()
                   .AppendOpeningCurlyBrace()
                   .IncreaseIndentation()
                   .AppendRaw("throw new NotImplementedException();")
                   .DecreaseIndentation()
                   .AppendClosingCurlyBrace();
        }
    }

    private static void AppendNonPureMethodImplementation(SourceBuilder builder, CSharpMethod method, MethodBuilderDetails details, GeneratorOptions options)
    {
        var genericTypeArgs = details.GenericTypeArgs ?? MethodBuilderDetails.ToGenericTypeArgument(MethodBuilderDetails.GenericComponentType);
        var memberName = $"{details.CSharpMethodName}{details.Suffix}";

        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
               .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpMethodName}{details.Suffix}{genericTypeArgs}(", postIncreaseIndentation: true);

        if (method.ParameterDefinitions.Count > 0)
        {
            AppendMethodParameters(builder, method, details, options);
        }
        else
        {
            builder.AppendRaw(") where TComponent : class;", appendNewLine: true, omitIndentation: true);
        }

        builder.AppendRaw("throw new NotImplementedException();")
               .DecreaseIndentation()
               .AppendClosingCurlyBrace();
    }

    private static void AppendImplementationProperty(SourceBuilder builder, CSharpProperty property, GeneratorOptions options)
    {
        var details = PropertyBuilderDetails.Create(property, options);
        var accessors = details.Property.IsReadonly ? "{ get; }" : "{ get; set; }";
        var memberName = $"{details.CSharpPropertyName}";
        builder.AppendTripleSlashInheritdocComments(builder.InterfaceName, memberName)
               .AppendRaw($"{details.ReturnType} {builder.InterfaceName}.{details.CSharpPropertyName} {accessors}")
               .AppendLine()
               .AppendOpeningCurlyBrace()
               .IncreaseIndentation()
               .AppendRaw("throw new NotImplementedException();")
               .DecreaseIndentation()
               .AppendClosingCurlyBrace();
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