// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TypeScript.TypeConverter.Parsers;

namespace Blazor.SourceGenerators;

[Generator]
public class JavaScriptInteropGenerator : ISourceGenerator
{
    private readonly LibDomParser _libDomParser = new();

    private const string JSAutoInteropAttributeFullName = "JSAutoInteropAttribute";
    private const string JSAutoInteropAttributeSource = @"using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class JSAutoInteropAttribute : Attribute
{
    public string? TypeName { get; set; }

    public string? Url { get; set; }
}
";

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        //if (!Debugger.IsAttached) Debugger.Launch();
#endif
        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(SyntaxContextReceiver.Create);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // Add the attribute text
        context.AddSource("JSAutoInteropAttribute.cs",
            SourceText.From(JSAutoInteropAttributeSource, Encoding.UTF8));

        return;

        if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver)
        {
            return;
        }

        foreach (var (typeName, classDeclaration) in receiver.ClassDeclarations)
        {
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

            var result = _libDomParser.ParseStaticType(typeName);
            if (result.Status == ParserResultStatus.SuccessfullyParsed &&
                result.Value is not null)
            {
                var staticObject = result.Value;
                if (staticObject.DependentTypes?.Any() ?? false)
                {
                    foreach (var dependentObj in staticObject.DependentTypes)
                    {
                        context.AddSource($"{dependentObj.Key}.generated.cs",
                            SourceText.From(dependentObj.ToString(), Encoding.UTF8));
                    }
                }

                context.AddSource($"{typeSymbol.Name}.generated.cs",
                    SourceText.From(staticObject.ToStaticPartialClassString(), Encoding.UTF8));
            }
        }
    }

    private sealed class SyntaxContextReceiver : ISyntaxContextReceiver
    {
        internal static ISyntaxContextReceiver Create() => new SyntaxContextReceiver();

        public HashSet<(string TypeName, ClassDeclarationSyntax ClassDeclaration)> ClassDeclarations { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is ClassDeclarationSyntax classDeclaration &&
                classDeclaration.AttributeLists.Count > 0)
            {
                foreach (var attributeListSyntax in classDeclaration.AttributeLists)
                {
                    foreach (var attributeSyntax in attributeListSyntax.Attributes)
                    {
                        var symbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol;
                        if (symbol is not IMethodSymbol attributeSymbol)
                        {
                            continue;
                        }

                        var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                        var fullName = attributeContainingTypeSymbol.ToDisplayString();
                        if (fullName == JSAutoInteropAttributeFullName)
                        {
                            var typeName = GetJavaScriptInteropTypeName(attributeSyntax);
                            ClassDeclarations.Add((typeName, classDeclaration));
                        }
                    }
                }
            }
        }

        private static string GetJavaScriptInteropTypeName(AttributeSyntax attribute)
        {
            if (attribute is { ArgumentList: not null })
            {
                return attribute.ArgumentList
                    .Arguments
                    .First()
                    .Expression
                    .ToString();
            }

            return ""; // 🤬
        }
    }
}
