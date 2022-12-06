// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class TypeParameterDeclaration : Declaration
{
    public TypeParameterDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.TypeParameter;

    public ITypeNode Constraint { get; set; }
    public ITypeNode Default { get; set; }
    public IExpression Expression { get; set; }
}
