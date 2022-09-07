// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class FlowCondition : FlowNode
{
    internal IExpression Expression { get; set; }
    internal FlowNode Antecedent { get; set; }
}