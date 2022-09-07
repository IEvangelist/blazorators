// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class FlowArrayMutation : FlowNode
{
    internal Node Node { get; set; } // CallExpression | BinaryExpression
    internal FlowNode Antecedent { get; set; }
}