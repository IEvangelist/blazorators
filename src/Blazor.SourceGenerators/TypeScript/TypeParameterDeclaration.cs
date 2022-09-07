// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class TypeParameterDeclaration : Declaration
{
    internal TypeParameterDeclaration() => ((INode)this).Kind = CommentKind.TypeParameter;

    internal ITypeNode Constraint { get; set; }
    internal ITypeNode Default { get; set; }
    internal IExpression Expression { get; set; }
}
