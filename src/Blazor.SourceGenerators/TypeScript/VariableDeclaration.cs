// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class VariableDeclaration : Declaration, IVariableLikeDeclaration
{
    public VariableDeclaration() => ((INode)this).Kind = TypeScriptSyntaxKind.VariableDeclaration;

    public ITypeNode Type { get; set; }
    public IExpression Initializer { get; set; }
    public IPropertyName PropertyName { get; set; }
    public DotDotDotToken DotDotDotToken { get; set; }
    public QuestionToken QuestionToken { get; set; }
}
