// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class ConditionalExpression : Expression
{
    public ConditionalExpression()
    {
        Kind = TypeScriptSyntaxKind.ConditionalExpression;
    }

    public IExpression Condition { get; set; }
    public QuestionToken QuestionToken { get; set; }
    public IExpression WhenTrue { get; set; }
    public ColonToken ColonToken { get; set; }
    public IExpression WhenFalse { get; set; }
}