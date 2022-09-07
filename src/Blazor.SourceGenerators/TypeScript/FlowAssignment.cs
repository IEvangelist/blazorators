// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class FlowAssignment : FlowNode
{
    internal Node Node { get; set; } // Expression | VariableDeclaration | BindingElement
    internal FlowNode Antecedent { get; set; }
}