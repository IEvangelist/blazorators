// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class FlowNode : IFlowNode
{
    public FlowFlags Flags { get; set; }
    public int Id { get; set; }
}