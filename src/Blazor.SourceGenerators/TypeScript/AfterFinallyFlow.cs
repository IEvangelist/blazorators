// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public sealed class AfterFinallyFlow : IFlowNode, IFlowLock
{
    public FlowNode Antecedent? { get; set; }
    bool IFlowLock.Locked { get; set; }
    FlowFlags IFlowNode.Flags { get; set; }
    int IFlowNode.Id { get; set; }
}