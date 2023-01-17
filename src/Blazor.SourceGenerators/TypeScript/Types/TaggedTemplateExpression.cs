// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TaggedTemplateExpression : MemberExpression
{
    public TaggedTemplateExpression()
    {
        Kind = TypeScriptSyntaxKind.TaggedTemplateExpression;
    }

    public IExpression Tag { get; set; }
    public Node Template { get; set; }
}