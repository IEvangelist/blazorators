// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class FlowStart : FlowNode
{
    public Node? Container { get; set; } // FunctionExpression | ArrowFunction | MethodDeclaration
}