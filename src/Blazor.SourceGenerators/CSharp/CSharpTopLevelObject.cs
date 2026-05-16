// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.CSharp;

internal sealed partial record CSharpTopLevelObject(string RawTypeName)
    : ICSharpDependencyGraphObject
{
    public List<CSharpProperty>? Properties { get; init; } = [];

    public List<CSharpMethod>? Methods { get; init; } = [];

    public Dictionary<string, CSharpObject> DependentTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    private IImmutableSet<(string TypeName, CSharpObject Object)>? _allDependentTypes;

    public IImmutableSet<(string TypeName, CSharpObject Object)> AllDependentTypes
    {
        get
        {
            if (_allDependentTypes is not null)
            {
                return _allDependentTypes;
            }

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

            return _allDependentTypes = result.Select(pair => (pair.Key, pair.Value))
                .ToImmutableHashSet();
        }
    }

    public int MemberCount => Properties!.Count + Methods!.Count;

    internal string ToInterfaceString(
        GeneratorOptions options,
        string? namespaceString)
    {
        // When any method returns `Promise<T>` we must emit
        // `using System.Threading.Tasks;` even under WebAssembly,
        // because Promise returns force the `ValueTask` async path
        // regardless of hosting model.
        var requiresValueTask = Methods?.Any(m => m.IsPromise) ?? false;

        var builder = new SourceBuilder(options)
            .AppendCopyRightHeader()
            .AppendUsingDeclarations(requiresValueTask)
            .AppendNamespace(namespaceString ?? "Microsoft.JSInterop")
            .AppendPublicInterfaceDeclaration()
            .AppendOpeningCurlyBrace()
            .IncreaseIndentation();

        var methodLevel = builder.IndentationLevel;

        // Methods
        foreach (var method in Methods ?? [])
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentationTo(methodLevel);

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
                                    .AppendRaw($"JsonTypeInfo<{MethodBuilderDetails.GenericTypeValue}>? typeInfo = null);")
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

            builder.ResetIndentationTo(methodLevel);

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

        builder.ResetIndentationTo(0);
        builder.AppendClosingCurlyBrace();

        var interfaceDeclaration = TryFormatCSharpSourceText(builder.ToSourceCodeString());
        return interfaceDeclaration;
    }

    internal string ToImplementationString(
        GeneratorOptions options,
        string? namespaceString)
    {
        // See `ToInterfaceString` for rationale -- Promise-returning
        // methods need `System.Threading.Tasks` under both hosting
        // models because the async path is forced.
        var requiresValueTask = Methods?.Any(m => m.IsPromise) ?? false;

        var builder = new SourceBuilder(options)
            .AppendCopyRightHeader()
            .AppendUsingDeclarations(requiresValueTask)
            .AppendNamespace(namespaceString ?? "Microsoft.JSInterop")
            .AppendInternalImplementationDeclaration()
            .AppendOpeningCurlyBrace()
            .IncreaseIndentation()
            .AppendConditionalDelegateFields(Methods)
            .AppendImplementationCtor();

        var methodLevel = builder.IndentationLevel;

        builder.AppendConditionalDelegateCallbackMethods(Methods);

        foreach (var (index, method) in (Methods ?? []).Select())
        {
            var details = MethodBuilderDetails.Create(method, options);
            builder.ResetIndentationTo(methodLevel);

            var isJavaScriptOverride = method.IsJavaScriptOverride(options);
            var isPureNonBiDirectionalOrOverriddenJS =
                method.IsPureJavaScriptInvocation ||
                method.IsNotBiDirectionalJavaScript ||
                isJavaScriptOverride;

            if (isPureNonBiDirectionalOrOverriddenJS)
            {
                EmitPureInvocationMethod(builder, method, details, options, index.IsLast);
            }
            else if (options.OnlyGeneratePureJS is false)
            {
                EmitGenericComponentMethod(builder, method, details, options, index.IsLast);
                EmitActionComponentMethod(builder, method, details, options, index.IsLast);
            }
        }

        foreach (var (index, property) in (Properties ?? []).Select())
        {
            EmitImplementationProperty(
                builder,
                property,
                options,
                isFirstProperty: index.IsFirst,
                isLastProperty: index.IsLast,
                methodLevel: methodLevel);
        }

        builder.ResetIndentationTo(0);
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
