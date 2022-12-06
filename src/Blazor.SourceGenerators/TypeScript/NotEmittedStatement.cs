// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class NotEmittedStatement : Statement
{
    public NotEmittedStatement() => ((INode)this).Kind = TypeScriptSyntaxKind.NotEmittedStatement;
}