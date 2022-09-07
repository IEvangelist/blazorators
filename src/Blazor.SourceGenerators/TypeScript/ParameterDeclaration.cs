// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ParameterDeclaration : Declaration, IVariableLikeDeclaration
{
    internal ParameterDeclaration() => ((INode)this).Kind = CommentKind.Parameter;

    IPropertyName IVariableLikeDeclaration.PropertyName { get; set; }
    DotDotDotToken IVariableLikeDeclaration.DotDotDotToken { get; set; }
    QuestionToken IVariableLikeDeclaration.QuestionToken { get; set; }
    ITypeNode IVariableLikeDeclaration.Type { get; set; }
    IExpression IVariableLikeDeclaration.Initializer { get; set; }
}
