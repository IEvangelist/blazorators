// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class FlowSwitchClause : FlowNode
{
    public SwitchStatement? SwitchStatement { get; set; }
    public int ClauseStart { get; set; }
    public int ClauseEnd { get; set; }
    public FlowNode? Antecedent { get; set; }
}