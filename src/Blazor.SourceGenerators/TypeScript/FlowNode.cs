// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class FlowNode : IFlowNode
{
    FlowFlags IFlowNode.Flags { get; set; }
    int IFlowNode.Id { get; set; }
}