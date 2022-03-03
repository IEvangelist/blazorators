// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Blazor.SourceGenerators;

internal class JavaScriptInteropSyntaxContextReceiver : ISyntaxContextReceiver
{
    internal static ISyntaxContextReceiver Create() => new JavaScriptInteropSyntaxContextReceiver();

    public HashSet<ClassDeclarationDetails> ClassDeclarations { get; } = new();

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
                    if (nameof(JSAutoInteropAttribute).Contains(name) ||
                        nameof(JSAutoGenericInteropAttribute).Contains(name))
                    {
                        ClassDeclarations.Add(
                            new(
                                Options: attributeSyntax.GetGeneratorOptions(),
                                ClassDeclaration: classDeclaration,
                                InteropAttribute: attributeSyntax));
                    }
                }
            }
        }
    }
}
