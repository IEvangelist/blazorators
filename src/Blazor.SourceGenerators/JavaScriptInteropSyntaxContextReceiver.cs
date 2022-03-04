// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

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

                    var isAutoInterop =
                        nameof(JSAutoInteropAttribute).Contains(name);
                    var isAutoGenericInterop =
                        nameof(JSAutoGenericInteropAttribute).Contains(name);

                    if (isAutoInterop || isAutoGenericInterop)
                    {
                        ClassDeclarations.Add(
                            new(
                                Options: attributeSyntax.GetGeneratorOptions(isAutoGenericInterop),
                                ClassDeclaration: classDeclaration,
                                InteropAttribute: attributeSyntax));
                    }
                }
            }
        }
    }
}
