// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using Blazor.SourceGenerators.Diagnostics;
using Blazor.SourceGenerators.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazor.SourceGenerators;

[Generator]
internal sealed partial class JavaScriptInteropGenerator : ISourceGenerator
{
    private readonly LibDomParser _libDomParser = new();

    private const string RecordCompatSource = @"using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}";

    public void Initialize(GeneratorInitializationContext context)
    {
//#if DEBUG
//        if (!System.Diagnostics.Debugger.IsAttached)
//        {
//            System.Diagnostics.Debugger.Launch();
//        }
//#endif

        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(
            JavaScriptInteropSyntaxContextReceiver.Create);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Add source from text.
        context.AddSource("RecordCompat.g.cs",
            SourceText.From(RecordCompatSource, Encoding.UTF8));

        if (context.SyntaxContextReceiver is not JavaScriptInteropSyntaxContextReceiver receiver)
        {
            return;
        }

        foreach (var (options, classDeclaration, attribute) in receiver.ClassDeclarations)
        {
            if (options.TypeName is null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptors.TypeNameRequiredDiagnostic,
                        attribute.GetLocation()));

                continue;
            }

            if (options.PathFromWindow is null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptors.PathFromWindowRequiredDiagnostic,
                        attribute.GetLocation()));

                continue;
            }

            var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            if (!isPartial)
            {
                continue;
            }

            var model = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            var symbol = model.GetDeclaredSymbol(classDeclaration);
            if (symbol is not ITypeSymbol typeSymbol || !typeSymbol.IsStatic)
            {
                continue;
            }

            var result = _libDomParser.ParseStaticType(options.TypeName!);
            if (result.Status == ParserResultStatus.SuccessfullyParsed &&
                result.Value is not null)
            {
                var staticObject = result.Value;
                if (staticObject.DependentTypes?.Any() ?? false)
                {
                    foreach (var dependentObj in
                        staticObject.DependentTypes.Where(
                            t => !t.Value.IsActionParameter))
                    {
                        context.AddSource($"{dependentObj.Key}.g.cs",
                            SourceText.From(dependentObj.Value.ToString(),
                            Encoding.UTF8));
                    }
                }

                var namespaceString =
                    (typeSymbol.ContainingNamespace.ToDisplayString(), classDeclaration.Parent) switch
                    {
                        (string { Length: > 0 } containingNamespace, _) => containingNamespace,
                        (_, BaseNamespaceDeclarationSyntax namespaceDeclaration) => namespaceDeclaration.Name.ToString(),
                        _ => null
                    };

                context.AddSource(
                    $"{typeSymbol.Name}.g.cs",
                    SourceText.From(
                        staticObject.ToStaticPartialClassString(
                            options,
                            classDeclaration.Identifier.ValueText,
                            namespaceString),
                        Encoding.UTF8));
            }
        }
    }
}
