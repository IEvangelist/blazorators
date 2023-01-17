// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class SpreadAssignment : ObjectLiteralElement, IObjectLiteralElementLike
{
    public SpreadAssignment()
    {
        Kind = TypeScriptSyntaxKind.SpreadAssignment;
    }

    public IExpression Expression { get; set; }
}