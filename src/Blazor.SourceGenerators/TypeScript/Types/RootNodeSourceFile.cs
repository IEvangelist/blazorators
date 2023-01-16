// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class RootNodeSourceFile : SourceFile
{
    public Node? Window =>
        AbstractSyntaxTree.RootNode
            .OfKind(TypeScriptSyntaxKind.InterfaceDeclaration)
            .FirstOrDefault(node => node.IdentifierStr is nameof(Window));
}
