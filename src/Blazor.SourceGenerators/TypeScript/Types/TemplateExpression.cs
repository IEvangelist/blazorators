// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class TemplateExpression : PrimaryExpression
{
    public TemplateExpression()
    {
        Kind = TypeScriptSyntaxKind.TemplateExpression;
    }

    public TemplateHead Head { get; set; }
    public NodeArray<TemplateSpan> TemplateSpans { get; set; }
}