// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class PropertySignature : TypeElement, IVariableLikeDeclaration
{
    public PropertySignature()
    {
        Kind = TypeScriptSyntaxKind.PropertySignature;
    }

    public ITypeNode Type { get; set; }
    public IExpression Initializer { get; set; }
    public IPropertyName PropertyName { get; set; }
    public DotDotDotToken DotDotDotToken { get; set; }
}