// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TypeParameterDeclaration : Declaration
{
    public TypeParameterDeclaration()
    {
        Kind = TypeScriptSyntaxKind.TypeParameter;
    }

    public ITypeNode Constraint { get; set; }
    public ITypeNode Default { get; set; }
    public IExpression Expression { get; set; }
}