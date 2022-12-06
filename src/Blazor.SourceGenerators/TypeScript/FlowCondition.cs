// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class FlowCondition : FlowNode
{
    public IExpression? Expression { get; set; }
    public FlowNode? Antecedent { get; set; }
}