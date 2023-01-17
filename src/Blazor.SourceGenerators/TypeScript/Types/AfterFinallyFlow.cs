// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class AfterFinallyFlow : IFlowNode, IFlowLock
{
    public FlowNode Antecedent { get; set; }
    public bool Locked { get; set; }
    public FlowFlags Flags { get; set; }
    public int Id { get; set; }
}