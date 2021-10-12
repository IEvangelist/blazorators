// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazor.SourceGenerators
{
    [Generator]
    public class JavaScriptInteropGenerator : ISourceGenerator
    {
        private const string attributeText = @"
using System;

namespace Microsoft.JSInterop.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class JavaScriptInteropAttribute : Attribute
{
    public string TypeName { get; set; }

    public string Url { get; set; }
}
";

        public void Initialize(GeneratorInitializationContext context)
        {
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Add the attribute text
            context.AddSource("JavaScriptInteropAttribute", SourceText.From(attributeText, Encoding.UTF8));

            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;
        }

        internal sealed class SyntaxReceiver : ISyntaxReceiver
        {
            internal List<(bool IsCaseInsensitve, TypeDeclarationSyntax TypeDeclaration)> TypeDeclarationSyntaxList { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is TypeDeclarationSyntax typeDeclarationSyntax)
                {
                    var attribute =
                        typeDeclarationSyntax.AttributeLists.SelectMany(
                            list => list.Attributes.Where(
                                attr => attr.Name.ToString() == "AutoEquality"))
                            .FirstOrDefault();

                    if (attribute is not null)
                    {
                        var isCaseInsensitive =
                            attribute.ArgumentList is not null &&
                            bool.Parse(
                                attribute.ArgumentList
                                    .Arguments
                                    .FirstOrDefault()
                                    ?.Expression.ToString());
                        TypeDeclarationSyntaxList.Add((isCaseInsensitive, typeDeclarationSyntax));
                    }
                }
            }
        }
    }
}
