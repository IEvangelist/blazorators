// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal class Identifier : PrimaryExpression, IJsxTagNameExpression, IEntityName, IPropertyName
{
    internal Identifier() => ((INode)this).Kind = CommentKind.Identifier;

    internal string Text { get; set; } = default!;
    internal CommentKind OriginalKeywordKind { get; set; }
    internal GeneratedIdentifierKind AutoGenerateKind { get; set; }
    internal int AutoGenerateId { get; set; }
    internal bool IsInJsDocNamespace { get; set; }
}
