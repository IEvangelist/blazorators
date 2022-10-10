// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class FlowArrayMutation : FlowNode
{
    internal Node Node { get; set; }
    internal FlowNode Antecedent { get; set; }
}