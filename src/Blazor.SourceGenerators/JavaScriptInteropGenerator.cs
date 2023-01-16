// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators;

[Generator]
internal sealed partial class JavaScriptInteropGenerator : ISourceGenerator
{
    private readonly HashSet<(string FileName, string SourceCode)> _sourceCodeToAdd = new()
    {
        (nameof(RecordCompat).ToGeneratedFileName(), RecordCompat),
        (nameof(BlazorHostingModel).ToGeneratedFileName(), BlazorHostingModel),
        (nameof(JSAutoInteropAttribute).ToGeneratedFileName(), JSAutoInteropAttribute),
        (nameof(JSAutoGenericInteropAttribute).ToGeneratedFileName(), JSAutoGenericInteropAttribute),
    };

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            // System.Diagnostics.Debugger.Launch();
        }
#endif

        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(
            JavaScriptInteropSyntaxContextReceiver.Create);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Add source from text.
        foreach (var (fileName, sourceCode) in _sourceCodeToAdd)
        {
            context.AddSource(fileName,
                SourceText.From(sourceCode, Encoding.UTF8));
        }

        if (context.SyntaxContextReceiver is not JavaScriptInteropSyntaxContextReceiver receiver)
        {
            return;
        }

        foreach (var (options, classDeclaration, attribute) in receiver.InterfaceDeclarations)
        {
            if (options is null || IsDiaganosticError(options, context, attribute))
            {
                continue;
            }

            var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            if (!isPartial)
            {
                continue;
            }

            var model = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            var symbol = model.GetDeclaredSymbol(classDeclaration);
            if (symbol is not ITypeSymbol typeSymbol || typeSymbol.IsStatic)
            {
                continue;
            }

            foreach (var parser in options.Parsers)
            {
                var result = parser.ParseTargetType(options.TypeName!);
                if (result.Status is ParserResultStatus.SuccessfullyParsed &&
                    result.Value is not null)
                {
                    var namespaceString =
                        (typeSymbol.ContainingNamespace.ToDisplayString(), classDeclaration.Parent) switch
                        {
                            (string { Length: > 0 } containingNamespace, _) => containingNamespace,
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
                }
            }
        }
    }

    static bool IsDiaganosticError(GeneratorOptions options, GeneratorExecutionContext context, AttributeSyntax attribute)
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

        if (options.SupportsGenerics &&
            context.Compilation.ReferencedAssemblyNames.Any(ai =>
            ai.Name.Equals("Blazor.Serialization", StringComparison.OrdinalIgnoreCase)) is false)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.MissingBlazorSerializationPackageReferenceDiagnostic,
                    attribute.GetLocation()));

            return true;
        }

        return false;
    }
}
