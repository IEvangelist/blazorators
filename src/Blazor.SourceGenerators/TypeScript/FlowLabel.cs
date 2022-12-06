// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class FlowLabel : FlowNode
{
    public FlowNode[] Antecedents { get; set; } = Array.Empty<FlowNode>();
}