// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TemplateSpan : Node
{
    public TemplateSpan()
    {
        Kind = TypeScriptSyntaxKind.TemplateSpan;
    }

    public IExpression Expression { get; set; }
    public ILiteralLikeNode Literal { get; set; }
}