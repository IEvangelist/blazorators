// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using System;

namespace Blazor.SourceGenerators;

[Generator(LanguageNames.CSharp)]
internal sealed partial class JavaScriptInteropGenerator : IIncrementalGenerator
{
    private readonly HashSet<(string FileName, string SourceCode)> _sourceCodeToAdd =
    [
        (nameof(RecordCompat).ToGeneratedFileName(), RecordCompat),
        (nameof(BlazorHostingModel).ToGeneratedFileName(), BlazorHostingModel),
        (nameof(JSAutoInteropAttribute).ToGeneratedFileName(), JSAutoInteropAttribute),
        (nameof(JSAutoGenericInteropAttribute).ToGeneratedFileName(), JSAutoGenericInteropAttribute),
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            // System.Diagnostics.Debugger.Launch();
        }
#endif

        // Add constant source files.
        context.RegisterPostInitializationOutput(c =>
        {
            // Add source from text.
            foreach (var (fileName, sourceCode) in _sourceCodeToAdd)
            {
                c.AddSource(fileName,
                    SourceText.From(sourceCode, Encoding.UTF8));
            }
        });

        var provider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        var compilationAndInterfaces =
            context.CompilationProvider.Combine(provider.Collect());

        context.RegisterSourceOutput(
            compilationAndInterfaces,
            (ctx, tuple) => Execute(ctx, tuple.Left, tuple.Right));

    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        if (node is not InterfaceDeclarationSyntax interfaceDeclaration ||
            interfaceDeclaration.AttributeLists.Count is 0)
        {
            return false;
        }

        foreach (var attributeListSyntax in interfaceDeclaration.AttributeLists)
        {
            foreach (var attributeSyntax in attributeListSyntax.Attributes)
            {
                if (TryMatchInteropAttribute(attributeSyntax, out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static InterfaceDeclarationDetails? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;

        foreach (var attributeListSyntax in interfaceDeclaration.AttributeLists)
        {
            foreach (var attributeSyntax in attributeListSyntax.Attributes)
            {
                if (TryMatchInteropAttribute(attributeSyntax, out var isGeneric))
                {
                    return new(
                        Options: attributeSyntax.GetGeneratorOptions(isGeneric),
                        InterfaceDeclaration: interfaceDeclaration,
                        InteropAttribute: attributeSyntax);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="attribute"/> is one of the source generator's interop
    /// attributes (with or without the <c>Attribute</c> suffix and ignoring any qualifying namespace).
    /// When matched, <paramref name="isGeneric"/> reports whether the attribute is the generic variant.
    /// </summary>
    internal static bool TryMatchInteropAttribute(AttributeSyntax attribute, out bool isGeneric)
    {
        isGeneric = false;

        var name = attribute.Name.ToString();
        var lastDot = name.LastIndexOf('.');
        var simpleName = lastDot >= 0 ? name.Substring(lastDot + 1) : name;

        switch (simpleName)
        {
            case "JSAutoInterop":
            case nameof(JSAutoInteropAttribute):
                return true;

            case "JSAutoGenericInterop":
            case nameof(JSAutoGenericInteropAttribute):
                isGeneric = true;
                return true;

            default:
                return false;
        }
    }

    private static void Execute(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<InterfaceDeclarationDetails?> interfaceDeclarations)
    {
        // Not sure if/when this will be needed.
        _ = compilation;

        if (interfaceDeclarations.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var tuple in interfaceDeclarations.Distinct())
        {
            if (tuple is null)
            {
                continue;
            }

            var (options, interfaceDeclaration, attribute) = tuple;

            if (options is null || IsDiagnosticError(options, context, attribute))
            {
                continue;
            }

            var isPartial = interfaceDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            if (!isPartial)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptors.MissingPartialModifierDiagnostic,
                        interfaceDeclaration.Identifier.GetLocation(),
                        interfaceDeclaration.Identifier.ValueText));
                continue;
            }

            var @namespace = interfaceDeclaration.Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault();

            var containingNamespace = @namespace?.Name.ToString();

            foreach (var parser in options.Parsers)
            {
                var result = parser.ParseTargetType(options.TypeName!);

                switch (result.Status)
                {
                    case ParserResultStatus.SuccessfullyParsed when result.Value is not null:
                        var namespaceString =
                            (containingNamespace, interfaceDeclaration.Parent) switch
                            {
                                (string { Length: > 0 } ns, _) => ns,
                                (_, BaseNamespaceDeclarationSyntax namespaceDeclaration) => namespaceDeclaration.Name.ToString(),
                                _ => null
                            };
                        var @interface =
                            options.Implementation.ToInterfaceName();
                        var implementation =
                            options.Implementation.ToImplementationName();

                        var topLevelObject = result.Value;

                        context.AddDependentTypesSource(topLevelObject)
                            .AddInterfaceSource(topLevelObject, @interface, options, namespaceString)
                            .AddImplementationSource(topLevelObject, implementation, options, namespaceString)
                            .AddDependencyInjectionExtensionsSource(topLevelObject, implementation, options);
                        break;

                    case ParserResultStatus.TargetTypeNotFound:
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Descriptors.TargetTypeNotFoundDiagnostic,
                                attribute.GetLocation(),
                                options.TypeName));
                        break;

                    case ParserResultStatus.ErrorParsing:
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Descriptors.TypeParseFailureDiagnostic,
                                attribute.GetLocation(),
                                options.TypeName,
                                result.Error ?? "(no error message)"));
                        break;
                }
            }
        }
    }

    static bool IsDiagnosticError(GeneratorOptions options, SourceProductionContext context, AttributeSyntax attribute)
    {
        if (options.TypeName is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.TypeNameRequiredDiagnostic,
                    attribute.GetLocation()));

            return true;
        }

        if (options.Implementation is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.PathFromWindowRequiredDiagnostic,
                    attribute.GetLocation()));

            return true;
        }

        //if (options.SupportsGenerics &&
        //    context.Compilation.ReferencedAssemblyNames.Any(ai =>
        //    ai.Name.Equals("Blazor.Serialization", StringComparison.OrdinalIgnoreCase)) is false)
        //{
        //    context.ReportDiagnostic(
        //        Diagnostic.Create(
        //            Descriptors.MissingBlazorSerializationPackageReferenceDiagnostic,
        //            attribute.GetLocation()));

        //    return true;
        //}

        return false;
    }
}
