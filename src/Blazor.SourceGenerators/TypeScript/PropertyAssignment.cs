// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class PropertyAssignment : ObjectLiteralElement, IObjectLiteralElementLike, IVariableLikeDeclaration
{
    public PropertyAssignment() => ((INode)this).Kind = TypeScriptSyntaxKind.PropertyAssignment;

    public QuestionToken QuestionToken { get; set; }
    public IExpression Initializer { get; set; }
    public IPropertyName PropertyName { get; set; }
    public DotDotDotToken DotDotDotToken { get; set; }
    public ITypeNode Type { get; set; }
}
