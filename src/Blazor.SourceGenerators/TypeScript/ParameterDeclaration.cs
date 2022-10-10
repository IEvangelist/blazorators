// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class ParameterDeclaration : Declaration, IVariableLikeDeclaration
{
    internal ParameterDeclaration() => ((INode)this).Kind = SyntaxKind.Parameter;

    IPropertyName IVariableLikeDeclaration.PropertyName { get; set; } = default!;
    DotDotDotToken IVariableLikeDeclaration.DotDotDotToken { get; set; } = default!;
    QuestionToken IVariableLikeDeclaration.QuestionToken { get; set; } = default!;
    ITypeNode IVariableLikeDeclaration.Type { get; set; } = default!;
    IExpression IVariableLikeDeclaration.Initializer { get; set; } = default!;
}
