// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class MappedTypeNode : Node, ITypeNode, IDeclaration
{
    public MappedTypeNode()
    {
        Kind = TypeScriptSyntaxKind.MappedType;
    }

    public ReadonlyToken ReadonlyToken { get; set; }
    public TypeParameterDeclaration TypeParameter { get; set; }
    public QuestionToken QuestionToken { get; set; }
    public ITypeNode Type { get; set; }
    public object DeclarationBrand { get; set; }
    public INode Name { get; set; }
    public object TypeNodeBrand { get; set; }
}