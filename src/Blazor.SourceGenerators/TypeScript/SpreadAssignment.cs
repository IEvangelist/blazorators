// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class SpreadAssignment : ObjectLiteralElement, IObjectLiteralElementLike
{
    public SpreadAssignment() => ((INode)this).Kind = TypeScriptSyntaxKind.SpreadAssignment;

    public IExpression Expression { get; set; }
}
