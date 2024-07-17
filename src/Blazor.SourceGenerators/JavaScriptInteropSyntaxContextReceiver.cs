// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators;

internal class JavaScriptInteropSyntaxContextReceiver : ISyntaxContextReceiver
{
    internal static ISyntaxContextReceiver Create() => new JavaScriptInteropSyntaxContextReceiver();

    public HashSet<InterfaceDeclarationDetails> InterfaceDeclarations { get; } = [];

    public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
    {
        if (context.Node is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 } interfaceDeclaration)
        {
            foreach (var attributeListSyntax in interfaceDeclaration.AttributeLists)
            {
                foreach (var attributeSyntax in attributeListSyntax.Attributes)
                {
                    var attributeName = attributeSyntax.Name.ToString();

                    var isAutoInterop =
                        nameof(JSAutoInteropAttribute).Contains(attributeName);
                    var isAutoGenericInterop =
                        nameof(JSAutoGenericInteropAttribute).Contains(attributeName);

                    if (isAutoInterop || isAutoGenericInterop)
                    {
                        InterfaceDeclarations.Add(
                            new(
                                Options: attributeSyntax.GetGeneratorOptions(isAutoGenericInterop),
                                InterfaceDeclaration: interfaceDeclaration,
                                InteropAttribute: attributeSyntax));
                    }
                }
            }
        }
    }
}
