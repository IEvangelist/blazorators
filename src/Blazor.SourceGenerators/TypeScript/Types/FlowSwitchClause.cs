// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class FlowSwitchClause : FlowNode
{
    public SwitchStatement SwitchStatement { get; set; }
    public int ClauseStart { get; set; }
    public int ClauseEnd { get; set; }
    public FlowNode Antecedent { get; set; }
}