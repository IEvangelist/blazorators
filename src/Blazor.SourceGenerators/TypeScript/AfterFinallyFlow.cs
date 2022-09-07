// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal sealed class AfterFinallyFlow : IFlowNode, IFlowLock
{
    internal FlowNode Antecedent { get; set; } = default!;
    bool IFlowLock.Locked { get; set; }
    FlowFlags IFlowNode.Flags { get; set; }
    int IFlowNode.Id { get; set; }
}