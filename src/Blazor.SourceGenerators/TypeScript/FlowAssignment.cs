// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class FlowAssignment : FlowNode
{
    public Node? Node { get; set; } // Expression | VariableDeclaration | BindingElement
    public FlowNode? Antecedent { get; set; }
}