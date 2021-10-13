// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazor.SourceGenerators
{
    [Generator]
    public class JavaScriptInteropGenerator : ISourceGenerator
    {
        private const string JavaScriptInteropAttributeFullName = "Microsoft.JSInterop.Attributes.JavaScriptInteropAttribute";

        private const string attributeText = @"
using System;

#nullable enable

namespace Microsoft.JSInterop.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class JavaScriptInteropAttribute : Attribute
{
    public string? TypeName { get; set; }

    public string? Url { get; set; }
}
";

        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            // For debugging
            if (!Debugger.IsAttached) Debugger.Launch(); 
#endif

            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(SyntaxContextReceiver.Create);
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Add the attribute text
            // context.AddSource("JavaScriptInteropAttribute", SourceText.From(attributeText, Encoding.UTF8));

            if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver
                || receiver.ClassDeclarations.Count == 0)
                return;

            foreach (var classDeclaration in receiver.ClassDeclarations)
            {
                // TODO:

                // 1. Parse corresponding type:
                //    a. Class name, less the "Extensions" suffix.
                //    - or -
                //    b. The TypeName as defined within the JavaScriptInterop itself.
                var model = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(classDeclaration);
                if (!(symbol is ITypeSymbol typeSymbol
                    && typeSymbol.IsStatic))
                {
                    continue;
                }
                var assemblyName = GetType().Assembly.GetName().Name;
                var attributes = typeSymbol.GetAttributes();
                var attribute = attributes.First(c => c.AttributeClass.ContainingAssembly.Name == assemblyName
                    && c.AttributeClass.ToDisplayString() == JavaScriptInteropAttributeFullName);
                var attributeTypeName = attribute.NamedArguments.FirstOrDefault(a => a.Key == "TypeName").Value.Value?.ToString();
                var classTypeName = typeSymbol.Name;

                // The final type name to be used 
                var typeName = string.IsNullOrEmpty(attributeTypeName) ? classTypeName : attributeTypeName;
                if (typeName.EndsWith("Extensions"))
                {
                    typeName = typeName[..^"Extensions".Length];
                }

                // 2. Ask cache for API descriptors
                //    a. If not found, request raw from values from
                //    https://github.com/microsoft/TypeScript-DOM-lib-generator/tree/main/inputfiles
                //    and populate cache.
                //    - or -
                //    b. If found, return it.

                // 3. Source generate records, classes, structs, and interfaces that define the object surface area.
                // 4. Source generate the extension methods.

                // maybe consider for a generate template
                var generatedExtensions = new StringBuilder();
                generatedExtensions.Append($@"// This file is generated
using Microsoft.JSInterop;

namespace {typeSymbol.ContainingNamespace.ToDisplayString()};

public static partial class {typeSymbol.Name}
{{
    private static void TestMethod(this IJSRuntime jsRuntime){{}}
}}
");
                var generatedText = generatedExtensions.ToString();
                context.AddSource($"{typeSymbol.Name}.generated.cs", generatedText);

                // 5. Source generate the JavaScript, if necessary.
            }
        }

        private sealed class SyntaxContextReceiver : ISyntaxContextReceiver
        {
            internal static ISyntaxContextReceiver Create()
            {
                return new SyntaxContextReceiver();
            }

            public HashSet<ClassDeclarationSyntax> ClassDeclarations { get; } = new();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if(context.Node is ClassDeclarationSyntax classDeclaration
                    && classDeclaration.AttributeLists.Count > 0)
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
                            if (fullName == JavaScriptInteropAttributeFullName)
                            {
                                ClassDeclarations.Add(classDeclaration);
                            }
                        }
                    }
                }
            }
        }
    }
}
