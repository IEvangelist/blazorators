// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Blazor.SourceGenerators.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazor.SourceGenerators
{
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

        private const string RecordCompatSource = @"using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}";

        public void Initialize(GeneratorInitializationContext context)
        {
//#if DEBUG
//            if (!Debugger.IsAttached) Debugger.Launch();
//#endif

            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(SyntaxContextReceiver.Create);
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Add source from text.
            context.AddSource("JSAutoInteropAttribute",
                SourceText.From(JSAutoInteropAttributeSource, Encoding.UTF8));
            context.AddSource("RecordCompat",
                SourceText.From(RecordCompatSource, Encoding.UTF8));

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
                        foreach (var dependentObj in staticObject.DependentTypes.Where(t => !t.Value.IsActionParameter))
                        {
                            context.AddSource($"{dependentObj.Key}.generated.cs",
                                SourceText.From(dependentObj.Value.ToString(), Encoding.UTF8));
                        }
                    }

                    // TODO:
                    // Output JavaScript also

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
                            var name = attributeSyntax.Name.ToString();
                            if (JSAutoInteropAttributeFullName.Contains(name))
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
                    var argumentValue = attribute.ArgumentList
                        .Arguments
                        .First()
                        .Expression
                        .ToString();

                    return argumentValue.Replace("\"", "");
                }

                return ""; // 🤬
            }
        }
    }
}