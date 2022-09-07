// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class FlowSwitchClause : FlowNode
{
    internal SwitchStatement SwitchStatement { get; set; }
    internal int ClauseStart { get; set; }
    internal int ClauseEnd { get; set; }
    internal FlowNode Antecedent { get; set; }
}