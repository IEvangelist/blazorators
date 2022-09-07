// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class FlowStart : FlowNode
{
    internal Node Container { get; set; } // FunctionExpression | ArrowFunction | MethodDeclaration
}